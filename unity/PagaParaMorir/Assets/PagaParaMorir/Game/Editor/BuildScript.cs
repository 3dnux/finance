using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PagaParaMorir.Game.Editor
{
    /// <summary>
    /// Compilaciones del juego, desde el menú o desde la línea de comandos / GitHub Actions:
    /// <c>Unity -batchmode -projectPath unity/PagaParaMorir -executeMethod PagaParaMorir.Game.Editor.BuildScript.BuildAndroidFromCommandLine</c>
    /// </summary>
    public static class BuildScript
    {
        public const string PackageId = "gg.pagaparamorir.juego";
        private const string DefaultApkPath = "Builds/Android/PagaParaMorir.apk";
        private const string DefaultServerPath = "Builds/LinuxServer/PagaParaMorir.x86_64";

        [MenuItem("Paga para Morir/Compilar APK de Android")]
        public static void BuildAndroidMenu()
        {
            if (BuildAndroid(DefaultApkPath))
                EditorUtility.RevealInFinder(DefaultApkPath);
        }

        [MenuItem("Paga para Morir/Compilar servidor dedicado (Linux)")]
        public static void BuildLinuxServerMenu()
        {
            if (BuildLinuxServer(DefaultServerPath))
                EditorUtility.RevealInFinder(DefaultServerPath);
        }

        /// <summary>Para GameCI (<c>buildMethod</c>) y <c>-executeMethod</c>. Sale con código 0 si compiló.</summary>
        public static void BuildAndroidFromCommandLine()
        {
            var path = Arg("-customBuildPath") ?? DefaultApkPath;
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "PagaParaMorir.apk");
            EditorApplication.Exit(BuildAndroid(path) ? 0 : 1);
        }

        public static void BuildLinuxServerFromCommandLine()
        {
            var path = Arg("-customBuildPath") ?? DefaultServerPath;
            EditorApplication.Exit(BuildLinuxServer(path) ? 0 : 1);
        }

        public static bool BuildAndroid(string apkPath)
        {
            SceneSetup.EnsureMainScene();
            ConfigureCommon();
            ConfigureAndroid();
            return Build(new BuildPlayerOptions
            {
                scenes = new[] { SceneSetup.MainScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }

        public static bool BuildLinuxServer(string path)
        {
            SceneSetup.EnsureMainScene();
            ConfigureCommon();
            return Build(new BuildPlayerOptions
            {
                scenes = new[] { SceneSetup.MainScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None,
            });
        }

        private static bool Build(BuildPlayerOptions options)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(options.locationPathName));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] {options.target} listo: {options.locationPathName} " +
                          $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalMinutes:0.0} min)");
                return true;
            }
            Debug.LogError($"[Build] {options.target} falló: {summary.result}, {summary.totalErrors} errores. Revisa la consola.");
            return false;
        }

        /// <summary>Ajustes del proyecto que necesitan todas las plataformas (también se aplican al crear la escena).</summary>
        public static void ConfigureCommon()
        {
            PlayerSettings.companyName = "Paga para Morir";
            PlayerSettings.productName = "Paga para Morir";
            // Unity bloquea http:// por defecto; el backend y el RPC local de pruebas lo usan.
            // Cuando el backend tenga HTTPS, cambiar a DevelopmentOnly o NotAllowed.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            var version = Arg("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
        }

        private static void ConfigureAndroid()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            // IL2CPP + ARM64: obligatorio para Google Play y para la mayoría de teléfonos actuales.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // Habla con Solana, el backend y el servidor de la partida.
            PlayerSettings.Android.forceInternetPermission = true;

            // Shooter en horizontal.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // APK (no AAB): se instala directo en el teléfono.
            EditorUserBuildSettings.buildAppBundle = false;

            if (int.TryParse(Arg("-androidVersionCode"), out var versionCode) && versionCode > 0)
                PlayerSettings.Android.bundleVersionCode = versionCode;

            // Firma de release si GameCI la pasa; si no, Unity firma con la llave de depuración
            // (sirve para instalar a mano, no para Google Play).
            var keystore = Arg("-androidKeystoreName");
            if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Arg("-androidKeystorePass") ?? "";
                PlayerSettings.Android.keyaliasName = Arg("-androidKeyaliasName") ?? "";
                PlayerSettings.Android.keyaliasPass = Arg("-androidKeyaliasPass") ?? "";
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
            }
        }

        /// <summary>Valor de un argumento de la línea de comandos (<c>-nombre valor</c>), o null.</summary>
        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
