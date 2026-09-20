using System;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using SM64Lib;
using SM64Lib.Patching;


[HarmonyPatch(typeof(PatchingManager), "RunArmips")]
public static class ArmipsPatch {
    // Prefix form since the function returns
    [HarmonyPrefix]
    public static bool Prefix(
        // Patches the armips assembler to capture errors.
        // This unintentionally also captures the output from assembly tweaks.
        string script,
        string filePath,
        string rootPath)
    {
        string createText =
$@".Open ""{filePath}"", 0
.n64
{script}
.Close";

        string tmpAsmFile = Path.GetTempFileName();

        try {
            File.WriteAllText(tmpAsmFile, createText);

            string armipsPath =
                FilePathsConfiguration
                    .DefaultConfiguration
                    .Files["armips.exe"];

            var psi = new ProcessStartInfo {
                FileName = armipsPath,
                Arguments = $"-root \"{rootPath}\" \"{tmpAsmFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var p = new Process()) {
                p.StartInfo = psi;
                p.Start();

                Plugin.ClearOutput();

                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();

                p.WaitForExit();

                string stdout = stdoutTask.Result;
                string stderr = stderrTask.Result;

                if (!string.IsNullOrEmpty(stdout)) {
                    Plugin.AppendOutput(stdout);
                }

                if (!string.IsNullOrEmpty(stderr)) {
                    Plugin.AppendOutput(stderr);
                }

                if (p.ExitCode != 0) {
                    Plugin.AppendOutput(
                        Environment.NewLine +
                        $"armips exited with code {p.ExitCode}" +
                        Environment.NewLine);
                } else {
                    Plugin.AppendOutput(
                        Environment.NewLine +
                        "Assembly completed succesfully." +
                        Environment.NewLine
                    );
                }
            }
        }
        finally {
            File.Delete(tmpAsmFile);
        }

        return false;
    }
}