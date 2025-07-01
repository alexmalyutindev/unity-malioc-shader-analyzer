using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaliOC.Core;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Shader = UnityEngine.Shader;

namespace MaliOC.Editor
{
    public class ShaderAnalyzerTool : EditorWindow
    {
        [SerializeField] private Shader _shader;
        [SerializeField] private Report _report;

        private Vector2 _definesScrollPosition;
        private Vector2 _mainScrollPosition;

        private UnityEditor.Editor _inspector;
        private EditorCoroutine _maliOCProcessHandle;
        private GUIStyle _fieldLabelStyle;

        [SerializeField] private SerializableDictionary<string, bool> _keywords = new();

        [MenuItem("Tools/" + nameof(ShaderAnalyzerTool))]
        public static void Create()
        {
            if (Selection.activeObject is Shader shader)
            {
                Open(shader);
            }
            else
            {
                Open();
            }
        }

        public static void Open(Shader target = null)
        {
            var window = CreateWindow<ShaderAnalyzerTool>();
            window.SelectShader(target);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is Shader shader)
            {
                SelectShader(shader);
            }
        }

        private void SelectShader(Shader shader)
        {
            _shader = shader;
            UnityEditor.Editor.CreateCachedEditor(_shader, typeof(ShaderInspector), ref _inspector);
            Repaint();

            Debug.Log(_shader);
        }

        private void OnGUI()
        {
            if (_shader == null) return;
            if (_inspector is not ShaderInspector shaderInspector) return;

            shaderInspector.DrawHeader();

            using var scrollViewScope = new GUILayout.ScrollViewScope(_mainScrollPosition);
            _mainScrollPosition = scrollViewScope.scrollPosition;

            // Keywords selector
            using (new EditorGUILayout.VerticalScope(EditorStyles.frameBox))
            {
                var keywordsLabel = new GUIContent("Keywords");
                var dropDownRect = GUILayoutUtility.GetRect(keywordsLabel, EditorStyles.dropDownList);
                if (EditorGUI.DropdownButton(dropDownRect, keywordsLabel, FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var localKeyword in _shader.keywordSpace.keywords)
                    {
                        if (!_keywords.TryGetValue(localKeyword.name, out var enabled))
                        {
                            _keywords[localKeyword.name] = false;
                        }

                        menu.AddItem(
                            new GUIContent(localKeyword.name),
                            enabled,
                            data =>
                            {
                                var kw = (LocalKeyword)data;
                                _keywords[kw.name] = !_keywords[kw.name];
                            },
                            localKeyword
                        );
                    }

                    menu.DropDown(dropDownRect);
                }

                foreach (var localKeyword in _shader.keywordSpace.keywords)
                {
                    var enabled = _keywords.GetValueOrDefault(localKeyword.name, false);
                    if (enabled)
                    {
                        if (!GUILayout.Toggle(true, localKeyword.name))
                        {
                            _keywords[localKeyword.name] = false;
                        }
                    }
                }
            }

            if (_maliOCProcessHandle == null)
            {
                if (GUILayout.Button("Analyze"))
                {
                    var activeKeywords = _shader.keywordSpace.keywords
                        .Where(keyword => _keywords.GetValueOrDefault(keyword.name))
                        .Select(keyword => keyword.name)
                        .ToArray();
                    AnalyzeShader(_shader, activeKeywords, 0, 0);
                }
            }
            else
            {
                if (GUILayout.Button("Cancel"))
                {
                    EditorCoroutineUtility.StopCoroutine(_maliOCProcessHandle);
                    _maliOCProcessHandle = null;
                    Debug.Log("Canceled");
                }
            }

            if (_report != null)
            {
                DrawReport(_report);
            }
        }

        private void DrawReport(Report report)
        {
            // Header
            {
                GUILayout.BeginHorizontal();
                var v = report.Producer.Version;
                EditorGUILayout.LabelField(
                    $"{report.Producer.Name} v{v[0]}.{v[1]}.{v[2]} Build ({report.Producer.Build})");
                if (EditorGUILayout.LinkButton("Documentation"))
                {
                    Application.OpenURL(report.Producer.Documentation.AbsoluteUri);
                }

                GUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(5);

            _fieldLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                padding = new RectOffset(20, 0, 0, 0),
            };

            foreach (var shader in report.Shaders)
            {
                EditorGUILayout.LabelField("Shader file:", shader.Filename, EditorStyles.wordWrappedLabel);

                EditorGUILayout.LabelField("Configuration:");
                {
                    using (new GUILayout.VerticalScope(EditorStyles.frameBox))
                    {
                        EditorGUILayout.LabelField("Hardware:", $"{shader.Hardware.Core} {shader.Hardware.Revision}");
                        EditorGUILayout.LabelField("Architecture:", shader.Hardware.Architecture);
                        EditorGUILayout.LabelField("Driver:", shader.Driver);
                        EditorGUILayout.LabelField("Shader type:", $"{shader.Shader.Api} {shader.Shader.Type}");
                    }
                }

                GUILayout.Space(5);

                if (shader.Variants != null)
                {
                    foreach (var variant in shader.Variants)
                    {
                        EditorGUILayout.LabelField("Shader:", variant.Name);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.frameBox, GUILayout.Width(300)))
                        {
                            foreach (var variantProperty in variant.Properties)
                            {
                                GUIContent valueContent;
                                if (variantProperty.Value.Bool.HasValue)
                                {
                                    valueContent = new GUIContent(variantProperty.Value.Bool.Value ? "True" : "False");
                                }
                                else
                                {
                                    var format = variantProperty.Name switch
                                    {
                                        "fp16_arithmetic" or "thread_occupancy" or "fp16_idle_lanes" => @"0\%",
                                        _ => "",
                                    };
                                    valueContent = new GUIContent(variantProperty.Value.Integer?.ToString(format));
                                }

                                EditorGUILayout.LabelField(
                                    new GUIContent(variantProperty.DisplayName, variantProperty.Description),
                                    valueContent,
                                    _fieldLabelStyle
                                );
                            }
                        }

                        DrawPerformanceTable(variant.Performance, shader.Hardware);
                    }
                }

