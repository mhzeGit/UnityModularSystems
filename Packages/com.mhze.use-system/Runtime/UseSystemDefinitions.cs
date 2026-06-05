using UnityEngine;

namespace MHZE.UseSystem
{
    [CreateAssetMenu(fileName = "UseSystemDefinitions", menuName = "MHZE/Use System/Definitions")]
    public class UseSystemDefinitions : ScriptableObject
    {
        public string[] toolNames;
        public string[] targetNames;
    }
}
