using UnityEngine;

/// <summary>
/// Terminal state: the enemy has touched the player. Fires exactly one
/// <c>PlayerController.TriggerEncounter</c> call, then waits for the scene
/// to unload as GameController transitions to the Battle scene.
///
/// We piggy-back on the existing encounter pipeline:
///   PlayerController.TriggerEncounter → OnEncounterTriggered event
///     → GameController.OnEncounterTriggered → SceneManager.LoadScene(battle)
///
/// No new APIs are needed in battle code for this step. The "remember which
/// enemy triggered the battle so the registry can record it on win" hook is
/// added in step 5.
/// </summary>
public class AttackState : IEnemyState
{
    private bool triggered;

    public void Enter(OverworldEnemyController enemy)
    {
        triggered = false;
    }

    public void Tick(OverworldEnemyController enemy)
    {
        if (triggered) { enemy.ApplyGravityOnly(); return; }

        var data = enemy.AIData;
        if (data.EncounterToTrigger == null)
        {
            Debug.LogWarning(
                $"[OverworldEnemyController] '{enemy.name}' caught the player but " +
                $"EncounterToTrigger is not set on its EnemyAIData — no battle started.",
                enemy);
            enemy.ChangeState(enemy.IdleState);
            return;
        }

        var player = enemy.Player;
        if (player == null)
        {
            // Player went missing (e.g. scene change mid-frame). Bail out.
            enemy.ChangeState(enemy.IdleState);
            return;
        }

        triggered = true;

        // Tell GameController which overworld enemy started this battle so its
        // id can be recorded in DefeatedEnemyRegistry when the player wins.
        if (GameController.Instance != null)
            GameController.Instance.SetPendingOverworldEnemy(enemy.EnemyId);

        player.TriggerEncounter(data.EncounterToTrigger);
    }

    public void Exit(OverworldEnemyController enemy) { }
}
