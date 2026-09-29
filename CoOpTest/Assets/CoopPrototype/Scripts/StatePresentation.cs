using UnityEngine;

namespace CoopPrototype
{
    public class StatePresentation : MonoBehaviour
    {
        public Light[] lights;
        public GameObject[] activeObjects;
        public Transform movingPart;
        public Vector3 offLocalPosition;
        public Vector3 onLocalPosition;
        public void Apply(bool value)
        {
            foreach (var lightSource in lights) if (lightSource != null) lightSource.enabled = value;
            foreach (var obj in activeObjects) if (obj != null) obj.SetActive(value);
            if (movingPart != null) movingPart.localPosition = value ? onLocalPosition : offLocalPosition;
        }
    }
}
