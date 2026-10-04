using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Materiales y efectos sin assets: sirven igual con el render pipeline integrado o con URP.
    /// </summary>
    public static class Visuals
    {
        private static Material _litBase;
        private static Material _unlitBase;

        /// <summary>Material con iluminación (copia el material por defecto de las primitivas).</summary>
        public static Material Lit(Color color)
        {
            if (_litBase == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _litBase = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            return new Material(_litBase) { color = color };
        }

        /// <summary>Material sin iluminación para líneas (zona, disparos).</summary>
        public static Material Unlit(Color color)
        {
            if (_unlitBase == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _unlitBase = shader != null ? new Material(shader) : Lit(Color.white);
            }
            return new Material(_unlitBase) { color = color };
        }

        /// <summary>Primitiva solo visual (sin collider).</summary>
        public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = Lit(color);
            return go;
        }

        /// <summary>Color estable por jugador, a partir de su id.</summary>
        public static Color ColorFor(string id)
        {
            var hash = 17;
            foreach (var c in id ?? "") hash = hash * 31 + c;
            var hue = (hash & 0xFFFF) / 65535f;
            return Color.HSVToRGB(hue, 0.65f, 0.95f);
        }

        /// <summary>"7xKX…9fQa" para mostrar llaves públicas.</summary>
        public static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length <= 10 ? id : id.Substring(0, 4) + "…" + id.Substring(id.Length - 4);
        }

        /// <summary>Línea breve de un disparo.</summary>
        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.material = Unlit(new Color(1f, 0.85f, 0.3f));
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.04f;
            line.endWidth = 0.02f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, 0.06f);
        }
    }

    /// <summary>Anillos rojos que marcan el borde de la zona segura.</summary>
    public class ZoneVisual : MonoBehaviour
    {
        private const int Segments = 96;
        private LineRenderer[] _rings;
        private float _radius = -1;

        public static ZoneVisual Create(Transform parent)
        {
            var go = new GameObject("Zone");
            go.transform.SetParent(parent, false);
            var zone = go.AddComponent<ZoneVisual>();
            zone._rings = new[] { zone.Ring(0.15f, 0.6f), zone.Ring(3f, 0.25f), zone.Ring(6f, 0.15f) };
            return zone;
        }

        private LineRenderer Ring(float height, float width)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, height, 0);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = line.endWidth = width;
            line.material = Visuals.Unlit(new Color(1f, 0.15f, 0.2f, 0.9f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        public void SetRadius(float radius)
        {
            if (Mathf.Approximately(radius, _radius)) return;
            _radius = radius;
            var points = new Vector3[Segments];
            for (var i = 0; i < Segments; i++)
            {
                var a = i * Mathf.PI * 2f / Segments;
                points[i] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            }
            foreach (var ring in _rings)
            {
                ring.enabled = radius > 0.05f;
                ring.SetPositions(points);
            }
        }
    }
}
