using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>
    /// With "Script Changes While Playing = Recompile And Continue Playing", a script reload during Play destroys Netcode's
    /// managed state without closing Unity Transport's native UDP socket. The port (7777) then stays bound by the editor
    /// until Unity restarts, and every later Host fails with "address is already in use". This guard closes all network
    /// transports right before an assembly reload in Play mode so the port is released. The session itself cannot
    /// survive a reload: stop Play and start again.
    /// </summary>
    [InitializeOnLoad]
    static class NetworkReloadGuard
    {
        static NetworkReloadGuard() => AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;

        static void BeforeReload()
        {
            if (!EditorApplication.isPlaying) return;
            int closed = 0;
            foreach (var transport in Object.FindObjectsByType<NetworkTransport>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try { transport.Shutdown(); closed++; }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (closed > 0)
                Debug.LogWarning($"[Coop] Scripts recompiled during Play: closed {closed} network transport(s) so the port is released. " +
                                 "The network session cannot survive a reload: stop Play and start again. " +
                                 "(Tip: Preferences > General > Script Changes While Playing > Recompile After Finished Playing.)");
        }
    }
}
