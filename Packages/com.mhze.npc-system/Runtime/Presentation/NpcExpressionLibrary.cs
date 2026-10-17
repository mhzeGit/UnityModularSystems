using System;
using UnityEngine;

namespace ModularNPC
{
    [CreateAssetMenu(menuName = "Modular NPC/Expression Library")]
    public sealed class NpcExpressionLibrary : ScriptableObject
    {
        [Serializable]
        public struct BonePose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable]
        public sealed class Pose
        {
            public string id;
            public BonePose[] bones = Array.Empty<BonePose>();
        }
        [Tooltip("Paths relative to the NPC's Animator. Include only facial bones, never the head, neck or body.")]
        public string[] bonePaths = Array.Empty<string>();
        public BonePose[] neutral = Array.Empty<BonePose>();
        public Pose[] poses = Array.Empty<Pose>();

        public Pose Find(string id) => Array.Find(poses, p => p != null && p.id == id);
    }
}