                EditorGUILayout.Space(5);

                if (shader.Properties != null)
                {
                    EditorGUILayout.LabelField("Shader properties:");
                    using (new GUILayout.VerticalScope(EditorStyles.frameBox, GUILayout.Width(300)))
                    {
                        foreach (var shaderProperty in shader.Properties)
                        {
                            EditorGUILayout.LabelField(
                                new GUIContent(shaderProperty.DisplayName, shaderProperty.Description),
                                new GUIContent(shaderProperty.Value ? "True" : "False"),
                                _fieldLabelStyle
                            );
                        }
                    }
                }

                if (shader.Errors != null)
                {
                    foreach (var error in shader.Errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }
                }

                if (shader.Notes != null)
                {
                    foreach (var note in shader.Notes)
                    {
                        EditorGUILayout.HelpBox(note, MessageType.Warning);
                    }
                }

                if (shader.Warnings != null)
                {
                    foreach (var warning in shader.Warnings)
                    {
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    }
                }
            }
        }

        private static void DrawPerformanceTable(Performance performance, Hardware hardware)
        {
            GUILayout.Label("Performance:");
            using (new GUILayout.HorizontalScope(EditorStyles.frameBox))
            {
                EditorGUILayout.BeginVertical();
                GUILayout.Label("Cycles");
                GUILayout.Label("Total");
                GUILayout.Label("Shortest");
                GUILayout.Label("Longest");
                EditorGUILayout.EndVertical();

                for (var i = 0; i < performance.Pipelines.Length; i++)
                {
                    EditorGUILayout.BeginVertical();
                    var pipeline = performance.Pipelines[i];
                    var pipelineInfo = hardware.Pipelines.First(p => p.Name == pipeline);
                    GUILayout.Label(new GUIContent(pipelineInfo.DisplayName, pipelineInfo.Description));

                    GUILayout.Label(performance.TotalCycles.CycleCount[i].ToString("F2"));
                    GUILayout.Label(performance.ShortestPathCycles.CycleCount[i].ToString("F2"));
                    GUILayout.Label(performance.LongestPathCycles.CycleCount[i].ToString("F2"));
                    EditorGUILayout.EndVertical();
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("Bound By");

                    EditorGUILayout.BeginHorizontal();
                    foreach (var boundPipeline in performance.TotalCycles.BoundPipelines)
                    {
                        var boundName = hardware.Pipelines.First(p => p.Name == boundPipeline).DisplayName;
                        GUILayout.Label(boundName);
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    foreach (var boundPipeline in performance.ShortestPathCycles.BoundPipelines)
                    {
                        var boundName = hardware.Pipelines.First(p => p.Name == boundPipeline).DisplayName;
                        GUILayout.Label(boundName);
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    foreach (var boundPipeline in performance.LongestPathCycles.BoundPipelines)
                    {
                        var boundName = hardware.Pipelines.First(p => p.Name == boundPipeline).DisplayName;
                        GUILayout.Label(boundName);
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void AnalyzeShader(Shader sourceShader, string[] keywords, int subshaderIndex, int passIndex)
        {
            // TODO: Configure Shader variant!
            // - Target API
            // - Shader Stage
            // - Graphics Tier
            var shaderData = new ShaderData(sourceShader);
            var compileInfo = shaderData
                .GetSubshader(subshaderIndex)
                .GetPass(passIndex)
                .CompileVariant(
                    ShaderType.Fragment,
                    keywords,
                    ShaderCompilerPlatform.Vulkan,
                    BuildTarget.Android,
                    GraphicsTier.Tier1,
                    forExternalTool: true
                );

            var shaderPassData = compileInfo.ShaderData;

            var tempPath = $"Temp/{sourceShader.name.Replace('/', '_')}.spv.frag";
            File.WriteAllBytes(tempPath, shaderPassData);

            _maliOCProcessHandle = EditorCoroutineUtility.StartCoroutine(
                MakePassReport(tempPath, json =>
                {
                    try
                    {
                        _report = Newtonsoft.Json.JsonConvert.DeserializeObject<Report>(json);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    Repaint();
                    _maliOCProcessHandle = null;
                }),
                this
            );
        }

        private IEnumerator MakePassReport(string shaderPassFile, Action<string> onComplete)
        {
            // TODO: Use vulkan only if target API is Vulkan!
            // TODO: Add OpenGLES support!

            yield return MaliOfflineCompiler.Run(
                shaderPassFile,
                new[] { "--vulkan", "--format json" },
                (output, error) =>
                {
                    Debug.Log(output);
                    if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
                    onComplete?.Invoke(output);
                }
            );

            yield return MaliOfflineCompiler.Run(
                shaderPassFile,
                new[] { "--vulkan" },
                (output, error) =>
                {
                    Debug.Log(output);
                    if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
                }
            );
        }
    }
}