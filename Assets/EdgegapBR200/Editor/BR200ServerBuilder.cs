using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Edgegap.BR200.Editor
{
    /// <summary>
    /// Checks a BR200 project and builds its Linux dedicated server where the
    /// Edgegap Unity plugin expects it (Builds/EdgegapServer/ServerBuild).
    /// Touches no BR200 or Fusion code: Fusion settings are read by reflection.
    /// </summary>
    public static class BR200ServerBuilder
    {
        const string DefaultOutput = "Builds/EdgegapServer/ServerBuild";
        const string ScenesFolder = "/TPSBR/Scenes/";

        [MenuItem("Tools/Edgegap BR200/Check Project")]
        public static void CheckFromMenu()
        {
            var problems = Check(fix: false);
            if (problems.Count == 0)
                EditorUtility.DisplayDialog("Edgegap BR200", "The project is ready for a server build.", "OK");
            else
                EditorUtility.DisplayDialog("Edgegap BR200", string.Join("\n\n", problems), "OK");
        }

        [MenuItem("Tools/Edgegap BR200/Build Linux Server")]
        public static void BuildFromMenu()
        {
            var problems = Check(fix: true);
            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("Edgegap BR200", string.Join("\n\n", problems), "OK");
                return;
            }
            Build(DefaultOutput);
        }

        /// <summary>
        /// Unity CLI entry point:
        /// -executeMethod Edgegap.BR200.Editor.BR200ServerBuilder.BuildFromCommandLine [-edgegapOutput path]
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-edgegapOutput");
            string output = i >= 0 && i + 1 < args.Length ? args[i + 1] : DefaultOutput;

            var problems = Check(fix: true);
            foreach (string problem in problems)
                Debug.LogError($"[Edgegap BR200] {problem}");

            bool ok = problems.Count == 0 && Build(output);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Returns what blocks a working server build. With fix, applies the safe fixes.</summary>
        static List<string> Check(bool fix)
        {
            var problems = new List<string>();

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                problems.Add("Install the Linux Dedicated Server build support module in Unity Hub.");

            // BR200 and Fusion use unsafe code; the import fails without it.
            if (!PlayerSettings.allowUnsafeCode)
            {
                if (fix)
                {
                    PlayerSettings.allowUnsafeCode = true;
                    Debug.Log("[Edgegap BR200] Enabled Player Settings > Allow 'unsafe' Code.");
                }
                else
                {
                    problems.Add("Enable Edit > Project Settings > Player > Allow 'unsafe' Code.");
                }
            }

            var inBuild = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            string[] missing = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains(ScenesFolder) && !inBuild.Contains(p))
                .ToArray();
            if (inBuild.Count == 0)
                problems.Add("The build profile has no enabled scenes. Add the TPSBR/Scenes scenes, Loader first.");
            else if (missing.Length > 0)
                problems.Add("Add every TPSBR/Scenes scene to the build profile. Missing:\n" + string.Join("\n", missing));

            string appId = ReadSetting("Fusion.Photon.Realtime.PhotonAppSettings", "Global", "AppSettings", "AppIdFusion");
            if (appId == null)
                Debug.LogWarning("[Edgegap BR200] Could not read the Fusion App ID; check Tools > Fusion > Realtime Settings yourself.");
            else if (appId.Trim().Length == 0)
                problems.Add("Set your Fusion App ID in Tools > Fusion > Realtime Settings.");

            string peerMode = ReadSetting("Fusion.NetworkProjectConfig", "Global", "PeerMode");
            if (peerMode == null)
                Debug.LogWarning("[Edgegap BR200] Could not read Peer Mode; check Tools > Fusion > Network Project Config yourself.");
            else if (peerMode != "Single")
                problems.Add($"Set Peer Mode to Single in Tools > Fusion > Network Project Config (it is {peerMode}).");

            return problems;
        }

        static bool Build(string output)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            // Build the Server subtarget (headless, UNITY_SERVER defined), not a
            // plain Linux player, and check afterwards that Unity did.
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                locationPathName = output,
            });

            var subtarget = report.summary.GetSubtarget<StandaloneBuildSubtarget>();
            bool ok = report.summary.result == BuildResult.Succeeded && subtarget == StandaloneBuildSubtarget.Server;
            string backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server).ToString();

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "edgegap-build.txt"),
                $"unity={Application.unityVersion}\nsubtarget={subtarget}\nbackend={backend}\n" +
                $"result={report.summary.result}\nbuilt_utc={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\n");

            Debug.Log($"[Edgegap BR200] {report.summary.result}: {output} ({subtarget}, {backend}, " +
                      $"{report.summary.totalTime.TotalMinutes:0.0} min)");
            return ok;
        }

        /// <summary>Follows static/instance members by name; null when anything is missing.</summary>
        static string ReadSetting(string typeName, params string[] path)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(typeName, false))
                .FirstOrDefault(t => t != null);
            if (type == null) return null;

            object current = null;
            Type currentType = type;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (string name in path)
            {
                try
                {
                    PropertyInfo prop = currentType.GetProperty(name, flags);
                    FieldInfo field = prop == null ? currentType.GetField(name, flags) : null;
                    if (prop == null && field == null) return null;
                    current = prop != null ? prop.GetValue(current) : field.GetValue(current);
                }
                catch (Exception)
                {
                    return null;
                }
                if (current == null) return null;
                currentType = current.GetType();
            }
            return current.ToString();
        }
    }
}
