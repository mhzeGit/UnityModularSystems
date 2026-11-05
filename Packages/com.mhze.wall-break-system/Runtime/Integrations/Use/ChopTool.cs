using UnityEngine;
using MHZE.UseSystem;

namespace MHZE.WallBreak
{
    /// <summary>
    /// Sound for a held chopping tool (an axe): a whoosh the moment the swing starts. The tool itself is the Use System's
    /// UsableItemBase on the same object (tool id "Axe", impact delay = how far into the swing the blade lands); the thud
    /// of the blow belongs to the wall it lands on. Add it next to the UsableItemBase.
    /// </summary>
    [RequireComponent(typeof(UsableItemBase))]
    [AddComponentMenu("MHZE/Wall Break/Chop Tool")]
    public sealed class ChopTool : MonoBehaviour
    {
        [SerializeField] private AudioClip[] swingClips;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

        private UsableItemBase item;

        private void OnEnable()
        {
            item = GetComponent<UsableItemBase>();
            if (item != null) item.OnUsedItem += Swung;
        }

        private void OnDisable()
        {
            if (item != null) item.OnUsedItem -= Swung;
        }

        private void Swung(GameObject target, string targetId)
        {
            if (swingClips == null || swingClips.Length == 0) return;
            AudioClip clip = swingClips[Random.Range(0, swingClips.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }
    }
}
