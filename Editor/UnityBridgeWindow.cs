using UnityEditor;
using UnityEngine;

namespace VolxGames.UnityBridge.Editor
{
    internal sealed class UnityBridgeWindow : EditorWindow
    {
        [MenuItem("Tools/Unity Bridge/Settings Window")]
        public static void ShowWindow()
        {
            GetWindow<UnityBridgeWindow>("Unity Bridge");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity Editor MCP Bridge", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var autoStart = EditorGUILayout.Toggle("Auto Start", UnityBridgeServer.AutoStart);
            if (autoStart != UnityBridgeServer.AutoStart)
            {
                UnityBridgeServer.AutoStart = autoStart;
            }

            EditorGUI.BeginDisabledGroup(UnityBridgeServer.IsRunning);
            var port = EditorGUILayout.IntField("Port", UnityBridgeServer.Port);
            if (port > 0 && port != UnityBridgeServer.Port)
            {
                UnityBridgeServer.Port = port;
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                UnityBridgeServer.IsRunning
                    ? $"Bridge is listening on http://127.0.0.1:{UnityBridgeServer.Port}/"
                    : "Bridge is stopped.",
                MessageType.Info);

            if (!UnityBridgeServer.IsRunning)
            {
                if (GUILayout.Button("Start Bridge"))
                {
                    UnityBridgeServer.Start();
                }
            }
            else
            {
                if (GUILayout.Button("Stop Bridge"))
                {
                    UnityBridgeServer.Stop();
                }
            }
        }
    }
}
