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
            var bridge = MCPForUnity.Editor.Services.MCPServiceLocator.Bridge;
            bool ok = await bridge.StartAsync();
            if (!ok)
            {
                // HTTP mode dials an external relay that usually isn't running.
                // Fall back to stdio, where Unity hosts the listener clients probe.
                var config = MCPForUnity.Editor.Services.EditorConfigurationCache.Instance;
                if (config.UseHttpTransport)
                {
                    Debug.Log("MCPBOOT: preferred transport failed, falling back to stdio");
                    config.SetUseHttpTransport(false);
                    ok = await bridge.StartAsync();
                }
            }
            Debug.Log("MCPBOOT: bridge start " + (ok ? "OK" : "FAILED"));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("MCPBOOT: bridge start threw: " + ex.Message);
        }
    }
}
