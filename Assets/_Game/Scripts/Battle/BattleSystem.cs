using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// The states our little battle can be in.
public enum BattleState
{
    Start,        // Setting up the battlefield
    PlayerAction, // Waiting for player to pick Attack or Run
    PlayerAttack, // Doing the player's attack
    EnemyAttack,  // Enemy AI taking its turn
    Busy,         // Waiting for an animation / coroutine to finish
    BattleOver    // Fight's done
}

// The heart of the turn-based battle: spawns everyone, figures out turn order,
// runs the attacks, and decides who won.
//
// How to set up the scene:
//   1. Make a "Battle" scene with this component on a BattleSystem GameObject.
//   2. Add spawn-point Transforms for the player units and the enemy units.
//   3. Make a BattleUnit prefab (model + Animator + BattleUnit script + BattleHud UI).
//   4. Drop a BattleDialogBox in the Canvas.
//   5. GameController calls StartBattle() once the scene loads.
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

    // Stuff we track while a fight is happening.
    private BattleState       state;
    private List<BattleUnit>  playerUnits = new List<BattleUnit>();
    private List<BattleUnit>  enemyUnits  = new List<BattleUnit>();
    private List<BattleUnit>  turnOrder   = new List<BattleUnit>(); // sorted by Speed
    private int               turnIndex;

    // Fired when the fight ends. bool = true if the player won (or ran away).
    public event Action<bool> OnBattleOver;

    // GameController calls this once the Battle scene has loaded.
    public void StartBattle(List<PartyMember> partyMembers, EnemyEncounterData encounterData)
    {
        Debug.Log("[BattleSystem] StartBattle() called.");
        StartCoroutine(SetupBattle(partyMembers, encounterData));
    }

    private IEnumerator SetupBattle(List<PartyMember> partyMembers, EnemyEncounterData encounterData)
    {
        state = BattleState.Start;
        ClearUnits();

        // Make sure everything got wired up in the Inspector, or complain loudly.
        if (battleUnitPrefab == null)
            Debug.LogError("[BattleSystem] battleUnitPrefab is NOT assigned in the Inspector! Assign the BattleUnit prefab to the BattleSystem component in the Battle scene.");

        if (playerSpawnPoints == null || playerSpawnPoints.Count == 0)
            Debug.LogError("[BattleSystem] playerSpawnPoints list is EMPTY! Assign at least one spawn point Transform in the Inspector.");

        if (enemySpawnPoints == null || enemySpawnPoints.Count == 0)
            Debug.LogError("[BattleSystem] enemySpawnPoints list is EMPTY! Assign at least one spawn point Transform in the Inspector.");

        if (dialogBox == null)
            Debug.LogError("[BattleSystem] dialogBox is NOT assigned in the Inspector! Assign the BattleDialogBox component.");

        // Spawn the player's healthy members into the player spawn points.
        var healthyMembers = partyMembers.Where(m => !m.IsFainted).ToList();
        int playerCount    = Mathf.Min(healthyMembers.Count, playerSpawnPoints.Count);

        Debug.Log($"[BattleSystem] Spawning player units: {healthyMembers.Count} healthy member(s), {playerSpawnPoints.Count} spawn point(s), spawning {playerCount}.");

        for (int i = 0; i < playerCount; i++)
        {
            Debug.Log($"[BattleSystem] Spawning player unit [{i}]: {healthyMembers[i].Name}");
            var unit = SpawnUnit(playerSpawnPoints[i]);
            if (unit == null) { Debug.LogError($"[BattleSystem] SpawnUnit() returned null for player slot {i}! Check your BattleUnit prefab has a BattleUnit component on its root."); continue; }
            healthyMembers[i].ResetSpecial();               // Special gauge always starts empty
            healthyMembers[i].ClearStatuses();              // no buffs/debuffs carry over between fights
            unit.Setup(healthyMembers[i], isPlayer: true);  // true = this is a player
            playerUnits.Add(unit);
        }

        // Now spawn the enemies the encounter rolled up.
        var enemyDataList = encounterData.GetRandomEnemies();
        int enemyCount    = Mathf.Min(enemyDataList.Count, enemySpawnPoints.Count);

        Debug.Log($"[BattleSystem] Spawning enemy units: {enemyDataList.Count} from encounter data, {enemySpawnPoints.Count} spawn point(s), spawning {enemyCount}.");

        if (enemyDataList.Count == 0)
            Debug.LogError("[BattleSystem] GetRandomEnemies() returned 0 enemies! Check your EnemyEncounterData SO has enemies assigned with spawnWeight > 0.");

        for (int i = 0; i < enemyCount; i++)
        {
            Debug.Log($"[BattleSystem] Spawning enemy unit [{i}]: {enemyDataList[i].Name}");
            var unit = SpawnUnit(enemySpawnPoints[i]);
            if (unit == null) { Debug.LogError($"[BattleSystem] SpawnUnit() returned null for enemy slot {i}! Check your BattleUnit prefab has a BattleUnit component on its root."); continue; }
            unit.Setup(new PartyMember(enemyDataList[i]), isPlayer: false);  // false = this is an enemy
            enemyUnits.Add(unit);
        }

        // Work out who goes first. We rebuild this every round from current Speed
        // (so Slow/Haste actually shuffle the order), and the display bar gets set up inside.
        StartNewRound();

        if (turnOrder.Count == 0)
        {
            Debug.LogError("[BattleSystem] Turn order is empty, nobody spawned. Battle can't start.");
            yield break;
        }

        Debug.Log($"[BattleSystem] Turn order ({turnOrder.Count} units): {string.Join(" -> ", turnOrder.Select(u => u.Member.Name))}");

        // Say hello to the enemies.
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
            Debug.LogError($"[BattleSystem] Instantiated prefab '{battleUnitPrefab.name}' but found no BattleUnit component on its root GameObject!");

        return unit;
    }

    // Builds a fresh turn order for a new round from everyone still standing, sorted by
    // their current Speed. That means Slow/Haste picked up mid-fight shuffle the order
    // starting next round. Also resets the turn pointer and rebuilds the turn bar
    // (fainted units just drop off since we filter them out here).
    private void StartNewRound()
    {
        turnOrder = playerUnits.Concat(enemyUnits)
                               .Where(u => u != null && !u.Member.IsFainted)
                               .OrderByDescending(u => u.Member.Speed)
                               .ToList();
        turnIndex = 0;
        turnOrderDisplay?.Initialise(turnOrder);
    }

    // Starts the current unit's turn. This is the entry point we call every time a turn ends.
    private void StartNextTurn() => StartCoroutine(RunTurn());

    // Runs one unit's turn: rolls a new round when we run out of units, skips anyone
    // who fainted mid-round, handles start-of-turn statuses (poison/regen + stun),
    // then hands control to either the player's command menu or the enemy AI.
    private IEnumerator RunTurn()
    {
        // Ran past the end of the queue? Start a fresh round (re-sorted by Speed).
        if (turnIndex >= turnOrder.Count)
            StartNewRound();

        // Skip anyone who fainted earlier this round (we keep them in the list so the
        // display stays lined up).
        int guard = 0;
        while (turnIndex < turnOrder.Count && turnOrder[turnIndex].Member.IsFainted)
        {
            turnIndex++;
            if (turnIndex >= turnOrder.Count) StartNewRound();
            if (++guard > 200) { EndBattle(false); yield break; }   // just in case, don't loop forever
        }

        if (turnOrder.Count == 0) { EndBattle(false); yield break; }

        var current = turnOrder[turnIndex];

        // Light up whoever's acting right now.
        turnOrderDisplay?.UpdateCurrentTurn(turnIndex);

        // Deal with any statuses that fire at the start of the turn.
        if (current.Member.HasStatuses)
        {
            var report = current.Member.ProcessTurnStart();
            current.RefreshStatusIcons();

            // HP ticks for this turn (poison/burn hurts, regen heals).
            foreach (var tick in report.Ticks)
            {
                current.UpdateHud();
                if (tick.HpDelta < 0)
                    yield return dialogBox.TypeDialog($"{current.Member.Name} terkena {tick.Data.Name} — {-tick.HpDelta} damage!");
                else
                    yield return dialogBox.TypeDialog($"{current.Member.Name} pulih {tick.HpDelta} HP dari {tick.Data.Name}.");
                yield return new WaitForSeconds(0.4f);
            }

            // Poison/burn might have just KO'd them. Handle the faint + win/lose, then bail.
            if (current.Member.IsFainted)
            {
                yield return ResolveAfterAction();
                yield break;
            }

            // Stunned? They lose their action this turn (the duration already ticked down).
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

    // --- Player's turn ---

    private IEnumerator ShowPlayerActions(BattleUnit unit)
    {
        yield return dialogBox.TypeDialog($"Apa yang akan dilakukan {unit.Member.Name}?");
        OpenActionMenu();
    }

    // Pops up the 4-button command menu and hooks up its events. Used both when a turn
    // starts and when the player backs out of the skill panel.
    private void OpenActionMenu()
    {
        state = BattleState.PlayerAction;
        dialogBox.ShowActionSelector(true);
        dialogBox.EnableButtons(true);

        // Unsubscribe first so we don't stack up duplicate listeners.
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

        // Only one enemy left? Skip the picker and just hit it.
        if (aliveEnemies.Count == 1 || targetSelector == null)
        {
            StartCoroutine(PerformAttack(attacker, aliveEnemies[0], isPlayerAttack: true));
            return;
        }

        // More than one enemy: show the target picker and wait for a choice.
        // Tapping the backdrop just backs out to the command menu (attacking is free).
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

    // --- Skill / Special Skill buttons ---

    private void HandleSkill()   => OpenSkillPicker(SkillCategory.Normal);
    private void HandleSpecial() => OpenSkillPicker(SkillCategory.Special);

    private void OpenSkillPicker(SkillCategory category)
    {
        if (state != BattleState.PlayerAction) return;

        var user = turnOrder[turnIndex];

        if (skillPanel == null)
        {
            // No panel hooked up yet, so keep the menu open instead of leaving a dead button.
            Debug.LogWarning("[BattleSystem] skillPanel not assigned, skill command ignored.");
            return;
        }

        CloseActionMenu();
        state = BattleState.Busy;

        // Grab the right list: equipped normal skills, or the fixed special skills.
        var list = category == SkillCategory.Special
            ? user.Member.SpecialSkills
            : user.Member.Skills;

        skillPanel.Show(list, user.Member, category, chosen =>
        {
            if (chosen == null)
            {
                // They tapped outside to cancel, so go back to the command menu.
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

    // Figures out who a skill hits, then runs it. For single-target skills it shows the
    // picker, or auto-targets when there's only one valid choice.
    private IEnumerator BeginSkill(BattleUnit user, SkillData skill)
    {
        // Heads up: we only pay for the skill in PerformSkill (after a target is locked
        // in), so backing out of target selection doesn't cost anything.

        // Build the list of possible targets (which side depends on the skill).
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

        // Single target: auto-pick if there's only one, otherwise show the picker.
        if (candidates.Count == 1 || targetSelector == null)
        {
            yield return PerformSkill(user, skill, new List<BattleUnit> { candidates[0] });
            yield break;
        }

        // Several to choose from: pick one, or tap outside to go back to the command menu.
        targetSelector.Show(candidates,
            chosen => StartCoroutine(PerformSkill(user, skill, new List<BattleUnit> { chosen })),
            onCancel: () => OpenActionMenu());
    }

    // Pays the skill's cost, then applies its effect (damage or heal) to every target.
    private IEnumerator PerformSkill(BattleUnit user, SkillData skill, List<BattleUnit> targets)
    {
        // Pay now that a target's locked in (cards were only tappable if we could afford them).
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
        user.RefreshResources();   // show the MP / Special drain right away

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
            if (skill.AppliesStatus) yield return ApplyStatusToTargets(skill, targets);   // bonus status on top
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
            if (skill.AppliesStatus) yield return ApplyStatusToTargets(skill, targets);   // bonus status on top
        }
        else // ApplyStatus: the status itself IS the whole point of the skill
        {
            yield return dialogBox.TypeDialog($"{user.Member.Name} menggunakan {skill.Name}!");
            yield return ApplyStatusToTargets(skill, targets);
        }

        // Check if anyone we hit went down, then carry on with the turn cycle.
        yield return ResolveAfterAction();
    }

    // Slaps a skill's status effect onto each living target and announces it.
    private IEnumerator ApplyStatusToTargets(SkillData skill, List<BattleUnit> targets)
    {
        var status = skill.StatusEffect;
        if (status == null) yield break;

        foreach (var t in targets)
        {
            if (t.Member.IsFainted) continue;

            bool added = t.Member.ApplyStatus(status);
            t.RefreshStatusIcons();
            t.UpdateHud();   // redraw in case the buff/debuff shows up on the HUD

            yield return dialogBox.TypeDialog(added
                ? $"{t.Member.Name} terkena efek {status.Name}!"
                : $"Efek {status.Name} pada {t.Member.Name} diperbarui!");
            yield return new WaitForSeconds(0.3f);
        }
    }

    // Shared "did anyone die, did we win/lose, whose turn next" cleanup. Skills use this
    // since they can hit several targets at once. Basically the tail end of CheckFainted.
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

    // --- Enemy's turn ---

    private IEnumerator EnemyTurn(BattleUnit attacker)
    {
        yield return new WaitForSeconds(enemyTurnDelay);

        var alivePlayers = playerUnits.Where(u => !u.Member.IsFainted).ToList();
        if (alivePlayers.Count == 0) { EndBattle(false); yield break; }

        var target = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];

        // Give the player a chance to parry the incoming hit.
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
            // Whiffed the parry (or there's no parry system): just eat the hit.
            yield return PerformAttack(attacker, target, isPlayerAttack: false);
        }
        else
        {
            // Nailed the parry: the hit is fully blocked, then we counter. A Perfect
            // parry counters harder than a Good one, so tapping precisely pays off.
            float counterMult = parryGrade == ParryTier.Perfect
                ? parryCounterMultiplier
                : goodParryCounterMultiplier;
            yield return PerformParryCounter(defender: target, originalAttacker: attacker,
                                             counterMultiplier: counterMult, grade: parryGrade);
        }
    }

    // --- Doing an attack ---

    // Runs one attack from attacker to target.
    // Pass damageMultiplier = critMultiplier for a crit, or 1f for a plain hit.
    // The dice-roll crit check happens automatically on player attacks.
    private IEnumerator PerformAttack(BattleUnit attacker, BattleUnit target,
                                       bool isPlayerAttack, float damageMultiplier = 1f)
    {
        state = isPlayerAttack ? BattleState.PlayerAttack : BattleState.EnemyAttack;

        // Dice roll only happens on player attacks, and only some of the time
        // (critTriggerChance). We settle the dice BEFORE the swing so the animation
        // doesn't play before we even know if it's a crit. No dice roll (the usual
        // case)? The swing fires right away on the button press, same as before.
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

        // Charge up the Special gauge: the attacker gains some for landing a basic
        // attack, and any player who gets hit gains some too (so even a party that's
        // just defending slowly builds toward a special).
        if (isPlayerAttack)      attacker.Member.AddSpecial(specialChargeOnAttack);
        if (target.IsPlayerUnit) target.Member.AddSpecial(specialChargeOnHit);

        // Push that gauge change to the HUDs (the target's HP bar already got refreshed
        // by UpdateHud above; this is here to catch the attacker's gauge).
        if (isPlayerAttack)      attacker.RefreshResources();
        if (target.IsPlayerUnit) target.RefreshResources();

        string dialogMsg = isCrit
            ? $"CRITICAL HIT! {attacker.Member.Name} memberikan {damage} damage kepada {target.Member.Name}!"
            : $"{attacker.Member.Name} menyerang {target.Member.Name} sebesar {damage} damage!";

        yield return dialogBox.TypeDialog(dialogMsg);

        yield return CheckFainted(target);
    }

    // --- Parry counter-attack ---

    // Runs when the player successfully parries an enemy attack. The incoming hit does
    // 0 damage, then the defender immediately swings back at whoever attacked them. How
    // hard the counter hits depends on parry precision (Perfect counters harder than Good).
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

    // --- Faint check (used by both PerformAttack and PerformParryCounter) ---

    // Checks whether a unit just went down after taking damage, and sorts out
    // win/lose/keep-going. If the fight continues, it moves to the next turn.
    private IEnumerator CheckFainted(BattleUnit unit)
    {
        if (unit.Member.IsFainted)
        {
            unit.PlayFaintAnimation();
            turnOrderDisplay?.MarkFainted(unit);
            yield return dialogBox.TypeDialog($"{unit.Member.Name} tewas mengenaskan!");
            yield return new WaitForSeconds(0.5f);

            // Wipe the fallen unit off the screen completely (sprite, name, HP bar).
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

    // --- Running away ---

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
            EndBattle(false); // false = we ran, didn't actually beat the enemies
        }
        else
        {
            yield return dialogBox.TypeDialog("Tidak bisa melarikan diri!");
            AdvanceTurnIndex();
            StartNextTurn();
        }
    }

    // --- Handing out EXP ---

    // When you win, add up every defeated enemy's ExpReward and hand the full amount to
    // each surviving party member (fainted ones get nothing). These are the live
    // PartyMember objects that stick around through the scene reload, so the EXP and any
    // level-ups actually save. Shows the EXP gain and any level-ups in the dialog box.
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

    // --- Wrapping up ---

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

    // --- Juice ---

    // Kicks the optional camera shake. Does nothing if no CameraShake is wired up.
    private void ShakeCamera(float magnitude) => cameraShake?.Shake(magnitude);

    // Here in case GameController wants to poll us. We don't need it; all input is buttons.
    public void HandleUpdate() { /* Input is handled via UI buttons */ }
}
