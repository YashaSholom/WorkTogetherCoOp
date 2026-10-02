using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CoopPrototype.Editor
{
    /// <summary>
    /// Multiplayer Play Mode keeps each additional player as a clone in Library/VP/mppm*, whose Assets, ProjectSettings and
    /// Packages entries are Windows junctions/links back to the main project. When the project folder is moved, old clones
    /// keep pointing at the former location and that player never starts. This tool lists the link targets and repoints
    /// stale ones at the current project. Windows only; run it with the clone's editor closed.
    /// </summary>
    public static class MppmCloneRepair
    {
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
        static string VpRoot => Path.Combine(ProjectRoot, "Library", "VP");

        static string Cmd(string arguments, string workingDirectory)
        {
            var info = new ProcessStartInfo("cmd.exe", "/c " + arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            return output;
        }
        /// <summary>(link name, kind, target) for every junction/symlink directly inside a folder.</summary>
        static (string name, string kind, string target)[] Links(string folder)
        {
            var output = Cmd("dir /AL", folder);
            return Regex.Matches(output, @"<(JUNCTION|SYMLINKD|SYMLINK)>\s+(.+?)\s+\[(.+?)\]").Cast<Match>()
                .Select(m => (m.Groups[2].Value.Trim(), m.Groups[1].Value, m.Groups[3].Value.Trim().Replace(@"\\?\", ""))).ToArray();
        }
        static string[] Clones() => Directory.Exists(VpRoot) ? Directory.GetDirectories(VpRoot, "mppm*") : Array.Empty<string>();
        /// <summary>A link is stale when it targets a folder outside the current project (or one that no longer exists).</summary>
        static bool Stale(string target) => !target.StartsWith(ProjectRoot, StringComparison.OrdinalIgnoreCase) || !(Directory.Exists(target) || File.Exists(target));

        /// <summary>Which processes hold a UDP/TCP port (e.g. the game's 7777), with their names. Windows only.</summary>
        [MenuItem("Coop Prototype/Multiplayer Play Mode/Who Uses Port 7777")]
        public static string PortOwners() => PortOwners(7777);
        public static string PortOwners(int port)
        {
            var sb = new StringBuilder();
            var lines = Cmd("netstat -ano", ProjectRoot).Split('\n').Where(l => Regex.IsMatch(l, $@":{port}\s")).Select(l => l.Trim()).ToArray();
            if (lines.Length == 0) return $"Nothing is using port {port}.";
            foreach (var line in lines)
            {
                var pid = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last();
                var task = Cmd($"tasklist /FI \"PID eq {pid}\" /FO CSV /NH", ProjectRoot).Trim();
                var path = Cmd($"wmic process where processid={pid} get CommandLine /value", ProjectRoot).Trim();
                sb.AppendLine(line + "\n   " + task + "\n   " + path);
            }
            sb.AppendLine("This editor's process id: " + Process.GetCurrentProcess().Id);
            Debug.Log("[MPPM] Port " + port + ":\n" + sb);
            return sb.ToString();
        }

        [MenuItem("Coop Prototype/Multiplayer Play Mode/List Clone Links")]
        public static string List()
        {
            var sb = new StringBuilder("Project: " + ProjectRoot + "\n");
            foreach (var clone in Clones()) sb.AppendLine(Path.GetFileName(clone) + "/Packages: " + Regex.Replace(Cmd("dir /A /B", Path.Combine(clone, "Packages")), @"\s+", " ").Trim());
            foreach (var clone in Clones())
                foreach (var folder in new[] { clone, Path.Combine(clone, "Packages") }.Where(Directory.Exists))
                    foreach (var (name, kind, target) in Links(folder))
                        sb.AppendLine($"{Path.GetFileName(clone)}{(folder == clone ? "" : "/Packages")}/{name}  {kind} -> {target}  {(Stale(target) ? "STALE" : "ok")}");
            Debug.Log("[MPPM] " + sb);
            return sb.ToString();
        }

        /// <summary>Repoints stale clone links at the matching path in the current project. Only links are replaced; no files are deleted.</summary>
        [MenuItem("Coop Prototype/Multiplayer Play Mode/Repair Stale Clone Links")]
        public static string Repair()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) return "Windows only.";
            var sb = new StringBuilder();
            foreach (var clone in Clones())
                foreach (var folder in new[] { clone, Path.Combine(clone, "Packages") }.Where(Directory.Exists))
                {
                    string relative = folder == clone ? "" : "Packages";
                    foreach (var (name, kind, target) in Links(folder).Where(l => Stale(l.target)))
                    {
                        string desired = Path.Combine(ProjectRoot, relative, name);
                        if (!Directory.Exists(desired) && !File.Exists(desired)) { sb.AppendLine($"SKIP {name}: no {desired} in this project"); continue; }
                        bool directory = Directory.Exists(desired);
                        // rmdir/del on a link removes the link only, never the folder it points to.
                        string remove = directory ? $"rmdir \"{name}\"" : $"del \"{name}\"";
                        string create = directory ? $"mklink /J \"{name}\" \"{desired}\"" : $"mklink /H \"{name}\" \"{desired}\"";
                        string result = Cmd(remove + " && " + create, folder).Trim();
                        sb.AppendLine($"{Path.GetFileName(clone)}/{relative}{(relative.Length > 0 ? "/" : "")}{name}: {target} -> {desired}  ({result})");
                    }
                }
            string report = sb.Length == 0 ? "No stale clone links." : sb.ToString();
            Debug.Log("[MPPM] Repair:\n" + report);
            return report;
        }
    }
}
