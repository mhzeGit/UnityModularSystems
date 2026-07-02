using UnityEngine;

namespace MHZE.GearSystem
{
    public class GearMeshTrigger : MonoBehaviour
    {
        public GearItem gearItem;

        private void OnTriggerEnter(Collider other)
        {
            if (gearItem != null)
                gearItem.OnChildTriggerEnter(transform, other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (gearItem != null)
                gearItem.OnChildTriggerExit(transform, other);
        }
    }
}
