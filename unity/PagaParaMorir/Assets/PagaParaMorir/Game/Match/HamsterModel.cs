using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// El hámster de cada jugador, armado con primitivas (sin assets). Solo es visual: la colisión
    /// y los impactos los resuelve la cápsula del <see cref="NetworkPlayer"/>, y la cabeza coincide
    /// con la zona de <see cref="PagaParaMorir.Rules.HitZones"/> (desde 0.2 m sobre el pivote).
    /// </summary>
    public class HamsterModel : MonoBehaviour
    {
        private static readonly Color[] Furs =
        {
            new Color(0.86f, 0.60f, 0.32f), // dorado
            new Color(0.95f, 0.86f, 0.70f), // crema
            new Color(0.62f, 0.60f, 0.58f), // gris
            new Color(0.55f, 0.36f, 0.22f), // café
            new Color(0.96f, 0.95f, 0.92f), // blanco
            new Color(0.32f, 0.27f, 0.24f), // oscuro
        };
        private static readonly Color Pink = new Color(1f, 0.66f, 0.72f);
        private static readonly Color Black = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color White = Color.white;
        private static readonly Color Blood = new Color(0.72f, 0.04f, 0.08f);
        private static readonly Color Bone = new Color(0.97f, 0.95f, 0.87f);
        private static readonly Color Metal = new Color(0.15f, 0.15f, 0.17f);

        /// <summary>Altura del centro de la cabeza sobre el pivote.</summary>
        private const float HeadCenter = 0.58f;
        private const int RaycastIgnoreLayer = 2; // "Ignore Raycast": los disparos atraviesan los restos

        private Transform _body;
        private Transform _head;
        private readonly List<Transform> _eyes = new List<Transform>();
        private readonly List<GameObject> _detached = new List<GameObject>();
        private Vector3 _lastPosition;
        private float _walkPhase;
        private bool _dead;

        public static HamsterModel Build(Transform player, string id)
        {
            var go = new GameObject("Hamster");
            go.transform.SetParent(player, false);
            var model = go.AddComponent<HamsterModel>();
            model.Construct(id);
            return model;
        }

        private void Construct(string id)
        {
            var hash = 17;
            foreach (var c in id ?? "") hash = hash * 31 + c;
            var fur = Furs[(hash & 0x7fffffff) % Furs.Length];
            var light = Color.Lerp(fur, White, 0.55f);
            var bandana = Visuals.ColorFor(id);

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);

            // Cuerpo gordito
            Part(PrimitiveType.Sphere, _body, new Vector3(0, -0.35f, 0), new Vector3(1.0f, 1.15f, 0.95f), fur);
            Part(PrimitiveType.Sphere, _body, new Vector3(0, -0.42f, 0.22f), new Vector3(0.72f, 0.8f, 0.55f), light);
            // Pañuelo del color del jugador (para reconocerlo)
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, 0.17f, 0), new Vector3(0.78f, 0.05f, 0.78f), bandana);
            // Patitas, pies y colita
            Part(PrimitiveType.Sphere, _body, new Vector3(-0.42f, -0.15f, 0.28f), new Vector3(0.2f, 0.22f, 0.2f), Pink);
            Part(PrimitiveType.Sphere, _body, new Vector3(0.42f, -0.15f, 0.28f), new Vector3(0.2f, 0.22f, 0.2f), Pink);
            Part(PrimitiveType.Sphere, _body, new Vector3(-0.26f, -0.93f, 0.15f), new Vector3(0.26f, 0.12f, 0.34f), Pink);
            Part(PrimitiveType.Sphere, _body, new Vector3(0.26f, -0.93f, 0.15f), new Vector3(0.26f, 0.12f, 0.34f), Pink);
            Part(PrimitiveType.Sphere, _body, new Vector3(0, -0.72f, -0.46f), Vector3.one * 0.12f, Pink);
            // Arma
            Part(PrimitiveType.Cube, _body, new Vector3(0.32f, -0.12f, 0.5f), new Vector3(0.09f, 0.1f, 0.42f), Metal);

            // Cabeza grande (todo lo que sale volando en un disparo a la cabeza)
            _head = new GameObject("Head").transform;
            _head.SetParent(_body, false);
            _head.localPosition = new Vector3(0, HeadCenter, 0);
            Part(PrimitiveType.Sphere, _head, Vector3.zero, new Vector3(0.86f, 0.8f, 0.82f), fur);
            Part(PrimitiveType.Sphere, _head, new Vector3(0, -0.1f, 0.28f), new Vector3(0.5f, 0.38f, 0.32f), light);
            // Cachetes inflados
            Part(PrimitiveType.Sphere, _head, new Vector3(-0.3f, -0.1f, 0.2f), new Vector3(0.36f, 0.32f, 0.32f), fur);
            Part(PrimitiveType.Sphere, _head, new Vector3(0.3f, -0.1f, 0.2f), new Vector3(0.36f, 0.32f, 0.32f), fur);
            // Orejas
            Part(PrimitiveType.Sphere, _head, new Vector3(-0.27f, 0.36f, -0.05f), new Vector3(0.24f, 0.26f, 0.08f), fur);
            Part(PrimitiveType.Sphere, _head, new Vector3(0.27f, 0.36f, -0.05f), new Vector3(0.24f, 0.26f, 0.08f), fur);
            Part(PrimitiveType.Sphere, _head, new Vector3(-0.27f, 0.36f, -0.01f), new Vector3(0.15f, 0.17f, 0.05f), Pink);
            Part(PrimitiveType.Sphere, _head, new Vector3(0.27f, 0.36f, -0.01f), new Vector3(0.15f, 0.17f, 0.05f), Pink);
            // Ojos con brillo
            _eyes.Add(Part(PrimitiveType.Sphere, _head, new Vector3(-0.16f, 0.08f, 0.36f), Vector3.one * 0.12f, Black).transform);
            _eyes.Add(Part(PrimitiveType.Sphere, _head, new Vector3(0.16f, 0.08f, 0.36f), Vector3.one * 0.12f, Black).transform);
            Part(PrimitiveType.Sphere, _head, new Vector3(-0.14f, 0.11f, 0.41f), Vector3.one * 0.04f, White);
            Part(PrimitiveType.Sphere, _head, new Vector3(0.18f, 0.11f, 0.41f), Vector3.one * 0.04f, White);
            // Nariz y dientes de enfrente
            Part(PrimitiveType.Sphere, _head, new Vector3(0, -0.02f, 0.42f), new Vector3(0.09f, 0.07f, 0.07f), Pink);
            Part(PrimitiveType.Cube, _head, new Vector3(0, -0.2f, 0.38f), new Vector3(0.1f, 0.09f, 0.03f), White);

            _lastPosition = transform.position;
        }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>Eliminado: con disparo a la cabeza se la vuela; si no, cae noqueado.</summary>
        public void Die(bool headshot)
        {
            if (_dead) return;
            _dead = true;
            SetVisible(true);
            StartCoroutine(headshot ? Decapitate() : KnockOut());
        }

        private void Update()
        {
            if (_dead || _body == null) return;
            // Caminadito de hámster: se balancea de lado a lado al moverse.
            var delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0;
            var speed = Time.deltaTime > 0 ? delta.magnitude / Time.deltaTime : 0;
            var amount = Mathf.Clamp01(speed / 6.5f);
            _walkPhase += speed * Time.deltaTime * 2.2f;
            _body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_walkPhase * Mathf.PI) * 9f * amount);
            _body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_walkPhase * Mathf.PI)) * 0.06f * amount, 0);
        }

        private IEnumerator KnockOut()
        {
            foreach (var eye in _eyes) eye.localScale = new Vector3(0.14f, 0.02f, 0.12f); // ojos cerrados
            yield return Fall(-85f, 0.35f);
        }

        private IEnumerator Decapitate()
        {
            // La cabeza sale volando y rebota por la arena.
            var head = _head.gameObject;
            _head.SetParent(null, true);
            _detached.Add(head);
            head.layer = RaycastIgnoreLayer;
            var collider = head.AddComponent<SphereCollider>();
            collider.radius = 0.42f;
            var body = head.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            var launch = transform.up * 6f + Random.insideUnitSphere * 2.5f;
            SetVelocity(body, launch);
            body.angularVelocity = Random.insideUnitSphere * 12f;

            // Lo que queda: muñón, hueso de caricatura y salpicadura.
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.02f, 0.5f), Blood);
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, 0.42f, 0), new Vector3(0.09f, 0.2f, 0.09f), Bone);
            Part(PrimitiveType.Sphere, _body, new Vector3(-0.06f, 0.64f, 0), Vector3.one * 0.13f, Bone);
            Part(PrimitiveType.Sphere, _body, new Vector3(0.06f, 0.64f, 0), Vector3.one * 0.13f, Bone);
            Splatter(_body.TransformPoint(new Vector3(0, 0.25f, 0)));

            // Se queda parado un momento, sin cabeza... y cae.
            yield return new WaitForSeconds(1.2f);
            yield return Fall(80f, 0.45f);
        }

        /// <summary>Cae hacia atrás (ángulo negativo) o hacia adelante (positivo) hasta el suelo.</summary>
        private IEnumerator Fall(float angle, float seconds)
        {
            var startRotation = _body.localRotation;
            var endRotation = Quaternion.Euler(angle, 0, 0);
            var startPosition = _body.localPosition;
            var endPosition = new Vector3(0, -0.55f, 0);
            for (var t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                var eased = t * t;
                _body.localRotation = Quaternion.Slerp(startRotation, endRotation, eased);
                _body.localPosition = Vector3.Lerp(startPosition, endPosition, eased);
                yield return null;
            }
            _body.localRotation = endRotation;
            _body.localPosition = endPosition;
        }

        private void Splatter(Vector3 origin)
        {
            for (var i = 0; i < 12; i++)
            {
                var drop = Visuals.Shape(PrimitiveType.Sphere, null, origin, Vector3.one * Random.Range(0.05f, 0.11f), Blood);
                drop.layer = RaycastIgnoreLayer;
                var rb = drop.AddComponent<Rigidbody>();
                rb.mass = 0.02f;
                SetVelocity(rb, Vector3.up * Random.Range(2f, 5f) + Random.insideUnitSphere * 2.5f);
                _detached.Add(drop);
                Destroy(drop, Random.Range(1.2f, 2f));
            }
        }

        private GameObject Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var part = Visuals.Shape(type, parent, position, scale, color);
            part.layer = RaycastIgnoreLayer;
            return part;
        }

        private static void SetVelocity(Rigidbody body, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        private void OnDestroy()
        {
            // La cabeza y las gotas no son hijas del jugador: limpiarlas con él.
            foreach (var go in _detached)
                if (go != null) Destroy(go);
        }
    }
}
