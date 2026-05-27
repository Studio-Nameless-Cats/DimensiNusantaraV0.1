using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// ── Battle state machine states ───────────────────────────────────────────────
public enum BattleState
{
    Start,        // Setting up the battlefield
    PlayerAction, // Waiting for player to choose Attack or Run
    PlayerAttack, // Executing player's attack
    EnemyAttack,  // Enemy AI taking its turn
    Busy,         // Waiting for an animation / coroutine to finish
    BattleOver    // Battle has ended
}

/// <summary>
/// Core turn-based battle system. Manages spawning, turn order, attacks, and win/lose.
///
/// Scene Setup:
///   1. Create a "Battle" scene with this component on a BattleSystem GameObject.
///   2. Add spawn point Transforms for player units and enemy units.
///   3. Create a BattleUnit prefab (model + Animator + BattleUnit script + BattleHud UI).
///   4. Wire up a BattleDialogBox in the Canvas.
///   5. The GameController will call StartBattle() after scene load.
/// </summary>
public class BattleSystem : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private List<Transform> playerSpawnPoints;
    [SerializeField] private List<Transform> enemySpawnPoints;

    [Header("Prefab")]
    [Tooltip("Prefab that has BattleUnit + Animator + BattleHud.")]
    [SerializeField] private GameObject battleUnitPrefab;

    [Header("UI")]
    [SerializeField] private BattleDialogBox  dialogBox;
    [SerializeField] private TurnOrderDisplay turnOrderDisplay;
    [SerializeField] private DiceRollUI       diceRollUI;
    [SerializeField] private TargetSelector   targetSelector;

    [Header("Critical Hit")]
    [Tooltip("Probability (0–1) that a Basic Attack triggers the Dice Roll modal.")]
    [SerializeField] [Range(0f, 1f)] private float critTriggerChance = 0.30f;
    [Tooltip("Damage multiplier applied when a Critical Hit is confirmed.")]
    [SerializeField] private float critMultiplier = 2f;

    [Header("Parry")]
    [SerializeField] private ParrySystem parrySystem;
    [Tooltip("Damage multiplier for the counter-attack the player lands after a successful parry. 1 = normal damage, 1.5 = 50% bonus.")]
    [SerializeField] private float parryCounterMultiplier = 1.5f;
    [Tooltip("How many TAP circles appear during the parry window. More = easier to parry.")]
    [SerializeField] [Range(1, 5)] private int parryButtonCount = 2;

    [Header("Timing")]
    [SerializeField] private float enemyTurnDelay = 0.8f;   // pause before enemy acts
    [SerializeField] private float attackDelay    = 0.5f;   // pause after attack anim starts

    // ── Runtime state ─────────────────────────────────────────────────────────
    private BattleState       state;
    private List<BattleUnit>  playerUnits = new List<BattleUnit>();
    private List<BattleUnit>  enemyUnits  = new List<BattleUnit>();
    private List<BattleUnit>  turnOrder   = new List<BattleUnit>(); // sorted by Speed
    private int               turnIndex;

    // ── Event ─────────────────────────────────────────────────────────────────
    /// <summary>Fired when the battle ends. bool = true if the player won (or fled).</summary>
    public event Action<bool> OnBattleOver;

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>Called by GameController after the Battle scene loads.</summary>
    public void StartBattle(List<PartyMember> partyMembers, EnemyEncounterData encounterData)
    {
        Debug.Log("[BattleSystem] StartBattle() called.");
        StartCoroutine(SetupBattle(partyMembers, encounterData));
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private IEnumerator SetupBattle(List<PartyMember> partyMembers, EnemyEncounterData encounterData)
    {
        state = BattleState.Start;
        ClearUnits();

        // ── Inspector reference checks ────────────────────────────────────────
        if (battleUnitPrefab == null)
            Debug.LogError("[BattleSystem] battleUnitPrefab is NOT assigned in the Inspector! ❌ Assign the BattleUnit prefab to the BattleSystem component in the Battle scene.");

        if (playerSpawnPoints == null || playerSpawnPoints.Count == 0)
            Debug.LogError("[BattleSystem] playerSpawnPoints list is EMPTY! ❌ Assign at least one spawn point Transform in the Inspector.");

        if (enemySpawnPoints == null || enemySpawnPoints.Count == 0)
            Debug.LogError("[BattleSystem] enemySpawnPoints list is EMPTY! ❌ Assign at least one spawn point Transform in the Inspector.");

        if (dialogBox == null)
            Debug.LogError("[BattleSystem] dialogBox is NOT assigned in the Inspector! ❌ Assign the BattleDialogBox component.");

        // ── Spawn player units ────────────────────────────────────────────────
        var healthyMembers = partyMembers.Where(m => !m.IsFainted).ToList();
        int playerCount    = Mathf.Min(healthyMembers.Count, playerSpawnPoints.Count);

        Debug.Log($"[BattleSystem] Spawning player units: {healthyMembers.Count} healthy member(s), {playerSpawnPoints.Count} spawn point(s) → spawning {playerCount}.");

        for (int i = 0; i < playerCount; i++)
        {
            Debug.Log($"[BattleSystem] Spawning player unit [{i}]: {healthyMembers[i].Name}");
            var unit = SpawnUnit(playerSpawnPoints[i]);
            if (unit == null) { Debug.LogError($"[BattleSystem] SpawnUnit() returned null for player slot {i}! ❌ Check your BattleUnit prefab has a BattleUnit component on its root."); continue; }
            unit.Setup(healthyMembers[i], isPlayer: true);  // ← explicitly marked as player
            playerUnits.Add(unit);
        }

        // ── Spawn enemy units ─────────────────────────────────────────────────
        var enemyDataList = encounterData.GetRandomEnemies();
        int enemyCount    = Mathf.Min(enemyDataList.Count, enemySpawnPoints.Count);

        Debug.Log($"[BattleSystem] Spawning enemy units: {enemyDataList.Count} from encounter data, {enemySpawnPoints.Count} spawn point(s) → spawning {enemyCount}.");

        if (enemyDataList.Count == 0)
            Debug.LogError("[BattleSystem] GetRandomEnemies() returned 0 enemies! ❌ Check your EnemyEncounterData SO has enemies assigned with spawnWeight > 0.");

        for (int i = 0; i < enemyCount; i++)
        {
            Debug.Log($"[BattleSystem] Spawning enemy unit [{i}]: {enemyDataList[i].Name}");
            var unit = SpawnUnit(enemySpawnPoints[i]);
            if (unit == null) { Debug.LogError($"[BattleSystem] SpawnUnit() returned null for enemy slot {i}! ❌ Check your BattleUnit prefab has a BattleUnit component on its root."); continue; }
            unit.Setup(new PartyMember(enemyDataList[i]), isPlayer: false);  // ← explicitly marked as enemy
            enemyUnits.Add(unit);
        }

        // ── Determine turn order ──────────────────────────────────────────────
        turnOrder = playerUnits.Concat(enemyUnits)
                               .OrderByDescending(u => u.Member.Speed)
                               .ToList();
        turnIndex = 0;

        Debug.Log($"[BattleSystem] Turn order ({turnOrder.Count} units): {string.Join(" → ", turnOrder.Select(u => u.Member.Name))}");

        // Initialise the Turn Order display bar
        turnOrderDisplay?.Initialise(turnOrder);

        if (turnOrder.Count == 0)
        {
            Debug.LogError("[BattleSystem] Turn order is empty — no units were spawned. Battle cannot start.");
            yield break;
        }

        // ── Opening message ───────────────────────────────────────────────────
        string enemyNames = string.Join(", ", enemyUnits.Select(u => u.Member.Name));
        yield return dialogBox.TypeDialog($"Pertemuan Tak Terduga! {enemyNames} muncul!");

        Debug.Log("[BattleSystem] Setup complete — starting first turn.");
        StartNextTurn();
    }

    private BattleUnit SpawnUnit(Transform spawnPoint)
    {
        if (battleUnitPrefab == null) return null;

        var go   = Instantiate(battleUnitPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        var unit = go.GetComponent<BattleUnit>();

        if (unit == null)
            Debug.LogError($"[BattleSystem] Instantiated prefab '{battleUnitPrefab.name}' but found no BattleUnit component on its root GameObject! ❌");

        return unit;
    }

    // ── Turn management ───────────────────────────────────────────────────────

    private void StartNextTurn()
    {
        // Skip fainted units, wrapping the list
        int attempts = 0;
        while (turnOrder[turnIndex].Member.IsFainted)
        {
            AdvanceTurnIndex();
            if (++attempts > turnOrder.Count) { EndBattle(false); return; } // safety
        }

        var current = turnOrder[turnIndex];

        // Update the Turn Order bar to highlight whoever is acting now
        turnOrderDisplay?.UpdateCurrentTurn(turnIndex);

        if (current.IsPlayerUnit)
        {
            state = BattleState.PlayerAction;
            StartCoroutine(ShowPlayerActions(current));
        }
        else
        {
            state = BattleState.EnemyAttack;
            StartCoroutine(EnemyTurn(current));
        }
    }

    private void AdvanceTurnIndex()
    {
        turnIndex = (turnIndex + 1) % turnOrder.Count;
    }

    // ── Player action ─────────────────────────────────────────────────────────

    private IEnumerator ShowPlayerActions(BattleUnit unit)
    {
        yield return dialogBox.TypeDialog($"Apa yang akan dilakukan {unit.Member.Name}?");
        dialogBox.ShowActionSelector(true);
        dialogBox.EnableButtons(true);

        // Wire button events — unsubscribe first to avoid stacking listeners
        dialogBox.OnAttackPressed -= HandleAttack;
        dialogBox.OnRunPressed    -= HandleRun;
        dialogBox.OnAttackPressed += HandleAttack;
        dialogBox.OnRunPressed    += HandleRun;
    }

    private void HandleAttack()
    {
        if (state != BattleState.PlayerAction) return;
        UnsubscribeButtons();
        dialogBox.ShowActionSelector(false);
        dialogBox.EnableButtons(false);

        var attacker     = turnOrder[turnIndex];
        var aliveEnemies = enemyUnits.Where(u => !u.Member.IsFainted).ToList();

        if (aliveEnemies.Count == 0) { EndBattle(true); return; }

        // Only one enemy alive — skip the selector and attack immediately
        if (aliveEnemies.Count == 1 || targetSelector == null)
        {
            StartCoroutine(PerformAttack(attacker, aliveEnemies[0], isPlayerAttack: true));
            return;
        }

        // Multiple enemies — show target selector, wait for player choice
        targetSelector.Show(aliveEnemies, chosenTarget =>
        {
            StartCoroutine(PerformAttack(attacker, chosenTarget, isPlayerAttack: true));
        });
    }

    private void HandleRun()
    {
        if (state != BattleState.PlayerAction) return;
        UnsubscribeButtons();
        dialogBox.ShowActionSelector(false);
        dialogBox.EnableButtons(false);
        StartCoroutine(TryRun());
    }

    private void UnsubscribeButtons()
    {
        dialogBox.OnAttackPressed -= HandleAttack;
        dialogBox.OnRunPressed    -= HandleRun;
    }

    // ── Enemy AI ──────────────────────────────────────────────────────────────

    private IEnumerator EnemyTurn(BattleUnit attacker)
    {
        yield return new WaitForSeconds(enemyTurnDelay);

        var alivePlayers = playerUnits.Where(u => !u.Member.IsFainted).ToList();
        if (alivePlayers.Count == 0) { EndBattle(false); yield break; }

        var target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];

        // ── Parry prompt ──────────────────────────────────────────────────────
        bool wasParried = false;

        if (parrySystem != null)
        {
            yield return parrySystem.Show(
                attacker.Member.Name,
                target.Member.Name,
                parryButtonCount,
                result => wasParried = result);
        }

        if (wasParried)
        {
            // Successful parry: incoming attack is completely nullified,
            // then the defending player unit immediately counter-attacks.
            yield return PerformParryCounter(defender: target, originalAttacker: attacker);
        }
        else
        {
            yield return PerformAttack(attacker, target, isPlayerAttack: false);
        }
    }

    // ── Attack execution ──────────────────────────────────────────────────────

    /// <summary>
    /// Executes one attack from attacker → target.
    /// Pass damageMultiplier = critMultiplier for a crit, or 1f for a normal hit.
    /// The dice roll check runs automatically for player attacks.
    /// </summary>
    private IEnumerator PerformAttack(BattleUnit attacker, BattleUnit target,
                                       bool isPlayerAttack, float damageMultiplier = 1f)
    {
        state = isPlayerAttack ? BattleState.PlayerAttack : BattleState.EnemyAttack;

        attacker.PlayAttackAnimation();
        yield return new WaitForSeconds(attackDelay);

        // ── Dice Roll: player attacks only, with critTriggerChance probability ──
        bool isCrit = false;

        if (isPlayerAttack && diceRollUI != null && UnityEngine.Random.value < critTriggerChance)
        {
            yield return diceRollUI.Show(
                attacker.Member.Name,
                target.Member.Name,
                result => isCrit = result);

            if (isCrit) damageMultiplier = critMultiplier;
        }

        target.PlayHitAnimation();
        int damage = target.Member.TakeDamage(attacker.Member.Attack, damageMultiplier);
        target.UpdateHud();

        string dialogMsg = isCrit
            ? $"CRITICAL HIT! {attacker.Member.Name} memberikan {damage} damage kepada {target.Member.Name}!"
            : $"{attacker.Member.Name} menyerang {target.Member.Name} sebesar {damage} damage!";

        yield return dialogBox.TypeDialog(dialogMsg);

        yield return CheckFainted(target);
    }

    // ── Parry counter-attack ──────────────────────────────────────────────────

    /// <summary>
    /// Called when the player successfully parries an enemy attack.
    /// The incoming attack is fully negated (0 damage), then the defender
    /// immediately strikes back at the original attacker with a damage bonus.
    /// </summary>
    private IEnumerator PerformParryCounter(BattleUnit defender, BattleUnit originalAttacker)
    {
        state = BattleState.Busy;

        yield return dialogBox.TypeDialog($"Parry berhasil! {defender.Member.Name} membalas serangan!");

        defender.PlayAttackAnimation();
        yield return new WaitForSeconds(attackDelay);

        originalAttacker.PlayHitAnimation();
        int damage = originalAttacker.Member.TakeDamage(defender.Member.Attack, parryCounterMultiplier);
        originalAttacker.UpdateHud();

        yield return dialogBox.TypeDialog(
            $"{defender.Member.Name} membalas serangan {originalAttacker.Member.Name} sebesar {damage} damage!");

        yield return CheckFainted(originalAttacker);
    }

    // ── Faint check (shared by PerformAttack and PerformParryCounter) ─────────

    /// <summary>
    /// Checks if a unit fainted after taking damage. Handles win/lose/continue.
    /// Advances the turn index and starts the next turn if the battle continues.
    /// </summary>
    private IEnumerator CheckFainted(BattleUnit unit)
    {
        if (unit.Member.IsFainted)
        {
            unit.PlayFaintAnimation();
            turnOrderDisplay?.MarkFainted(unit);
            yield return dialogBox.TypeDialog($"{unit.Member.Name} tewas mengenaskan!");
            yield return new WaitForSeconds(0.5f);

            bool playerWon  = !enemyUnits.Any(u => !u.Member.IsFainted);
            bool playerLost = !playerUnits.Any(u => !u.Member.IsFainted);

            if (playerWon)
            {
                yield return dialogBox.TypeDialog("Kamu menang dalam pertarungan!");
                yield return new WaitForSeconds(1f);
                EndBattle(true);
                yield break;
            }
            else if (playerLost)
            {
                yield return dialogBox.TypeDialog("Party kamu dikalahkan...");
                yield return new WaitForSeconds(1f);
                EndBattle(false);
                yield break;
            }
        }

        AdvanceTurnIndex();
        StartNextTurn();
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    private IEnumerator TryRun()
    {
        state = BattleState.Busy;

        int playerMaxSpeed = playerUnits.Where(u => !u.Member.IsFainted).Max(u => u.Member.Speed);
        int enemyMaxSpeed  = enemyUnits.Where(u  => !u.Member.IsFainted).Max(u => u.Member.Speed);

        float escapeChance = (float)playerMaxSpeed / (playerMaxSpeed + enemyMaxSpeed);

        if (UnityEngine.Random.value <= escapeChance)
        {
            yield return dialogBox.TypeDialog("Berhasil melarikan diri!");
            yield return new WaitForSeconds(0.8f);
            EndBattle(false); // false = did not defeat enemies (fled)
        }
        else
        {
            yield return dialogBox.TypeDialog("Tidak bisa melarikan diri!");
            AdvanceTurnIndex();
            StartNextTurn();
        }
    }

    // ── End battle ────────────────────────────────────────────────────────────

    private void EndBattle(bool playerWon)
    {
        state = BattleState.BattleOver;
        ClearUnits();
        OnBattleOver?.Invoke(playerWon);
    }

    private void ClearUnits()
    {
        foreach (var unit in playerUnits.Concat(enemyUnits))
            if (unit != null) Destroy(unit.gameObject);

        playerUnits.Clear();
        enemyUnits.Clear();
        turnOrder.Clear();
        turnIndex = 0;
    }

    // ── Public handle (for GameController poll if needed) ─────────────────────
    public void HandleUpdate() { /* Input is handled via UI buttons */ }
}
