/// <summary>
/// Contract for a state in the overworld enemy FSM.
///
/// State instances are created once in OverworldEnemyController.Awake() and
/// reused for the lifetime of the enemy — never allocated per-tick.
/// All per-frame work happens in Tick(); transient setup goes in Enter() and
/// teardown in Exit() (cleanup of timers, cone tint, alert bubbles, etc.).
///
/// The owning enemy is passed in on every call rather than stored on the state,
/// so a single state instance is safe to share if we ever pool enemies — though
/// today each enemy still owns its own instances.
/// </summary>
public interface IEnemyState
{
    void Enter(OverworldEnemyController enemy);
    void Tick (OverworldEnemyController enemy);
    void Exit (OverworldEnemyController enemy);
}
