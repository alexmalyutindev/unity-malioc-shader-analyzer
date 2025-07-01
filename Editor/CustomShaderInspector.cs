using UnityEditor;
using UnityEngine;

namespace MaliOC.Editor
{
    [CustomEditor(typeof(Shader))]
    internal class CustomShaderInspector : ShaderInspector
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledGroupScope(false))
            {
                GUI.enabled = true;
                if (GUILayout.Button("Open in Shader Analyzer Tool"))
                {
                    ShaderAnalyzerTool.Open(target as Shader);
                }
                GUI.enabled = false;
            }
            base.OnInspectorGUI();
        }
    }
}