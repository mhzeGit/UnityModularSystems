// ScriptableObject that stores all config data for a pickable item: name, shop info, whether it is currently pickable, position/rotation offsets when held, hand offset, and hold pressure/type.

using UnityEngine;

[CreateAssetMenu(fileName = "NewPickableItemData", menuName = "ScriptableObjects/PickableItemData")]
public class PickableItemData : ScriptableObject
{
    public string ItemName;

    [Header("Shop Data")]
    [TextArea]
    public string ItemDescription = "This is an Item!";
    public int ItemCost = 100;
    public Sprite ItemIcon;
    public GameObject ItemPrefab;

    [Header("Pickup Settings")]
    public bool IsPickable;
    public Vector3 ItemLocationOffset;
    public Vector3 ItemRotationOffset;
    public Vector3 HandLocationOffset;
    public float HandHoldPressure;
    public float HandHoldTypeIndex;
}