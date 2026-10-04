using System.IO;
using PagaParaMorir.Game.Match;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PagaParaMorir.Game.Editor
{
    /// <summary>Menú "Paga para Morir" del editor.</summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/PagaParaMorir/Scenes/Main.unity";
        private const string PrefabDir = "Assets/PagaParaMorir/Prefabs";

        [MenuItem("Paga para Morir/Crear escena principal")]
        public static void CreateMainScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Paga para Morir", "La escena Main ya existe. ¿Reemplazarla?", "Reemplazar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(PrefabDir);
            var playerPrefab = CreatePlayerPrefab();
            var matchPrefab = CreateMatchPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // La arena trae su propia luz.
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                Object.DestroyImmediate(light.gameObject);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.65f, 0.8f);
                camera.transform.SetPositionAndRotation(new Vector3(0, 60, -70), Quaternion.Euler(40, 0, 0));
            }

            var network = new GameObject("NetworkManager");
            var manager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = 30;
            var session = network.AddComponent<GameSession>();
            session.networkManager = manager;
            session.matchPrefab = matchPrefab;

            new GameObject("PagaParaMorir").AddComponent<PagaParaMorirApp>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Escena creada en " + ScenePath + " y agregada a Build Settings.");
        }

        /// <summary>Cápsula de 2 m con CharacterController; el pivote queda en su centro.</summary>
        private static GameObject CreatePlayerPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "NetworkPlayer";
            Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            var controller = go.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkPlayer>();
            return SavePrefab(go, "NetworkPlayer");
        }

        private static GameObject CreateMatchPrefab()
        {
            var go = new GameObject("NetworkMatch");
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchController>();
            return SavePrefab(go, "NetworkMatch");
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        [MenuItem("Paga para Morir/Borrar billetera guardada (solo pruebas)")]
        public static void DeleteSavedWallet()
        {
            if (!EditorUtility.DisplayDialog("Paga para Morir",
                    "Esto borra la billetera cifrada de este equipo. Sin sus 12 palabras, el USDC que tenga se pierde.",
                    "Borrar", "Cancelar"))
                return;
            PlayerPrefs.DeleteKey("EncryptedKeystore");
            PlayerPrefs.Save();
        }
    }
}
