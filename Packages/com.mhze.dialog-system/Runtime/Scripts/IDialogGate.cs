// Implemented by any component that can veto starting a dialog until a condition is met, for example "the NPC has arrived at its waypoint and is waiting".

namespace MHZE.DialogSystem
{
/// <summary>
/// Optional gate for <see cref="DialogProximityTrigger"/>. While a gate is assigned, the trigger only
/// arms when <see cref="CanStartDialog"/> has been true at least once with the required actor present,
/// so a sequence can wait for a character to be in a particular state (arrived, sitting, waiting, ...).
/// </summary>
public interface IDialogGate
{
    bool CanStartDialog { get; }
}
}
