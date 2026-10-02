using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public sealed class PortalMotion : MonoBehaviour
    {
        public float degreesPerSecond = 16;
        void Update() => transform.Rotate(0, 0, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
