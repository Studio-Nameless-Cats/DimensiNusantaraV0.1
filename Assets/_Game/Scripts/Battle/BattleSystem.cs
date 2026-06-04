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
    [SerializeField] private SkillPanel       skillPanel;

    [Header("Special gauge")]
    [Tooltip("Special-gauge points a player gains when they land a basic attack (0..100).")]
    [SerializeField] private int specialChargeOnAttack = 20;
    [Tooltip("Special-gauge points a player gains when they get hit (0..100).")]
    [SerializeField] private int specialChargeOnHit    = 15;

    [Header("Critical Hit")]
    [Tooltip("Probability (0–1) that a Basic Attack triggers the Dice Roll modal.")]
    [SerializeField] [Range(0f, 1f)] private float critTriggerChance = 0.30f;
    [Tooltip("Damage multiplier applied when a Critical Hit is confirmed.")]
    [SerializeField] private float critMultiplier = 2f;

    [Header("Parry")]
    [SerializeField] private ParrySystem parrySystem;
    [Tooltip("Counter-attack multiplier after a PERFECT parry (all circles tapped dead-on). 1 = normal, 1.5 = +50%.")]
    [SerializeField] private float parryCounterMultiplier = 1.5f;
    [Tooltip("Counter-attack multiplier after a GOOD (but not perfect) parry. Lower than Perfect — precision is rewarded.")]
    [SerializeField] private float goodParryCounterMultiplier = 0.75f;
    [Tooltip("How many TAP circles appear during the parry window. More = easier to parry.")]
    [SerializeField] [Range(1, 5)] private int parryButtonCount = 2;

    [Header("Juice")]
    [Tooltip("Optional camera shake on impacts. Assign the Battle Camera's CameraShake component. Leave null to disable.")]
    [SerializeField] private CameraShake cameraShake;
    [Tooltip("Shake magnitude for a normal hit / skill hit.")]
    [SerializeField] private float hitShakeMagnitude  = 0.15f;
    [Tooltip("Shake magnitude for a critical hit or a perfect-parry counter (heavier).")]
    [SerializeField] private float critShakeMagnitude = 0.30f;

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
            healthyMembers[i].ResetSpecial();               // Special gauge starts empty each battle
            healthyMembers[i].ClearStatuses();              // no buffs/debuffs carry between battles
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
        // Initiative is rebuilt each round from current effective Speed (so Slow/Haste
        // statuses change ordering), and the display bar is (re)initialised inside.
        StartNewRound();

        if (turnOrder.Count == 0)
        {
            Debug.LogError("[BattleSystem] Turn order is empty — no units were spawned. Battle cannot start.");
            yield break;
        }

        Debug.Log($"[BattleSystem] Turn order ({turnOrder.Count} units): {string.Join(" → ", turnOrder.Select(u => u.Member.Name))}");

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

    /// <summary>
    /// (Re)build the initiative order for a fresh round from every still-standing unit,
    /// sorted by CURRENT effective Speed — so Slow/Haste statuses applied during the fight
    /// re-order the queue from the next round. Resets the turn pointer and rebuilds the
    /// turn-order bar (fainted units drop off naturally since they're filtered out here).
    /// </summary>
    private void StartNewRound()
    {
        turnOrder = playerUnits.Concat(enemyUnits)
                               .Where(u => u != null && !u.Member.IsFainted)
                               .OrderByDescending(u => u.Member.Speed)
                               .ToList();
        turnIndex = 0;
        turnOrderDisplay?.Initialise(turnOrder);
    }

    /// <summary>Kicks off the current unit's turn (entry point used everywhere a turn ends).</summary>
    private void StartNextTurn() => StartCoroutine(RunTurn());

    /// <summary>
    /// Drives one unit's turn: rolls a new round when the queue is exhausted, skips
    /// units that fainted mid-round, resolves start-of-turn statuses (DoT/regen + stun),
    /// then hands off to the player command menu or the enemy AI.
    /// </summary>
    private IEnumerator RunTurn()
    {
        // End of the queue → start a fresh round (re-sorted by current Speed).
        if (turnIndex >= turnOrder.Count)
            StartNewRound();

        // Skip units that fainted earlier this round (still in the list for display alignment).
        int guard = 0;
        while (turnIndex < turnOrder.Count && turnOrder[turnIndex].Member.IsFainted)
        {
            turnIndex++;
            if (turnIndex >= turnOrder.Count) StartNewRound();
            if (++guard > 200) { EndBattle(false); yield break; }   // safety net
        }

        if (turnOrder.Count == 0) { EndBattle(false); yield break; }

        var current = turnOrder[turnIndex];

        // Highlight whoever is acting now.
        turnOrderDisplay?.UpdateCurrentTurn(turnIndex);

        // ── Start-of-turn status resolution ────────────────────────────────────
        if (current.Member.HasStatuses)
        {
            var report = current.Member.ProcessTurnStart();
            current.RefreshStatusIcons();

            // Per-turn HP ticks (poison/burn damage, regen heal).
            foreach (var tick in report.Ticks)
            {
                current.UpdateHud();
                if (tick.HpDelta < 0)
                    yield return dialogBox.TypeDialog($"{current.Member.Name} terkena {tick.Data.Name} — {-tick.HpDelta} damage!");
                else
                    yield return dialogBox.TypeDialog($"{current.Member.Name} pulih {tick.HpDelta} HP dari {tick.Data.Name}.");
                yield return new WaitForSeconds(0.4f);
            }

            // A DoT may have downed the unit — resolve faint + win/lose, then move on.
            if (current.Member.IsFainted)
            {
                yield return ResolveAfterAction();
                yield break;
            }

            // Stun: the unit loses its action this turn (duration already counted down).
            if (report.WasStunned)
            {
                yield return dialogBox.TypeDialog($"{current.Member.Name} tertegun dan tidak bisa bergerak!");
                yield return new WaitForSeconds(0.6f);
                AdvanceTurnIndex();
                StartNextTurn();
                yield break;
            }
        }

        if (current.IsPlayerUnit)
        {
            state = BattleState.PlayerAction;
            yield return ShowPlayerActions(current);
        }
        else
        {
            state = BattleState.EnemyAttack;
            yield return EnemyTurn(current);
        }
    }

    private void AdvanceTurnIndex() => turnIndex++;

    // ── Player action ─────────────────────────────────────────────────────────

    private IEnumerator ShowPlayerActions(BattleUnit unit)
    {
        yield return dialogBox.TypeDialog($"Apa yang akan dilakukan {unit.Member.Name}?");
        OpenActionMenu();
    }

    /// <summary>Shows the 4-button command menu and (re)wires its events. Used both at
    /// turn start and when the player backs out of a skill panel.</summary>
    private void OpenActionMenu()
    {
        state = BattleState.PlayerAction;
        dialogBox.ShowActionSelector(true);
        dialogBox.EnableButtons(true);

        // Wire button events — unsubscribe first to avoid stacking listeners
        UnsubscribeButtons();
        dialogBox.OnAttackPressed  += HandleAttack;
        dialogBox.OnSkillPressed   += HandleSkill;
        dialogBox.OnSpecialPressed += HandleSpecial;
        dialogBox.OnRunPressed     += HandleRun;
    }

    private void HandleAttack()
    {
        if (state != BattleState.PlayerAction) return;
        CloseActionMenu();

        var attacker     = turnOrder[turnIndex];
        var aliveEnemies = enemyUnits.Where(u => !u.Member.IsFainted).ToList();

        if (aliveEnemies.Count == 0) { EndBattle(true); return; }

        // Only one enemy alive — skip the selector and attack immediately
        if (aliveEnemies.Count == 1 || targetSelector == null)
        {
            StartCoroutine(PerformAttack(attacker, aliveEnemies[0], isPlayerAttack: true));
            return;
        }

        // Multiple enemies — show target selector, wait for player choice.
        // Tapping the backdrop backs out to the command menu (attack costs nothing).
        state = BattleState.Busy;
        targetSelector.Show(aliveEnemies,
            chosenTarget => StartCoroutine(PerformAttack(attacker, chosenTarget, isPlayerAttack: true)),
            onCancel: () => OpenActionMenu());
    }

    private void HandleRun()
    {
        if (state != BattleState.PlayerAction) return;
        CloseActionMenu();
        StartCoroutine(TryRun());
    }

    // ── Skill / Special Skill commands ─────────────────────────────────────────

    private void HandleSkill()   => OpenSkillPicker(SkillCategory.Normal);
    private void HandleSpecial() => OpenSkillPicker(SkillCategory.Special);

    private void OpenSkillPicker(SkillCategory category)
    {
        if (state != BattleState.PlayerAction) return;

        var user = turnOrder[turnIndex];

        if (skillPanel == null)
        {
            // No panel wired yet — keep the menu open so the button isn't a dead-end.
            Debug.LogWarning("[BattleSystem] skillPanel not assigned — skill command ignored.");
            return;
        }

        CloseActionMenu();
        state = BattleState.Busy;

        // Read the loadout-aware lists: equipped normal skills, fixed special skills.
        var list = category == SkillCategory.Special
            ? user.Member.SpecialSkills
            : user.Member.Skills;

        skillPanel.Show(list, user.Member, category, chosen =>
        {
            if (chosen == null)
            {
                // Cancelled (tapped outside) — back to the command menu.
                OpenActionMenu();
                return;
            }
            StartCoroutine(BeginSkill(user, chosen));
        });
    }

    private void CloseActionMenu()
    {
        UnsubscribeButtons();
        dialogBox.ShowActionSelector(false);
        dialogBox.EnableButtons(false);
    }

    private void UnsubscribeButtons()
    {
        dialogBox.OnAttackPressed  -= HandleAttack;
        dialogBox.OnSkillPressed   -= HandleSkill;
        dialogBox.OnSpecialPressed -= HandleSpecial;
        dialogBox.OnRunPressed     -= HandleRun;
    }

    /// <summary>Resolves the skill's target(s), then runs it. Handles the per-target
    /// picker for single-target skills (auto-targets when only one valid choice).</summary>
    private IEnumerator BeginSkill(BattleUnit user, SkillData skill)
    {
        // Note: the resource is spent in PerformSkill (after a target is locked in), so
        // backing out of target selection costs nothing.

        // Build the candidate target list (side depends on the skill).
        if (skill.TargetsSelf)
        {
            yield return PerformSkill(user, skill, new List<BattleUnit> { user });
            yield break;
        }

        var candidates = (skill.TargetsEnemies ? enemyUnits : playerUnits)
            .Where(u => !u.Member.IsFainted).ToList();

        if (candidates.Count == 0) { OpenActionMenu(); yield break; }

        if (skill.TargetsAll)
        {
            yield return PerformSkill(user, skill, candidates);
            yield break;
        }

        // Single target: auto-pick if only one, else show the selector.
        if (candidates.Count == 1 || targetSelector == null)
        {
            yield return PerformSkill(user, skill, new List<BattleUnit> { candidates[0] });
            yield break;
        }

        // Multiple choices — pick one, or tap outside to back out to the command menu.
        targetSelector.Show(candidates,
            chosen => StartCoroutine(PerformSkill(user, skill, new List<BattleUnit> { chosen })),
            onCancel: () => OpenActionMenu());
    }

    /// <summary>Spends the skill's resource, then applies its effect (damage or heal) to every target.</summary>
    private IEnumerator PerformSkill(BattleUnit user, SkillData skill, List<BattleUnit> targets)
    {
        // Pay now that a target is committed (cards were only tappable if affordable).
        bool paid = skill.Category == SkillCategory.Special
            ? user.Member.SpendSpecial(skill.Cost)
            : user.Member.SpendMp(skill.Cost);
        if (!paid)
        {
            Debug.LogWarning("[BattleSystem] Could not pay for skill — returning to menu.");
            OpenActionMenu();
            yield break;
        }

        state = BattleState.Busy;
        user.RefreshResources();   // show the MP / Special spend immediately

        user.PlayAttackAnimation();
        yield return new WaitForSeconds(attackDelay);

        if (skill.EffectType == SkillEffectType.Damage)
        {
            foreach (var t in targets)
            {
                if (t.Member.IsFainted) continue;
                t.PlayHitAnimation();
                int dmg = t.Member.TakeDamage(user.Member.Attack, skill.DamageMultiplier);
                t.UpdateHud();
                ShakeCamera(hitShakeMagnitude);
                yield return dialogBox.TypeDialog(
                    $"{user.Member.Name} menggunakan {skill.Name} — {dmg} damage ke {t.Member.Name}!");
            }
            if (skill.AppliesStatus) yield return ApplyStatusToTargets(skill, targets);   // rider
        }
        else if (skill.EffectType == SkillEffectType.Heal)
        {
            foreach (var t in targets)
            {
                t.Member.Heal(skill.HealAmount);
                t.UpdateHud();
                yield return dialogBox.TypeDialog(
                    $"{user.Member.Name} menggunakan {skill.Name} — memulihkan {skill.HealAmount} HP {t.Member.Name}!");
            }
            if (skill.AppliesStatus) yield return ApplyStatusToTargets(skill, targets);   // rider
        }
        else // ApplyStatus — the status IS the effect
        {
            yield return dialogBox.TypeDialog($"{user.Member.Name} menggunakan {skill.Name}!");
            yield return ApplyStatusToTargets(skill, targets);
        }

        // Resolve faints for any damaged enemies, then continue the turn cycle.
        yield return ResolveAfterAction();
    }

    /// <summary>Applies a skill's status effect to each living target and announces it.</summary>
    private IEnumerator ApplyStatusToTargets(SkillData skill, List<BattleUnit> targets)
    {
        var status = skill.StatusEffect;
        if (status == null) yield break;

        foreach (var t in targets)
        {
            if (t.Member.IsFainted) continue;

            bool added = t.Member.ApplyStatus(status);
            t.RefreshStatusIcons();
            t.UpdateHud();   // refresh in case a buff/debuff is reflected on the HUD

            yield return dialogBox.TypeDialog(added
                ? $"{t.Member.Name} terkena efek {status.Name}!"
                : $"Efek {status.Name} pada {t.Member.Name} diperbarui!");
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>Shared post-action win/lose + turn advance (used by skills, which can
    /// hit multiple targets). Mirrors the tail of CheckFainted.</summary>
    private IEnumerator ResolveAfterAction()
    {
        foreach (var u in playerUnits.Concat(enemyUnits))
            if (u.gameObject.activeSelf && u.Member.IsFainted)
            {
                u.PlayFaintAnimation();
                turnOrderDisplay?.MarkFainted(u);
                yield return dialogBox.TypeDialog($"{u.Member.Name} tewas mengenaskan!");
                yield return new WaitForSeconds(0.4f);
                u.Hide();
            }

        bool playerWon  = !enemyUnits.Any(u => !u.Member.IsFainted);
        bool playerLost = !playerUnits.Any(u => !u.Member.IsFainted);

        if (playerWon)  { yield return dialogBox.TypeDialog("Kamu menang dalam pertarungan!"); yield return new WaitForSeconds(1f); yield return AwardBattleExp(); EndBattle(true);  yield break; }
        if (playerLost) { yield return dialogBox.TypeDialog("Party kamu dikalahkan...");        yield return new WaitForSeconds(1f); EndBattle(false); yield break; }

        AdvanceTurnIndex();
        StartNextTurn();
    }

    // ── Enemy AI ──────────────────────────────────────────────────────────────

    private IEnumerator EnemyTurn(BattleUnit attacker)
    {
        yield return new WaitForSeconds(enemyTurnDelay);

        var alivePlayers = playerUnits.Where(u => !u.Member.IsFainted).ToList();
        if (alivePlayers.Count == 0) { EndBattle(false); yield break; }

        var target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];

        // ── Parry prompt ──────────────────────────────────────────────────────
        ParryTier parryGrade = ParryTier.Miss;

        if (parrySystem != null)
        {
            yield return parrySystem.Show(
                attacker.Member.Name,
                target.Member.Name,
                parryButtonCount,
                result => parryGrade = result);
        }

        if (parryGrade == ParryTier.Miss)
        {
            // Failed parry (or no parry system): take the hit in full.
            yield return PerformAttack(attacker, target, isPlayerAttack: false);
        }
        else
        {
            // Successful parry: incoming attack is fully blocked, then the defender
            // counters — Perfect lands a bigger counter than Good (precision pays off).
            float counterMult = parryGrade == ParryTier.Perfect
                ? parryCounterMultiplier
                : goodParryCounterMultiplier;
            yield return PerformParryCounter(defender: target, originalAttacker: attacker,
                                             counterMultiplier: counterMult, grade: parryGrade);
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

        // ── Dice Roll: player attacks only, with critTriggerChance probability ──
        // Resolve the dice BEFORE swinging so the attack animation doesn't fire
        // before the crit is even decided. When no dice roll happens (the common
        // case), the swing plays immediately on the button press as before.
        bool isCrit       = false;
        bool willRollDice = isPlayerAttack && diceRollUI != null
                            && UnityEngine.Random.value < critTriggerChance;

        if (willRollDice)
        {
            yield return diceRollUI.Show(
                attacker.Member.Name,
                target.Member.Name,
                result => isCrit = result);

            if (isCrit) damageMultiplier = critMultiplier;
        }

        attacker.PlayAttackAnimation();
        yield return new WaitForSeconds(attackDelay);

        target.PlayHitAnimation();
        int damage = target.Member.TakeDamage(attacker.Member.Attack, damageMultiplier);
        target.UpdateHud();
        ShakeCamera(isCrit ? critShakeMagnitude : hitShakeMagnitude);

        // Build the Special gauge: the attacker charges on a basic attack, and any
        // player who gets hit charges too (so a defending party still builds toward a special).
        if (isPlayerAttack)      attacker.Member.AddSpecial(specialChargeOnAttack);
        if (target.IsPlayerUnit) target.Member.AddSpecial(specialChargeOnHit);

        // Reflect the Special-gauge change on the HUDs (target's HP bar already
        // refreshed via UpdateHud above; this catches the attacker's gauge).
        if (isPlayerAttack)      attacker.RefreshResources();
        if (target.IsPlayerUnit) target.RefreshResources();

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
    /// immediately strikes back at the original attacker. The counter multiplier
    /// scales with parry precision (Perfect > Good).
    /// </summary>
    private IEnumerator PerformParryCounter(BattleUnit defender, BattleUnit originalAttacker,
                                            float counterMultiplier, ParryTier grade)
    {
        state = BattleState.Busy;

        string lead = grade == ParryTier.Perfect ? "PARRY SEMPURNA" : "Parry berhasil";
        yield return dialogBox.TypeDialog($"{lead}! {defender.Member.Name} membalas serangan!");

        defender.PlayParryAnimation();
        yield return new WaitForSeconds(attackDelay);

        originalAttacker.PlayHitAnimation();
        int damage = originalAttacker.Member.TakeDamage(defender.Member.Attack, counterMultiplier);
        originalAttacker.UpdateHud();
        ShakeCamera(grade == ParryTier.Perfect ? critShakeMagnitude : hitShakeMagnitude);

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

            // Clear the fallen unit from view entirely (sprite, name, HP bar).
            unit.Hide();

            bool playerWon  = !enemyUnits.Any(u => !u.Member.IsFainted);
            bool playerLost = !playerUnits.Any(u => !u.Member.IsFainted);

            if (playerWon)
            {
                yield return dialogBox.TypeDialog("Kamu menang dalam pertarungan!");
                yield return new WaitForSeconds(1f);
                yield return AwardBattleExp();
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

    // ── Experience reward ──────────────────────────────────────────────────────

    /// <summary>
    /// On a win, sums each defeated enemy's <see cref="CharacterData.ExpReward"/> and
    /// grants the full amount to every SURVIVING player member (fainted members earn
    /// nothing). Members are the live PartyMember instances (they persist across the
    /// scene reload), so the EXP + any level-ups stick and get saved. Announces the
    /// EXP gain and any level-ups in the dialog box.
    /// </summary>
    private IEnumerator AwardBattleExp()
    {
        int totalExp = enemyUnits.Sum(u => u.Member?.Base != null ? u.Member.Base.ExpReward : 0);
        if (totalExp <= 0) yield break;

        var recipients = playerUnits.Where(u => u.Member != null && !u.Member.IsFainted)
                                    .Select(u => u.Member)
                                    .ToList();
        if (recipients.Count == 0) yield break;

        yield return dialogBox.TypeDialog($"Party memperoleh {totalExp} EXP!");
        yield return new WaitForSeconds(0.6f);

        foreach (var m in recipients)
        {
            var levelsGained = m.AddExp(totalExp);
            foreach (int newLevel in levelsGained)
            {
                yield return dialogBox.TypeDialog($"{m.Name} naik ke Level {newLevel}!");
                yield return new WaitForSeconds(0.6f);
            }
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

    // ── Juice ─────────────────────────────────────────────────────────────────

    /// <summary>Fires the optional battle-camera shake. No-op if no CameraShake is wired.</summary>
    private void ShakeCamera(float magnitude) => cameraShake?.Shake(magnitude);

    // ── Public handle (for GameController poll if needed) ─────────────────────
    public void HandleUpdate() { /* Input is handled via UI buttons */ }
}
