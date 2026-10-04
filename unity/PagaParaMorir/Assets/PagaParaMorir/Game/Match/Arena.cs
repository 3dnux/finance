using System.Collections.Generic;
using UnityEngine;

namespace PagaParaMorir.Game.Match
{
    /// <summary>
    /// Arena del prototipo: piso, muros y coberturas generados con una semilla fija.
    /// Servidor y clientes la construyen igual, así no hace falta sincronizarla.
    /// </summary>
    public class Arena
    {
        public const float HalfSize = 70f;
        private const int Seed = 1337;
        private const float SpawnRadius = 48f;
        private const int SpawnCount = 16;

        public GameObject Root { get; }
        public IReadOnlyList<Vector3> SpawnPoints => _spawns;
        public ZoneVisual Zone { get; }

        private readonly List<Vector3> _spawns = new List<Vector3>();

        private Arena(GameObject root, ZoneVisual zone)
        {
            Root = root;
            Zone = zone;
        }

        public static Arena Build()
        {
            var root = new GameObject("Arena");
            var arena = new Arena(root, ZoneVisual.Create(root.transform));

            Block(root.transform, new Vector3(0, -0.5f, 0), new Vector3(HalfSize * 2, 1, HalfSize * 2), new Color(0.22f, 0.26f, 0.2f));
            var wall = new Color(0.3f, 0.3f, 0.34f);
            Block(root.transform, new Vector3(0, 4, HalfSize), new Vector3(HalfSize * 2, 8, 1), wall);
            Block(root.transform, new Vector3(0, 4, -HalfSize), new Vector3(HalfSize * 2, 8, 1), wall);
            Block(root.transform, new Vector3(HalfSize, 4, 0), new Vector3(1, 8, HalfSize * 2), wall);
            Block(root.transform, new Vector3(-HalfSize, 4, 0), new Vector3(1, 8, HalfSize * 2), wall);

            for (var i = 0; i < SpawnCount; i++)
            {
                var a = i * Mathf.PI * 2f / SpawnCount;
                arena._spawns.Add(new Vector3(Mathf.Cos(a) * SpawnRadius, 1.1f, Mathf.Sin(a) * SpawnRadius));
            }

            // Coberturas: lejos del centro exacto y de los puntos de aparición.
            var rng = new System.Random(Seed);
            var placed = 0;
            for (var attempt = 0; attempt < 400 && placed < 55; attempt++)
            {
                var pos = new Vector3(Range(rng, -60, 60), 0, Range(rng, -60, 60));
                if (pos.magnitude < 5 || pos.magnitude > 62 || NearSpawn(arena._spawns, pos, 5)) continue;
                var tall = rng.NextDouble() < 0.2;
                var size = tall
                    ? new Vector3(Range(rng, 2, 4), Range(rng, 5, 9), Range(rng, 2, 4))
                    : new Vector3(Range(rng, 1.5f, 6), Range(rng, 1.1f, 2.6f), Range(rng, 1.5f, 6));
                var shade = Range(rng, 0.35f, 0.55f);
                var color = tall ? new Color(shade, shade * 0.9f, shade * 0.8f) : new Color(shade * 0.8f, shade * 0.6f, shade * 0.4f);
                var block = Block(root.transform, new Vector3(pos.x, size.y / 2, pos.z), size, color);
                block.transform.rotation = Quaternion.Euler(0, Range(rng, 0, 90), 0);
                placed++;
            }

            if (!Application.isBatchMode)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.transform.SetParent(root.transform, false);
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            }
            return arena;
        }

        public void Destroy() => Object.Destroy(Root);

        /// <summary>Rotación (yaw) para mirar al centro desde un punto.</summary>
        public static float YawTowardsCenter(Vector3 from) =>
            Quaternion.LookRotation(new Vector3(-from.x, 0, -from.z)).eulerAngles.y;

        private static GameObject Block(Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Block";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            if (!Application.isBatchMode) go.GetComponent<Renderer>().material = Visuals.Lit(color);
            return go;
        }

        private static bool NearSpawn(List<Vector3> spawns, Vector3 pos, float distance)
        {
            foreach (var s in spawns)
                if (Vector3.Distance(new Vector3(s.x, 0, s.z), pos) < distance) return true;
            return false;
        }

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
