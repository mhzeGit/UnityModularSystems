//Made By MHZE

using System;

namespace MHZE.UseSystem
{
    [Obsolete("Replaced by string-based TargetId. Define target names in UseSystemDefinitions ScriptableObject instead.")]
    public enum UseTargetsName
    {
        Default,
        PlantableSoil,
        DiggableSoil,
        DiggedSoil,

        Door,
        Damagable,
        Rock,
        Bag,
        Crops,
    }
}
