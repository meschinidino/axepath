// Headless player builds: Unity -batchmode -projectPath . -executeMethod BuildScript.BuildOSX -quit
using UnityEditor;

public static class BuildScript
{
    // Same build, triggerable from the open editor (batchmode cannot
    // open a project the GUI editor already holds).
    [MenuItem("AxePath/Build OSX Player")]
    public static void BuildOSXMenu() { BuildOSX(); }

    public static void BuildOSX()
    {
        var scenes = new string[] { "Assets/Scenes/Main.unity" };
        var res = BuildPipeline.BuildPlayer(scenes, "Builds/AxePath.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        UnityEngine.Debug.Log("BUILDSCRIPT: result=" + res.summary.result + " errors=" + res.summary.totalErrors + " size=" + res.summary.totalSize);
    }
}
