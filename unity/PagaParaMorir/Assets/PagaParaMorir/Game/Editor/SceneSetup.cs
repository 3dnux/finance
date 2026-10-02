using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PagaParaMorir.Game.Editor
{
    /// <summary>Menú "Paga para Morir" del editor.</summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/PagaParaMorir/Scenes/Main.unity";

        [MenuItem("Paga para Morir/Crear escena principal")]
        public static void CreateMainScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Paga para Morir", "La escena Main ya existe. ¿Reemplazarla?", "Reemplazar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
            new GameObject("PagaParaMorir").AddComponent<PagaParaMorirApp>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Escena creada en " + ScenePath + " y agregada a Build Settings.");
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
