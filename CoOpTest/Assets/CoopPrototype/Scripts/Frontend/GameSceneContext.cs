using UnityEngine;

namespace CoopPrototype.Frontend
{
    /// <summary>Scene-owned references injected into the session when its scene finishes loading.</summary>
    public sealed class GameSceneContext : MonoBehaviour
    {
        public Transform[] spawnPoints;
        public Camera loadingCamera;
    }
}
