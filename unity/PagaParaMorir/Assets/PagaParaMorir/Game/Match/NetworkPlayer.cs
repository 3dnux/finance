using System.Collections.Generic;
using PagaParaMorir.Rules;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Jugador en red con predicción del lado del cliente.
    /// <list type="bullet">
    /// <item><b>Dueño:</b> simula su propio movimiento al instante, 30 ticks por segundo, con las
    /// mismas reglas que el servidor (<see cref="Movement"/>). Guarda cada input y lo manda al
    /// servidor (los 3 últimos por paquete, por si alguno se pierde).</item>
    /// <item><b>Servidor:</b> es la autoridad. Aplica los inputs en orden, limita su ritmo
    /// (<see cref="ServerInputQueue"/>), dispara y publica su estado con el último tick procesado.</item>
    /// <item><b>Reconciliación:</b> si el estado del servidor no coincide con lo que el dueño
    /// predijo para ese tick, el dueño parte del estado del servidor y rehace los inputs
    /// pendientes (<see cref="PredictionBuffer{TInput,TState}"/>). La diferencia se suaviza en
    /// la cámara para que no se note el salto.</item>
    /// <item><b>Los demás:</b> se dibujan un poco en el pasado, interpolando entre estados
    /// recibidos (<see cref="SnapshotBuffer"/>).</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        /// <summary>Altura de los ojos sobre el pivote (centro de la cápsula de 2 m).</summary>
        public const float EyeHeight = 0.65f;
        private const int InputsPerPacket = 3;
        /// <summary>Diferencia con el servidor que se considera "igual" (no corrige).</summary>
        private const float ReconcileTolerance = 0.02f;
        /// <summary>Correcciones más grandes que esto (p. ej. una aparición) no se suavizan.</summary>
        private const float SnapDistance = 3f;
        private const int MaxTicksPerFrame = 5;

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
        /// <summary>Estado de movimiento autoritativo, con el último tick del dueño que el servidor procesó.</summary>
        public readonly NetworkVariable<NetState> State = new NetworkVariable<NetState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string Id => WalletId.Value.ToString();
        public WeaponDef CurrentWeapon => Weapons.Get((WeaponId)Weapon.Value);

        // ---------- Mensajes de red (copiables byte a byte) ----------

        public struct NetInput : INetworkSerializeByMemcpy
        {
            public uint Tick;
            public Vector2 Move;
            public float Yaw;
            public float Pitch;
            public byte Flags;
            public byte Weapon;

            private const byte JumpFlag = 1, FireFlag = 2, ReloadFlag = 4;

            public static NetInput From(PlayerInput i) => new NetInput
            {
                Tick = i.Tick,
                Move = new Vector2(i.Move.X, i.Move.Y),
                Yaw = i.Yaw,
                Pitch = i.Pitch,
                Flags = (byte)((i.Jump ? JumpFlag : 0) | (i.Fire ? FireFlag : 0) | (i.Reload ? ReloadFlag : 0)),
                Weapon = i.Weapon,
            };

            public PlayerInput ToInput() => new PlayerInput
            {
                Tick = Tick,
                // El servidor no confía en el largo que manda el cliente.
                Move = Clamp01(new NVector2(Move.x, Move.y)),
                Yaw = Yaw,
                Pitch = Mathf.Clamp(Pitch, -85f, 85f),
                Jump = (Flags & JumpFlag) != 0,
                Fire = (Flags & FireFlag) != 0,
                Reload = (Flags & ReloadFlag) != 0,
                Weapon = Weapon,
            };

            private static NVector2 Clamp01(NVector2 v) => v.LengthSquared() > 1f ? NVector2.Normalize(v) : v;
        }

        /// <summary>Los últimos inputs del dueño (el más nuevo al final).</summary>
        public struct InputPacket : INetworkSerializeByMemcpy
        {
            public NetInput A;
            public NetInput B;
            public NetInput C;
            public byte Count;
        }

        public struct NetState : INetworkSerializeByMemcpy
        {
            public Vector3 Position;
            public float VerticalVelocity;
            public float Yaw;
            public float Pitch;
            public uint Ack;
            public byte Grounded;

            public MoveState ToMoveState() =>
                new MoveState(new NVector3(Position.x, Position.y, Position.z), VerticalVelocity, Grounded != 0);
        }

        private CharacterController _controller;
        private Renderer _body;
        private TextMesh _label;

        // Dueño
        private float _localYaw;
        private float _localPitch;
        private bool _jumpPending;
        private bool _reloadPending;
        private byte _selectedWeapon;
        private uint _tick;
        private float _tickAccumulator;
        private readonly List<PlayerInput> _recentInputs = new List<PlayerInput>();
        private readonly PredictionBuffer<PlayerInput, MoveState> _prediction = new PredictionBuffer<PlayerInput, MoveState>();
        private MoveState _predicted;
        private Vector3 _previousTickPosition;
        private Vector3 _visualError;
        private WeaponState[] _localWeapons;
        private bool _localFireWasHeld;
        private Camera _camera;
        private Transform _cameraParentBefore;

        // Servidor
        private readonly ServerInputQueue _inputs = new ServerInputQueue();
        private MoveState _serverState;
        private float _serverYaw;
        private float _serverPitch;
        private bool _fireWasHeld;
        private WeaponState[] _weapons;
        private System.Random _spreadRng;

        // Otros jugadores
        private readonly SnapshotBuffer _snapshots = new SnapshotBuffer();

        private bool Predicting => IsOwner && !IsServer;

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
                _weapons = NewWeapons();
                _spreadRng = new System.Random(unchecked((int)OwnerClientId * 7919 + 1));
                _serverState = new MoveState(ToNumerics(transform.position), 0, false);
                _serverYaw = transform.eulerAngles.y;
                WalletId.Value = new FixedString64Bytes(GameSession.Instance.Server.IdOf(OwnerClientId));
                Ammo.Value = _weapons[0].Ammo;
                PublishState();
            }

            // El servidor y el dueño simulan con el CharacterController; en los demás equipos
            // sirve de colisión para que la predicción y los disparos predichos choquen como en el servidor.
            _controller.enabled = true;

            if (IsOwner)
            {
                Local = this;
                _localYaw = transform.eulerAngles.y;
                _localWeapons = NewWeapons();
                _predicted = new MoveState(ToNumerics(transform.position), 0, false);
                _previousTickPosition = transform.position;
                AttachCamera();
                PlayerInputReader.LockCursor(true);
            }

            if (!IsServer && !IsOwner)
                State.OnValueChanged += (_, state) => _snapshots.Add(Time.timeAsDouble, ToNumerics(state.Position), state.Yaw);

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
            if (IsServer) ServerUpdate();
            else if (!IsOwner) FollowSnapshots();
        }

        private void LateUpdate()
        {
            // La etiqueta con el nombre siempre mira a la cámara.
            var cam = Camera.main;
            if (_label != null && cam != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }

        private static WeaponState[] NewWeapons()
        {
            var weapons = new WeaponState[Weapons.All.Count];
            for (var i = 0; i < weapons.Length; i++) weapons[i] = new WeaponState(Weapons.All[i]);
            return weapons;
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
                _jumpPending |= input.Jump;
                _reloadPending |= input.Reload;
                if (input.WeaponSlot >= 0) _selectedWeapon = (byte)input.WeaponSlot;
            }

            if (Predicting) Reconcile();

            // Ticks fijos: mismas reglas y mismo paso que el servidor.
            _tickAccumulator += Time.deltaTime;
            var ticks = 0;
            while (_tickAccumulator >= Movement.TickSeconds && ticks++ < MaxTicksPerFrame)
            {
                _tickAccumulator -= Movement.TickSeconds;
                OwnerTick(playing ? input.Move : Vector2.zero, playing && input.FireHeld);
            }
            if (ticks > MaxTicksPerFrame) _tickAccumulator = 0; // el equipo se trabó: no intentar alcanzar

            // Mirar es inmediato en este equipo.
            transform.rotation = Quaternion.Euler(0, _localYaw, 0);
            if (_camera != null && Alive.Value)
            {
                _camera.transform.localRotation = Quaternion.Euler(_localPitch, 0, 0);
                _camera.transform.localPosition = new Vector3(0, EyeHeight, 0) + CameraSmoothing();
            }
        }

        private void OwnerTick(Vector2 move, bool fireHeld)
        {
            var input = new PlayerInput
            {
                Tick = ++_tick,
                Move = new NVector2(move.x, move.y),
                Yaw = _localYaw,
                Pitch = _localPitch,
                Jump = _jumpPending,
                Fire = fireHeld,
                Reload = _reloadPending,
                Weapon = _selectedWeapon,
            };
            _jumpPending = false;
            _reloadPending = false;

            if (Predicting && Alive.Value)
            {
                _previousTickPosition = transform.position;
                _predicted = Simulate(_predicted, input);
                _prediction.Record(input.Tick, input, _predicted);
                PredictShot(input);
            }

            _recentInputs.Add(input);
            if (_recentInputs.Count > InputsPerPacket) _recentInputs.RemoveAt(0);
            SubmitInputsRpc(Pack(_recentInputs));
        }

        private void Reconcile()
        {
            var server = State.Value;
            if (server.Ack <= _prediction.LastAcknowledged || !Alive.Value) return;
            var before = transform.position;
            _predicted = _prediction.Reconcile(server.Ack, server.ToMoveState(), _predicted,
                (a, b) => NVector3.Distance(a.Position, b.Position) < ReconcileTolerance,
                Simulate, out var corrected);
            if (!corrected) return;

            var jump = before - transform.position;
            if (jump.magnitude > SnapDistance)
            {
                // Lo movió el servidor (aparición): ir directo, sin suavizar.
                _visualError = Vector3.zero;
                _previousTickPosition = transform.position;
            }
            else
            {
                _visualError += jump;
                _previousTickPosition -= jump;
            }
        }

        /// <summary>
        /// Desplazamiento de la cámara respecto del cuerpo simulado: interpola entre ticks
        /// (el juego corre a más FPS que la simulación) y suaviza las correcciones.
        /// </summary>
        private Vector3 CameraSmoothing()
        {
            if (!IsOwner) return Vector3.zero;
            _visualError *= Mathf.Exp(-12f * Time.deltaTime);
            if (_visualError.sqrMagnitude < 1e-6f) _visualError = Vector3.zero;
            var alpha = Mathf.Clamp01(_tickAccumulator / Movement.TickSeconds);
            var interpolated = Vector3.Lerp(_previousTickPosition, transform.position, alpha);
            return transform.InverseTransformVector(interpolated - transform.position + _visualError);
        }

        /// <summary>Disparo inmediato en pantalla (solo efecto: el daño lo decide el servidor).</summary>
        private void PredictShot(PlayerInput input)
        {
            if (Frozen.Value || _localWeapons == null || MatchController.Instance == null ||
                MatchController.Instance.Phase.Value != MatchPhase.Playing)
            {
                _localFireWasHeld = input.Fire;
                return;
            }
            var weapon = _localWeapons[Mathf.Clamp(input.Weapon, 0, _localWeapons.Length - 1)];
            var trigger = weapon.Def.Automatic ? input.Fire : input.Fire && !_localFireWasHeld;
            _localFireWasHeld = input.Fire;
            if (input.Reload) weapon.StartReload(Time.timeAsDouble);
            if (!trigger || !weapon.TryFire(Time.timeAsDouble) || _camera == null) return;

            var origin = _camera.transform.position;
            var direction = _camera.transform.forward;
            var end = Raycast(origin, direction, weapon.Def.Range, out var hit) ? hit.point : origin + direction * weapon.Def.Range;
            Visuals.Tracer(origin + direction * 0.6f + Vector3.down * 0.15f, end);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SubmitInputsRpc(InputPacket packet)
        {
            foreach (var input in _inputs.Accept(Unpack(packet), Time.timeAsDouble)) ServerProcess(input);
            PublishState();
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

        private static InputPacket Pack(List<PlayerInput> inputs)
        {
            var packet = new InputPacket { Count = (byte)inputs.Count };
            if (inputs.Count > 0) packet.A = NetInput.From(inputs[0]);
            if (inputs.Count > 1) packet.B = NetInput.From(inputs[1]);
            if (inputs.Count > 2) packet.C = NetInput.From(inputs[2]);
            return packet;
        }

        private static List<PlayerInput> Unpack(InputPacket packet)
        {
            var list = new List<PlayerInput>(3);
            if (packet.Count > 0) list.Add(packet.A.ToInput());
            if (packet.Count > 1) list.Add(packet.B.ToInput());
            if (packet.Count > 2) list.Add(packet.C.ToInput());
            return list;
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

        // ---------- Simulación (servidor y dueño) ----------

        /// <summary>Un tick de movimiento: reglas de <see cref="Movement"/> + colisiones del CharacterController.</summary>
        private MoveState Simulate(MoveState state, PlayerInput input)
        {
            var target = ToUnity(state.Position);
            if ((transform.position - target).sqrMagnitude > 1e-8f)
            {
                _controller.enabled = false;
                transform.position = target;
                _controller.enabled = true;
            }
            var delta = Movement.Step(ref state, input, Frozen.Value);
            _controller.Move(ToUnity(delta));
            state.Position = ToNumerics(transform.position);
            state.Grounded = _controller.isGrounded;
            return state;
        }

        // ---------- Servidor ----------

        private void ServerUpdate()
        {
            if (_weapons == null) return;
            // Inputs que esperaban un tick faltante y ya no conviene esperar más.
            foreach (var input in _inputs.Poll(Time.timeAsDouble)) ServerProcess(input);

            var now = Time.timeAsDouble;
            var weapon = _weapons[Weapon.Value];
            weapon.Update(now);
            if (Ammo.Value != weapon.Ammo) Ammo.Value = weapon.Ammo;
            if (Reloading.Value != weapon.Reloading) Reloading.Value = weapon.Reloading;
            PublishState();
        }

        private void ServerProcess(PlayerInput input)
        {
            _serverYaw = input.Yaw;
            _serverPitch = input.Pitch;
            if (!IsOwner) transform.rotation = Quaternion.Euler(0, _serverYaw, 0);
            if (!Alive.Value) return;

            if (IsOwner) _previousTickPosition = transform.position; // host: suavizado de cámara
            _serverState = Simulate(_serverState, input);

            var now = Time.timeAsDouble;
            var slot = Mathf.Clamp(input.Weapon, 0, _weapons.Length - 1);
            if (slot != Weapon.Value)
            {
                _weapons[Weapon.Value].CancelReload();
                Weapon.Value = (byte)slot;
            }
            var weapon = _weapons[slot];
            if (input.Reload) weapon.StartReload(now);

            var trigger = weapon.Def.Automatic ? input.Fire : input.Fire && !_fireWasHeld;
            _fireWasHeld = input.Fire;
            if (trigger && !Frozen.Value && GameSession.Instance.Server.CanShoot && weapon.TryFire(now))
                FireShot(weapon.Def);
        }

        private void PublishState()
        {
            State.Value = new NetState
            {
                Position = transform.position,
                VerticalVelocity = _serverState.VerticalVelocity,
                Grounded = (byte)(_serverState.Grounded ? 1 : 0),
                Yaw = _serverYaw,
                Pitch = _serverPitch,
                Ack = _inputs.LastProcessedTick,
            };
        }

        private void FireShot(WeaponDef def)
        {
            var origin = transform.position + Vector3.up * EyeHeight;
            var aim = Quaternion.Euler(_serverPitch, _serverYaw, 0);
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
                MatchController.Instance?.ShotRpc(OwnerClientId, origin + direction * 0.6f + Vector3.down * 0.15f, end);
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
            _serverState = new MoveState(ToNumerics(position), 0, false);
            _serverYaw = yaw;
            _serverPitch = 0;
            PublishState();
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

        // ---------- Otros jugadores ----------

        private void FollowSnapshots()
        {
            if (!_snapshots.Sample(Time.timeAsDouble, out var position, out var yaw))
            {
                var state = State.Value;
                transform.SetPositionAndRotation(state.Position, Quaternion.Euler(0, state.Yaw, 0));
                return;
            }
            transform.SetPositionAndRotation(ToUnity(position), Quaternion.Euler(0, yaw, 0));
        }

        private void OnAliveChanged(bool alive)
        {
            // Muerto: sin colisión (los disparos lo atraviesan) y sin cuerpo visible.
            _controller.enabled = alive;
            if (_body != null && !IsOwner) _body.enabled = alive;
            if (_label != null) _label.gameObject.SetActive(alive);
            if (IsOwner && !alive)
            {
                _prediction.Clear();
                if (_camera != null)
                {
                    // Vista de espectador: desde arriba.
                    _camera.transform.localPosition = new Vector3(0, 18f, -10f);
                    _camera.transform.localRotation = Quaternion.Euler(60f, 0, 0);
                }
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

        private static NVector3 ToNumerics(Vector3 v) => new NVector3(v.x, v.y, v.z);
        private static Vector3 ToUnity(NVector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
