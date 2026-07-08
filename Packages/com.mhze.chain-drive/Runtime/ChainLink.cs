using UnityEngine;

namespace MHZE.ChainDrive
{
    [AddComponentMenu("Mechanical/Chain Link")]
    public class ChainLink : MonoBehaviour
    {
        public int index;
        public ChainDriveConstraint chainConstraint;
    }
}
