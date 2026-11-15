using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cria a cena principal (vazia: o jogo inteiro é gerado por código ao dar Play),
/// coloca-a no Build Settings e adiciona o menu "Corrida" para gerar o executável.
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    public const string ScenePath = "Assets/Scenes/Interlagos.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureScene();
            if (!Application.isBatchMode && string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(ScenePath);
        };
    }

    public static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length != 1 || scenes[0].path != ScenePath)
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Corrida em Interlagos";
        PlayerSettings.companyName = "Codiguins";
    }

    [MenuItem("Corrida/Abrir cena de Interlagos")]
    public static void OpenScene()
    {
        EnsureScene();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Corrida/Gerar executável (Windows)")]
    public static void BuildWindows()
    {
        EnsureScene();
        MaterialLibrary.BuildAll();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/CorridaInterlagos.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[Corrida] Build: " + report.summary.result + " -> " + Path.GetFullPath("Build/CorridaInterlagos.exe"));
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
