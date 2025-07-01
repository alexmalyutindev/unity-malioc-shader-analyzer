using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEditor.Rendering;
using Debug = UnityEngine.Debug;

namespace MaliOC.Core
{
    public static class MaliOfflineCompiler
    {
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
            var args = $"{string.Join(' ', additionalArgs)} \"{inputFilePath}\"";
            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo("malioc", args)
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

            process.Start();
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