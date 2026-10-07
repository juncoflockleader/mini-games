using System.IO;
using MiniGames.Breakout;
using MiniGames.SproutRun;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniGames.Editor
{
    public static class ProjectSetup
    {
        public const string LauncherPath = "Assets/MiniGames/Scenes/Launcher.unity";
        public const string BreakoutPath = "Assets/MiniGames/Scenes/Breakout.unity";
        public const string SproutRunPath = "Assets/MiniGames/Scenes/SproutRun.unity";
        static string[] ScenePaths => new[] { LauncherPath, BreakoutPath, SproutRunPath };

        [MenuItem("Mini Games/Create Missing Scenes")]
        public static void CreateMissingScenes()
        {
            Directory.CreateDirectory("Assets/MiniGames/Scenes");
            AssetDatabase.Refresh();
            if (!File.Exists(LauncherPath)) CreateScene<LauncherController>(LauncherPath, "Mini Games Launcher");
            if (!File.Exists(BreakoutPath)) CreateScene<BreakoutController>(BreakoutPath, "Breakout");
            if (!File.Exists(SproutRunPath)) CreateScene<RunnerController>(SproutRunPath, "Sprout Run");
            EditorBuildSettings.scenes = System.Array.ConvertAll(ScenePaths, path => new EditorBuildSettingsScene(path, true));
            PlayerSettings.companyName = "Junco";
            PlayerSettings.productName = "Mini Games";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(LauncherPath);
            Debug.Log("Mini Games: scenes and desktop settings are ready.");
        }

        static void CreateScene<T>(string path, string name) where T : MonoBehaviour
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ArcadeUi.Background;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject(name).AddComponent<T>();
            EditorSceneManager.SaveScene(scene, path);
        }

        [MenuItem("Mini Games/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/Mini Games.app");

        [MenuItem("Mini Games/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Mini Games.exe");

        static void Build(BuildTarget target, string output)
        {
            if (!File.Exists(LauncherPath) || !File.Exists(BreakoutPath) || !File.Exists(SproutRunPath)) CreateMissingScenes();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Build failed: " + report.summary.result);
            Debug.Log("Mini Games built: " + Path.GetFullPath(output));
        }
    }
}
