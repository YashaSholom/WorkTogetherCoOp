using System;
using System.IO;
using System.Linq;
using System.Text;
using CoopPrototype.Checkpoint;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>Read-only checks of authored station art and live readouts; never rebuilds content.</summary>
    public static class StationArtValidation
    {
        public static string Run()
        {
            var report = new StringBuilder();
            void Check(bool ok, string description)
            { report.AppendLine((ok ? "PASS: " : "FAIL: ") + description); }
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            Check(SceneManager.GetActiveScene().name == "GameWorld", "GameWorld loaded");
            var root = GameObject.Find("Space Checkpoint/Production art pass");
            Check(root != null, "Saved production art hierarchy present");
            Check(session != null && session.documentSlots.Length == 6 && session.documentSlots.All(s => s != null), "Six document sockets retained");
            Check(session != null && session.packagePrefab != null && session.vehicleView != null && session.holdingCell != null, "Existing cargo, vehicle and detention references retained");
            var screen = Object.FindFirstObjectByType<StationTerminalReadout>();
            Check(screen != null && screen.session == session && screen.display != null, "Terminal screen wired to replicated session");
            if (screen != null && screen.display != null) report.AppendLine("READOUT: " + screen.display.text.Replace('\n', '|'));
            if (Application.isPlaying && session != null && session.IsSpawned && screen != null)
                Check(screen.display.text.Contains(session.Phase.Value.ToString().ToUpperInvariant()), "Live world screen matches current visit phase");
            var shop = Object.FindFirstObjectByType<ShopTerminal>();
            Check(shop != null && shop.dispenser != null && shop.offers.Length > 0, "Supply kiosk retains real shop and dispenser");
            var scanner = Object.FindFirstObjectByType<ContrabandScanner>();
            Check(scanner != null && scanner.display != null && scanner.slots.All(s => s != null), "X-ray screen and scanner sockets retained");
            var cargo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CoopPrototype/Prefabs/CargoPackage.prefab").GetComponent<CargoPackage>();
            Check(cargo.xrayContents != null && cargo.xrayContraband != null && cargo.xrayContents.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled), "Cargo X-ray contents preserved");
            Check(cargo.transform.Find("Imported freight case") != null && cargo.tinted.Length == 1 && cargo.tinted[0] != null, "Imported pickup case and per-case colour seal wired");
            int missingScripts = 0;
            foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in sceneRoot.GetComponentsInChildren<Transform>()) missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            Check(missingScripts == 0, "No missing scripts on active GameWorld objects");
            foreach (string file in new[] { "StratifiedSandstone", "CanyonSky", "WaterfallVeil" })
            {
                // Load the authored asset explicitly: Shader.Find only sees shaders already registered in this editor process.
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/CoopPrototype/Art/Checkpoint/" + file + ".shader");
                bool error = shader != null && ShaderUtil.ShaderHasError(shader);
                Check(shader != null && shader.isSupported && !error, file + " compiles and is supported");
                report.AppendLine($"SHADER: {file} found={shader != null} supported={(shader != null && shader.isSupported)} errors={error}");
                if (error) foreach (var message in ShaderUtil.GetShaderMessages(shader)) report.AppendLine(message.message);
            }
            Check(root == null || root.GetComponentsInChildren<Collider>().All(c => !c.transform.IsChildOf(root.transform.Find("Velora canyon vista"))), "Distant vista cannot obstruct players or visitor route");
            if (root != null) report.AppendLine("Active art renderers=" + root.GetComponentsInChildren<Renderer>().Length);
            Directory.CreateDirectory("TestResults/ArtAdvance");
            File.WriteAllText("TestResults/ArtAdvance/" + (Application.isPlaying ? "Runtime-" : "EditMode-") + "validation.txt", report.ToString());
            if (report.ToString().Contains("FAIL:")) throw new InvalidOperationException(report.ToString());
            return report.ToString();
        }
    }
}
