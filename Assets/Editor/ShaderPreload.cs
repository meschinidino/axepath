// One-shot: creates Resources materials so the player build includes the
// shaders we reference from code (Shader.Find returns null for stripped
// shaders). Run via AxePath menu or the MCP execute_menu_item tool.
using UnityEditor;
using UnityEngine;

public static class ShaderPreload
{
    [MenuItem("AxePath/Create Shader Preloads")]
    public static void Create()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        Make("PreloadStandard", "Standard");
        Make("PreloadUnlitColor", "Unlit/Color");
        Make("PreloadUnlitTransparent", "Unlit/Transparent");
        Make("PreloadPanoramic", "Skybox/Panoramic");
        AssetDatabase.Refresh();
        Debug.Log("SHADERPRELOAD: done");
    }

    private static void Make(string name, string shader)
    {
        string path = "Assets/Resources/" + name + ".mat";
        var m = new Material(Shader.Find(shader));
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) AssetDatabase.CreateAsset(m, path);
        else
        {
            existing.shader = Shader.Find(shader);
            EditorUtility.SetDirty(existing);
        }
    }
}
