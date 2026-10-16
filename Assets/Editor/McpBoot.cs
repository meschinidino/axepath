// Project boot helper: connects the Unity-MCP bridge on editor load so the
// local MCP server can drive this editor without any UI clicks.
// NOTE: uses a static bool (reset by every domain reload) on purpose. A
// SessionState once-per-session guard breaks on double-reload launches: the
// first reload queues the callback, the second reload wipes the queue, and
// the guard blocks re-registering. Static re-registers cleanly each reload.
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class McpBoot
{
    private static bool _registered;

    static McpBoot()
    {
        if (Application.isBatchMode) return;
        if (_registered) return;
        _registered = true;
        Debug.Log("MCPBOOT: ctor, queueing bridge start");
        EditorApplication.delayCall += StartBridge;
    }

    private static async void StartBridge()
    {
        try
        {
            bool ok = await MCPForUnity.Editor.Services.MCPServiceLocator.Bridge.StartAsync();
            Debug.Log("MCPBOOT: bridge start " + (ok ? "OK" : "FAILED"));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("MCPBOOT: bridge start threw: " + ex.Message);
        }
    }
}
