using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Windows;
using Debug = UnityEngine.Debug;

namespace MaliOC.Core
{
    public static class MaliOfflineCompiler
    {
        public static string MaliOCPath => EditorPrefs.GetString("mali-oc-path", "/Applications/Arm_Performance_Studio_2025.3/mali_offline_compiler/malioc");

        public enum TargetApi
        {
            Vulkan,
            GLES3x,
        }

        public enum ReportType
        {
            Basic,
            Json,
        }

        public static IEnumerator Run(
            string inputFilePath,
            ShaderType shaderStage,
            TargetApi targetApi,
            ReportType reportType,
            Action<string, string> onComplete
        )
        {
            string[] args = {
                targetApi switch
                {
                    TargetApi.Vulkan => "--vulkan",
                    _ => string.Empty,
                },
                shaderStage switch
                {
                    ShaderType.Vertex => "--vertex",
                    ShaderType.Fragment => "--fragment",
                    _ => string.Empty,
                },
                reportType switch
                {
                    ReportType.Json => "--format json",
                    _ => string.Empty,
                },
                "--detailed"
            };
            yield return Run(inputFilePath, args, onComplete);
        }

        public static IEnumerator Run(string inputFilePath, string[] additionalArgs, Action<string, string> onComplete)
        {
            var maliocPath = MaliOCPath;
            var args = $"{string.Join(' ', additionalArgs)} \"{inputFilePath}\"";

            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo(maliocPath, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            Debug.Log($"{process.StartInfo.FileName} {process.StartInfo.Arguments}");
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, dataEvent) => output.AppendLine(dataEvent.Data);
            process.ErrorDataReceived += (_, dataEvent) => error.AppendLine(dataEvent.Data);

            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                onComplete?.Invoke(string.Empty, "No MaliOC found! Please specify path to MaliOC!");
                yield break;
            }
            catch (Exception e)
            {
                onComplete?.Invoke(string.Empty, e.Message);
                yield break;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited)
            {
                yield return null;
            }

            var errorLog = error.ToString().Trim();
            onComplete?.Invoke(output.ToString(), errorLog);
        }
    }
}
