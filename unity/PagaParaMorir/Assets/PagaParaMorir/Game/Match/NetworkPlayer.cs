using System.Collections.Generic;
using PagaParaMorir.Rules;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Jugador en red. El servidor es la autoridad: mueve al personaje, dispara y aplica daño.
    /// El dueño solo manda su input; los demás clientes interpolan la posición que publica el servidor.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>Altura de los ojos sobre el pivote (centro de la cápsula de 2 m).</summary>
        public const float EyeHeight = 0.65f;
        private const float MoveSpeed = 6.5f;
        private const float JumpSpeed = 7f;
        private const float Gravity = -20f;
        private const float InputSendInterval = 1f / 30f;

        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();
        /// <summary>El jugador de este equipo (null en el servidor dedicado).</summary>
        public static NetworkPlayer Local { get; private set; }

        public readonly NetworkVariable<FixedString64Bytes> WalletId = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> Alive = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Quieto durante la cuenta regresiva y al terminar.</summary>
        public readonly NetworkVariable<bool> Frozen = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> Weapon = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> Ammo = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> Reloading = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> Yaw = new NetworkVariable<float>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> Pitch = new NetworkVariable<float>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string Id => WalletId.Value.ToString();
        public WeaponDef CurrentWeapon => Weapons.Get((WeaponId)Weapon.Value);

        /// <summary>
        /// Input que el dueño manda al servidor ~30 veces por segundo, sin garantía de entrega.
        /// Saltar y recargar van como contadores para que un paquete perdido no los pierda.
        /// </summary>
        public struct InputFrame : INetworkSerializable
        {
            public ushort Sequence;
            public Vector2 Move;
            public float Yaw;
            public float Pitch;
            public byte JumpCount;
            public byte ReloadCount;
            public bool Fire;
            public byte Weapon;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Sequence);
                serializer.SerializeValue(ref Move);
                serializer.SerializeValue(ref Yaw);
                serializer.SerializeValue(ref Pitch);
                serializer.SerializeValue(ref JumpCount);
                serializer.SerializeValue(ref ReloadCount);
                serializer.SerializeValue(ref Fire);
                serializer.SerializeValue(ref Weapon);
            }
        }

        private CharacterController _controller;
        private Renderer _body;
        private TextMesh _label;

        // Dueño
        private float _localYaw;
        private float _localPitch;
        private float _nextInputSend;
        private ushort _sequence;
        private byte _jumpCount;
        private byte _reloadCount;
        private byte _selectedWeapon;
        private Camera _camera;
        private Transform _cameraParentBefore;

        // Servidor
        private InputFrame _input;
        private bool _hasInput;
        private bool _jumpQueued;
        private bool _reloadQueued;
        private bool _fireWasHeld;
        private float _verticalVelocity;
        private WeaponState[] _weapons;
        private System.Random _spreadRng;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _body = GetComponentInChildren<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                _weapons = new WeaponState[Weapons.All.Count];
                for (var i = 0; i < _weapons.Length; i++) _weapons[i] = new WeaponState(Weapons.All[i]);
                _spreadRng = new System.Random(unchecked((int)OwnerClientId * 7919 + 1));
                Position.Value = transform.position;
                Yaw.Value = transform.eulerAngles.y;
                WalletId.Value = new FixedString64Bytes(GameSession.Instance.Server.IdOf(OwnerClientId));
                Ammo.Value = _weapons[0].Ammo;
            }
            else
            {
                // En los clientes la posición viene del servidor.
                _controller.enabled = false;
            }

            if (IsOwner)
            {
                Local = this;
                _localYaw = transform.eulerAngles.y;
                AttachCamera();
                PlayerInputReader.LockCursor(true);
            }

            WalletId.OnValueChanged += (_, __) => RefreshLooks();
            Alive.OnValueChanged += (_, alive) => OnAliveChanged(alive);
            RefreshLooks();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this)
            {
                Local = null;
                DetachCamera();
                PlayerInputReader.LockCursor(false);
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) OwnerUpdate();
            if (IsServer) ServerUpdate(Time.deltaTime);
            else FollowServerState();
        }

        private void LateUpdate()
        {
            // La etiqueta con el nombre siempre mira a la cámara.
            var cam = Camera.main;
            if (_label != null && cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }

        // ---------- Dueño (este equipo) ----------

        private void OwnerUpdate()
        {
            var input = PlayerInputReader.Read();
            if (input.ToggleCursor) PlayerInputReader.LockCursor(!PlayerInputReader.CursorLocked);
            var playing = PlayerInputReader.CursorLocked && Alive.Value;

            if (playing)
            {
                _localYaw = Mathf.Repeat(_localYaw + input.Look.x, 360f);
                _localPitch = Mathf.Clamp(_localPitch - input.Look.y, -85f, 85f);
                if (input.Jump) _jumpCount++;
                if (input.Reload) _reloadCount++;
                if (input.WeaponSlot >= 0) _selectedWeapon = (byte)input.WeaponSlot;
            }

            // Mirar es inmediato en este equipo; la posición llega del servidor.
            transform.rotation = Quaternion.Euler(0, _localYaw, 0);
            if (_camera != null && Alive.Value)
                _camera.transform.localRotation = Quaternion.Euler(_localPitch, 0, 0);

            if (Time.unscaledTime < _nextInputSend) return;
            _nextInputSend = Time.unscaledTime + InputSendInterval;
            SubmitInputRpc(new InputFrame
            {
                Sequence = ++_sequence,
                Move = playing ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero,
                Yaw = _localYaw,
                Pitch = _localPitch,
                JumpCount = _jumpCount,
                ReloadCount = _reloadCount,
                Fire = playing && input.FireHeld,
                Weapon = _selectedWeapon,
            });
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputRpc(InputFrame frame)
        {
            if (_hasInput)
            {
                // Descarta paquetes viejos que llegaron tarde (con vuelta del contador).
                if ((short)(frame.Sequence - _input.Sequence) <= 0) return;
                _jumpQueued |= frame.JumpCount != _input.JumpCount;
                _reloadQueued |= frame.ReloadCount != _input.ReloadCount;
            }
            _input = frame;
            _hasInput = true;
        }

        /// <summary>El servidor movió al jugador (aparición): el dueño ajusta hacia dónde mira.</summary>
        [Rpc(SendTo.Owner)]
        private void TeleportedRpc(float yaw)
        {
            _localYaw = yaw;
            _localPitch = 0;
        }

        /// <summary>Marca de impacto para quien disparó.</summary>
        [Rpc(SendTo.Owner)]
        public void HitConfirmedRpc(bool eliminated)
        {
            Hud.Instance?.ShowHitMarker(eliminated);
        }

        private void AttachCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                _camera = new GameObject("Camera").AddComponent<Camera>();
                _camera.tag = "MainCamera";
            }
            _cameraParentBefore = _camera.transform.parent;
            _camera.transform.SetParent(transform, false);
            _camera.transform.localPosition = new Vector3(0, EyeHeight, 0);
            _camera.transform.localRotation = Quaternion.identity;
            _camera.nearClipPlane = 0.05f;
            if (_body != null) _body.enabled = false; // primera persona: no vemos nuestro cuerpo

            // Arma en pantalla (solo visual).
            var gun = Visuals.Shape(PrimitiveType.Cube, _camera.transform, new Vector3(0.25f, -0.22f, 0.55f),
                new Vector3(0.08f, 0.1f, 0.45f), new Color(0.15f, 0.15f, 0.17f));
            gun.name = "ViewGun";
        }

        private void DetachCamera()
        {
            if (_camera == null) return;
            var gun = _camera.transform.Find("ViewGun");
            if (gun != null) Destroy(gun.gameObject);
            _camera.transform.SetParent(_cameraParentBefore, false);
            _camera = null;
        }

        // ---------- Servidor ----------

        private void ServerUpdate(float dt)
        {
            if (!Alive.Value || _weapons == null) return;
            var now = Time.timeAsDouble;
            var frozen = Frozen.Value;

            Yaw.Value = _input.Yaw;
            Pitch.Value = Mathf.Clamp(_input.Pitch, -85f, 85f);
            if (!IsOwner) transform.rotation = Quaternion.Euler(0, Yaw.Value, 0);

            // Movimiento
            var move = Vector3.zero;
            if (!frozen)
            {
                var wish = Quaternion.Euler(0, _input.Yaw, 0) * new Vector3(_input.Move.x, 0, _input.Move.y);
                move = Vector3.ClampMagnitude(wish, 1f) * MoveSpeed;
            }
            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
                if (_jumpQueued && !frozen) _verticalVelocity = JumpSpeed;
            }
            _verticalVelocity += Gravity * dt;
            move.y = _verticalVelocity;
            _controller.Move(move * dt);
            if ((transform.position - Position.Value).sqrMagnitude > 0.0001f) Position.Value = transform.position;
            _jumpQueued = false;

            // Armas
            var slot = Mathf.Clamp(_input.Weapon, 0, _weapons.Length - 1);
            if (slot != Weapon.Value)
            {
                _weapons[Weapon.Value].CancelReload();
                Weapon.Value = (byte)slot;
            }
            var weapon = _weapons[slot];
            if (_reloadQueued) weapon.StartReload(now);
            _reloadQueued = false;

            var trigger = weapon.Def.Automatic ? _input.Fire : _input.Fire && !_fireWasHeld;
            _fireWasHeld = _input.Fire;
            if (trigger && !frozen && GameSession.Instance.Server.CanShoot && weapon.TryFire(now))
                FireShot(weapon.Def);

            weapon.Update(now);
            if (Ammo.Value != weapon.Ammo) Ammo.Value = weapon.Ammo;
            if (Reloading.Value != weapon.Reloading) Reloading.Value = weapon.Reloading;
        }

        private void FireShot(WeaponDef def)
        {
            var origin = transform.position + Vector3.up * EyeHeight;
            var aim = Quaternion.Euler(Pitch.Value, Yaw.Value, 0);
            for (var i = 0; i < def.Pellets; i++)
            {
                var spread = Quaternion.Euler(
                    ((float)_spreadRng.NextDouble() * 2 - 1) * def.SpreadDegrees,
                    ((float)_spreadRng.NextDouble() * 2 - 1) * def.SpreadDegrees, 0);
                var direction = aim * spread * Vector3.forward;
                var end = origin + direction * def.Range;

                if (Raycast(origin, direction, def.Range, out var hit))
                {
                    end = hit.point;
                    var victim = hit.collider.GetComponentInParent<NetworkPlayer>();
                    if (victim != null) GameSession.Instance.Server.OnHit(this, victim, def.Damage);
                }
                // El trazo sale un poco delante del jugador para que se vea desde su cámara.
                MatchController.Instance?.ShotRpc(origin + direction * 0.6f + Vector3.down * 0.15f, end);
            }
        }

        private bool Raycast(Vector3 origin, Vector3 direction, float range, out RaycastHit closest)
        {
            closest = default;
            var found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (!found || hit.distance < closest.distance)
                {
                    closest = hit;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Lleva al jugador a un punto de aparición (solo servidor).</summary>
        public void ServerTeleport(Vector3 position, float yaw)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            _controller.enabled = true;
            _verticalVelocity = 0;
            Position.Value = position;
            Yaw.Value = yaw;
            _input.Yaw = yaw;
            _input.Pitch = 0;
            TeleportedRpc(yaw);
        }

        /// <summary>Copia el estado del árbitro (solo servidor).</summary>
        public void ServerSync(PlayerRecord record, bool frozen)
        {
            var health = record?.Health ?? 0;
            var alive = record?.Alive ?? false;
            if (Health.Value != health) Health.Value = health;
            if (Alive.Value != alive) Alive.Value = alive;
            if (Frozen.Value != frozen) Frozen.Value = frozen;
        }

        // ---------- Clientes ----------

        private void FollowServerState()
        {
            var target = Position.Value;
            transform.position = (transform.position - target).sqrMagnitude > 25f
                ? target
                : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
            if (!IsOwner)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, Yaw.Value, 0),
                    1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        private void OnAliveChanged(bool alive)
        {
            // Muerto: sin colisión (los disparos lo atraviesan) y sin cuerpo visible.
            if (IsServer) _controller.enabled = alive;
            if (_body != null && !IsOwner) _body.enabled = alive;
            if (_label != null) _label.gameObject.SetActive(alive);
            if (IsOwner && !alive && _camera != null)
            {
                // Vista de espectador: desde arriba.
                _camera.transform.localPosition = new Vector3(0, 18f, -10f);
                _camera.transform.localRotation = Quaternion.Euler(60f, 0, 0);
            }
        }

        private void RefreshLooks()
        {
            if (Application.isBatchMode) return;
            var color = Visuals.ColorFor(Id);
            if (_body != null) _body.material = Visuals.Lit(color);
            if (IsOwner) return;
            if (_label == null)
            {
                var go = new GameObject("Name");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 1.4f, 0);
                _label = go.AddComponent<TextMesh>();
                _label.font = UI.Ui.Font;
                go.GetComponent<MeshRenderer>().material = _label.font.material;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.characterSize = 0.08f;
                _label.fontSize = 48;
            }
            _label.text = Visuals.ShortId(Id);
            _label.color = color;
        }
    }
}
