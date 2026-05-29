# PROGRESS.md — Session log for Nusantara Game

Newest entries at the top. Each entry = one session. Keep tight: what shipped, files touched, decisions, open questions.

---

## 2026-05-29 (pt. 2) — Implementation: Overworld Animation System (code only)

**Shipped:** Code-side of the 4-state animator overhaul locked in the earlier session today. Idle timer + interact trigger plumbing in place. Editor walkthrough deferred to next session per CLAUDE.md Rule 3.

> **Reconciliation note (back-filled 2026-05-29 pt.3):** These pt.1 / pt.2 edits were originally written into the retired `NusantaraRPG_Scripts/` mirror because the Cowork session still had the old Claude Projects folder mounted. On 2026-05-29 pt.3 the Unity project folder was mounted directly, the drift was caught, and the pt.2 code below was applied to `Assets/_Game/Scripts/Player/` to bring Unity in sync. A new `ResetIdleOnExit.cs` StateMachineBehaviour was also added at that point to handle the natural-exit timer reset (see pt.3 entry).

**Files touched (final landing paths in Unity):**
- EDIT: `Assets/_Game/Scripts/Player/PlayerAnimator.cs` — added `idleThreshold` SerializeField (default 7s), `IdleTrigger`/`InteractTrigger` hash constants, `_idleTimer` + `_idleFired` state, public `ResetIdleTimer()`, public `TriggerInteract()`. In `UpdateAnimation()`: moving branch resets timer; non-moving branch accumulates and fires `idleTrigger` once when threshold crossed. FreeRoam gating is implicit (HandleUpdate only ticks in FreeRoam — noted in doc comment, no GameController.State accessor needed).
- EDIT: `Assets/_Game/Scripts/Player/PlayerController.cs` — `OnInteract()` now calls `playerAnimator?.TriggerInteract()` before `TryInteract()`. No `ResetIdleTimer()` in `OnMove` — relying on the moving-branch reset inside `UpdateAnimation` to avoid double-reset.

**Key decisions (deltas from research doc):**
1. **No GameController.State accessor added.** The research notes implied one was needed for FreeRoam gating, but `PlayerController.HandleUpdate()` is already gated to FreeRoam upstream by `GameController`. `UpdateAnimation()` only ticks when state == FreeRoam already. Kept GameController untouched.
2. **`_idleFired` bool guards trigger re-firing.** Without it, once `_idleTimer >= idleThreshold`, the trigger would fire every frame past the threshold. `_idleFired` flips back to false in `ResetIdleTimer()`.
3. **flipX logic preserved.** Mid-session the mirror file looked like it didn't have the 2026-05-27 flipX edit, so it briefly got dropped. User caught it. Restored: dominant-right movement → `flipX = true`, vertical → `flipX = false`.

**Open questions / next steps:**
- **Next session: editor walkthrough** — rename current `Idle` state → `Standby`, convert to 4-direction blend tree on `moveX`/`moveY` using stood-still clips, add `idleTrigger` + `interactTrigger` params to the Animator, add `Idle_1` state with read-a-book clip, add `Interact` state (single non-directional clip), wire all 7 transitions per the locked table from 2026-05-29 pt.1. Per CLAUDE.md Rule 3, editor setup is its own session.
- **Decision deferred (later):** when expanding Idle from 1 → 2-3 clips, add `idleVariant` int param + per-variant transitions + selection strategy (random / sequential / weighted).
- **Other GDD features still not started:** Phase B-E from 2026-05-27 pt.2 (day/night, loot, initiative context, elite spawns), Skill/Ability, Level/EXP, Party Recruitment, Quest, Item/Inventory, Cultural Encyclopedia, multiple areas/scenes, story/cutscene.

---

## 2026-05-29 — Research: Overworld Animation System Overhaul (Standby / Walking / Idle / Interact)

**Shipped:** Locked architecture for replacing the current 2-state animator (Idle default + Walking blend tree) with a 4-state system. No code written this session; next session implements.

**Files touched:** None (research only). Read for context: `Player/PlayerAnimator.cs`, `Player/PlayerController.cs`, `Party/FollowerController.cs`, `SETUP_GUIDE.md`.

**Locked design decisions:**
1. **Four animator states:** `Standby` (default, blend tree on `moveX`/`moveY` using latched last-facing — same 4-direction setup as walking, just the stood-still clips), `Walking` (existing blend tree, unchanged), `Idle` (single state `Idle_1` for v1), `Interact` (single non-directional clip).
2. **moveX/moveY already persist** as last-facing in `PlayerAnimator.UpdateAnimation()` — Standby blend tree reuses them, no new `faceX`/`faceY` params needed. Right direction continues via existing `SpriteRenderer.flipX` toggle.
3. **Idle timer = 7s default**, exposed as `idleThreshold` on PlayerAnimator. Gated on `GameController.State == FreeRoam` so it doesn't accumulate during Dialog/Battle/Cutscene.
4. **Idle v1 = 1 clip (`Idle_1`), single-facing.** `idleVariant` int param deferred — when scaling to 2-3 clips later, add it back and split the transition. Keeps Animator param list at 5: `isMoving`, `moveX`, `moveY`, `idleTrigger`, `interactTrigger`.
5. **Interact = single clip**, fired via `AnyState → Interact` on `interactTrigger` so it cleanly cancels mid-Idle.
6. **Followers untouched** — they inherit Standby + Walking automatically (same `PlayerAnimator` script). No Idle/Interact for them (driven by leader input only).
7. **Transition table locked.** Key behaviors: Standby↔Walking on `isMoving` flip, Idle_1→Walking on `isMoving==true` with 0s duration (instant cancel), Idle_1→Standby via Exit Time, Interact→Walking on `isMoving==true` (0s) else Interact→Standby on Exit Time.

**Open questions / next steps:**
- **Next session: implement the code** (PlayerAnimator + PlayerController edits per file plan). Code-only per CLAUDE.md Rule 3.
- **Session after that: full editor walkthrough.**
- **Decision deferred to later:** when expanding Idle from 1 → 2-3 clips, re-introduce `idleVariant` int param.

---

## 2026-05-27 (pt. 3) — Phase A shipped: rest action + bone markers

**Shipped:** Phase A from the post-launch enhancement plan. Player can now rest at a `RestPoint` to fully heal the party and respawn every defeated overworld enemy in the region. Bone markers automatically appear at death sites and persist across battle-scene reloads, vanishing only on rest or region-change. Also: Cowork workspace switched from the retired script mirror to the live Unity project at `C:\Users\Fantom\Documents\Unity\DimensiNusantaraV0.1` — direct-edit workflow now (no more manual VSC apply step).

**Files touched:**
- NEW: `Assets/_Game/Scripts/Data/WorldMarkerData.cs` — SO holding bone-marker prefab + Y-offset; designer-swappable per region.
- NEW: `Assets/_Game/Scripts/World/BoneMarker.cs` — trivial visual tag component; stores its source EnemyId.
- NEW: `Assets/_Game/Scripts/World/RestPoint.cs` — trigger interactable; on key press, heals party via `PartySystem.HealAll()`, clears `DefeatedEnemyRegistry`, fires `OnRestTaken`.
- EDIT: `Assets/_Game/Scripts/World/AI/DefeatedEnemyRegistry.cs` — added `defeatPositions` Dictionary, `MarkDefeated(id, pos)` overload, `OnRegistryChanged` event, `DefeatPositions` read-only accessor. Old `MarkDefeated(id)` kept for non-positioned callers.
- EDIT: `Assets/_Game/Scripts/Core/GameController.cs` — added `worldMarkerData` SerializeField, `pendingOverworldDefeatPosition` static field, renamed `SetPendingOverworldEnemy(id)` → `SetPendingOverworldDefeatInfo(id, pos)`, position-aware `MarkDefeated` call in `OnBattleOver`, new `SpawnBoneMarkers()` method called in `OnSceneLoaded` (overworld branch) after the region-clear check.
- EDIT: `Assets/_Game/Scripts/World/AI/States/AttackState.cs` — updated the single call site to pass `enemy.transform.position` to the renamed `SetPendingOverworldDefeatInfo`.
- NEW: `CLAUDE.md` at new Unity project root — Rule 3 rewritten for direct-edit workflow; Code folder pointer changed from `NusantaraRPG_Scripts/` to `Assets/_Game/Scripts/`; architecture list updated to include Phase A files.
- NEW: `PROGRESS.md` at new Unity project root — restored from old workspace; this memo appended.

**Key decisions:**
1. **Two-overload `MarkDefeated`.** Existing `MarkDefeated(id)` (membership-only) preserved; new `MarkDefeated(id, position)` adds both. Only position-aware calls produce bone markers. Lets future scripted-death callers opt out cleanly.
2. **Renamed `SetPendingOverworldEnemy` → `SetPendingOverworldDefeatInfo`.** Only one caller (AttackState) — clean break is safer than a deprecated wrapper. Signature now takes both id and position.
3. **Bone markers spawn AFTER the region-change clear** in `OnSceneLoaded`. Order matters: if the region changed, the registry was just wiped, so iterating it produces zero markers. Prevents ghost markers from a previous region.
4. **`SpawnBoneMarkers()` no-ops silently when `worldMarkerData` is null** or its prefab slot empty. Bone markers are polish — game runs fine without them.
5. **RestPoint uses legacy `Input.GetKeyDown(KeyCode.E)`.** Phase D will lock the input system choice; for Phase A, legacy works alongside the new Input System if both are enabled. KeyCode is `[SerializeField]` so designers can rebind without code.

**Open questions / next steps:**
- **Next session: editor walkthrough for Phase A** (if wanted). Cover: (1) make a `BoneMarker` prefab — tiny sprite/mesh placeholder + `BoneMarker.cs` on root; (2) make a `WorldMarkerData` SO asset — Right-click in Project → RPG → World Marker Data, drag the prefab into `boneMarkerPrefab`; (3) drag the SO into GameController's new `World Marker Data` slot; (4) make a `RestPoint` GameObject — BoxCollider with Is Trigger on, `RestPoint.cs`, optional visual child for the campfire/inn; (5) test the loop — defeat overworld enemy → see bones on return → rest → bones gone, enemies back.
- **Phase B (day/night cycle + spawn conditions)** is the next code phase. `RestPoint.OnRestTaken` is already exposed for `TimeOfDay` to subscribe to.
- **Other GDD features still not started:** Skill/Ability, Level/EXP, Party Recruitment, Quest, Item/Inventory (Phase C creates a placeholder hook), Cultural Encyclopedia, multiple areas/scenes, story/cutscene.

---

## 2026-05-27 (pt. 2) — Editor walkthrough + post-launch enhancement plan

**Shipped:** Verbal walkthrough delivered for prefab-ising the overworld enemy and authoring additional instances cleanly (covers prefab creation, clearing per-instance fields on the prefab, sibling `PatrolPoints` groups, EnemyId uniqueness, sanity checks before Play). Locked a 5-phase plan for 7 enhancement features: rest action, bone markers, day/night cycle + spawn, overworld loot drops, pre-emptive strike, ambush, elite spawns. No code written this session — design only.

**Files touched:** None this session (PROGRESS.md memo only).

**Key decisions:**
1. **Respawn = rest action**, not wall-clock timer. RestPoint clears `DefeatedEnemyRegistry`, heals party, and advances `TimeOfDay`. Player-driven world reset, fits RPG genre conventions (bonfire/inn pattern). Avoids the immersion break of seeing the same enemy reappear in real time.
2. **Time-of-day system has no real clock** — toggled exclusively by rest action for now. Avoids tick/save bookkeeping; gives the rest action meaningful narrative weight (sleep → night becomes day, world resets, kuntilanak-tier spooks vanish).
3. **Battle initiative gets a `BattleStartContext` enum** (Normal / PlayerAdvantage / EnemyAdvantage) to support pre-emptive strike (player attacks enemy from outside its vision cone → free first turn / bonus damage) and ambush (enemy catches player from behind in close-radius → enemy free turn). Default path stays Normal so existing battle flow is untouched.
4. **Inventory deferred** — overworld loot drops use a placeholder `PartyResources` SO (gold-only initially) until the full Item/Inventory GDD feature ships. Upgrade path is just adding an `items` dictionary to the SO and an `Item` LootType.
5. **Randomized enemy pool per spawn point dropped from this batch.** Stay with single-instance enemies + elite-chance roll. Pool can be folded into Phase E later if wanted.
6. **RestPoints are standalone interactables** in the world (not attached to NPCs). NPC-based rest (innkeeper) can come later as a thin wrapper around the same component.
7. **Unclaimed loot pickups persist across scene reloads** — stored in the registry alongside bone markers so the player can't lose loot to a battle scene reload.

**Phased execution plan (one phase per session):**
- **Phase A — Persistence polish (rest action + bone markers).** NEW: `World/RestPoint.cs`, `World/BoneMarker.cs`, `Data/WorldMarkerData.cs` (SO). EDIT: `World/AI/DefeatedEnemyRegistry.cs` (defeat-position dict, public `Clear()`, `OnRegistryChanged` event), `Core/GameController.cs` (cache defeat position before scene reload, spawn markers on scene load).
- **Phase B — Day/night cycle + spawn conditions.** NEW: `Core/TimeOfDay.cs` (singleton, `Phase` enum, `Advance()`, `OnPhaseChanged`), `World/DayNightVisuals.cs` (tint directional light + ambient). EDIT: `Data/EnemyAIData.cs` (SpawnTime enum field), `World/AI/OverworldEnemyController.cs` (self-deactivate on mismatch in Awake), `World/RestPoint.cs` (call `TimeOfDay.Advance()`).
- **Phase C — Loot drops + minimal resource system.** NEW: `Data/LootTableData.cs`, `Data/LootEntry.cs`, `Data/PartyResources.cs` (SOs), `World/LootPickup.cs`. EDIT: `Data/EnemyAIData.cs` (LootTable reference), `Core/GameController.cs` (roll & spawn on battle win), `World/AI/DefeatedEnemyRegistry.cs` (also track unclaimed loot for persistence).
- **Phase D — Battle initiative context (stealth + ambush).** EDIT: `Battle/BattleSystem.cs` (BattleStartContext enum, threading + first-turn/damage branch), `Core/GameController.cs` (pass-through), `World/AI/OverworldEnemyController.cs` (`IsPlayerBehindMe` helper), `Player/PlayerController.cs` (attack input binding + stealth-attack trigger), `World/AI/States/AttackState.cs` (ambush branch when player caught from behind).
- **Phase E — Elite spawns.** NEW: `Data/EliteModifierData.cs` (SO: stat multipliers, visual tint/scale, aura prefab, loot override). EDIT: `Data/EnemyAIData.cs` (eliteChance + EliteModifierData ref), `World/AI/OverworldEnemyController.cs` (Awake roll-and-apply: tint material, scale transform, optional aura child), `Core/GameController.cs` (pass elite flag through to battle for stat scaling).

**Open questions / next steps:**
- **Next session: Phase A.** Implement RestPoint + BoneMarker + registry extensions. Code-only per CLAUDE.md Rule 3. Editor walkthrough for Phase A becomes its own follow-up session once the code is in.
- **Pre-Phase D decision to lock before that session:** which input binds to the stealth-attack action? Candidates: Spacebar (currently free?), E (interact-style), F (action-style), or mouse right-click. Should also confirm whether Phase D uses Unity's old Input Manager or the new Input System package (whichever `PlayerController` is already using — confirm before edits).
- **Pre-Phase E decision (further out):** keep single-instance enemies + elite-chance roll only, or add the random spawn pool when we get to elite work?
- **GDD features still not started:** Skill/Ability, Level/EXP, Party Recruitment, Quest, Item/Inventory (Phase C creates a placeholder hook), Cultural Encyclopedia, multiple areas/scenes (when `DefeatedEnemyRegistry.Clear()` on region change actually fires meaningfully), story/cutscene.

---

## 2026-05-27 — Implementation: Overworld Patrolling Enemies (all 5 steps)

**Shipped:** All five steps from the 2026-05-25 design plan. Visible overworld enemies now patrol waypoints, detect via cone + close radius + LOS, chase with a lose-sight grace, investigate the last-known spot with a head-swing, trigger battle on touch via the existing `TriggerEncounter` pipeline, and stay defeated across scene reload via a static registry. Also: filled-mesh vision cone, alert/question bubbles, sprite-flip for right-walk, and a fixed-world-rotation helper.

**Files touched:**
- NEW: `Data/EnemyAIData.cs`
- NEW: `World/AI/IEnemyState.cs`
- NEW: `World/AI/OverworldEnemyController.cs`
- NEW: `World/AI/EnemyPerception.cs`
- NEW: `World/AI/AlertBubble.cs`
- NEW: `World/AI/VisionConeRenderer.cs` (mesh fan, not LineRenderer)
- NEW: `World/AI/LockWorldRotation.cs` (sprite billboard helper)
- NEW: `World/AI/DefeatedEnemyRegistry.cs`
- NEW: `World/AI/States/IdleState.cs`
- NEW: `World/AI/States/PatrolState.cs`
- NEW: `World/AI/States/ChaseState.cs`
- NEW: `World/AI/States/InvestigateState.cs`
- NEW: `World/AI/States/AttackState.cs`
- EDIT: `Core/GameController.cs` — added `State` accessor, `pendingOverworldEnemyId` + `lastOverworldSceneName` static fields, `SetPendingOverworldEnemy` method, registry register-on-win in `OnBattleOver`, registry clear-on-region-change in `OnSceneLoaded`.
- EDIT: `Player/PlayerAnimator.cs` — cached `SpriteRenderer`, `flipX = true` on dominant-right movement, `false` on vertical.

**Key decisions (deltas from the research doc):**
1. **Vision cone is a procedural mesh, not LineRenderer.** LineRenderer only draws strokes — user wanted a filled pie slice. `VisionConeRenderer` now builds a triangle fan in local space, vertex-color tinted by state. Requires MeshFilter + MeshRenderer + a two-sided transparent material (Sprites-Default works).
2. **`EnemyAIData.touchRange` default raised 0.9 → 1.2.** With player CC radius 0.4 + enemy CC radius 0.5 = 0.9, the capsules wedge at exactly the old threshold and the `<=` distance check lost the float-precision race. Tooltip now spells out the rule.
3. **Sprite billboard via `LockWorldRotation`.** Parent rotation drives `transform.forward` for the perception cone (and *must* keep rotating), so the sprite has its own component that pins world rotation in `LateUpdate`. Reusable for any "body rotates but visual stays" case.
4. **Right-walk = flipped left clip via `SpriteRenderer.flipX`.** Cleaner than scaling the transform (no child side effects). Vertical motion resets flipX to false so Up/Down clips never render mirrored.
5. **`OnStateChanged` event dropped — `CurrentState` getter is enough.** The design doc listed an event, but the only consumer (VisionConeRenderer) polls every LateUpdate anyway. Skipped to avoid an event with one subscriber.

**Open questions / next steps:**
- **Next session: in-Unity editor walkthrough.** Per CLAUDE.md Rule 3, the editor setup gets its own session. Cover: creating the `EnemyAI_*` SO assets, building the enemy GameObject hierarchy (root + CC + Sprite child with LockWorldRotation + AlertBubble child with two visual children + VisionCone child with MeshFilter/Renderer), placing the WP_0..WP_N waypoints under a sibling `PatrolPoints` GameObject (not under the prefab), setting up the `SightObstacle` layer on walls, assigning unique `EnemyId` per instance, and saving the prefab variant. The user already has *one* enemy stood up and working — the walkthrough should focus on prefab-ising it and authoring a second one cleanly.
- **Optional polish punch list (do later if needed):** lift `InvestigateState.SwingDegrees` (60°) onto the SO for per-enemy tuning; expose alert/question bubble visuals via the SO (so multiple enemy types can share one bubble prefab); add a SpriteRenderer mirror to enemies (same trick as `PlayerAnimator.flipX`) once they get a directional walk animator.
- **Other GDD features still not started:** Skill/Ability, Level/EXP, Party Recruitment, Quest, Item/Inventory, Cultural Encyclopedia, multiple areas/scenes (which is when `DefeatedEnemyRegistry.Clear()` on region change actually fires), story/cutscene.

---

## 2026-05-25 — Research: Overworld Patrolling Enemies (SO + FSM AI)

**Shipped:** Locked architecture and full design for visible overworld enemies that patrol, detect, chase, and touch-trigger battles — alongside the existing grass-style `EncounterTrigger`. No code written this session; next session implements.

**Files touched:** None (research only). Read for context: `EncounterTrigger.cs`, `NPCController.cs`, `EnemyEncounterData.cs`, `PlayerController.cs`, `GameController.cs`, `BattleSystem.cs`.

**Locked design decisions:**
1. **FSM = hybrid SO + C# state classes.** One `EnemyAIData` SO holds tuning data only (speed, vision, patrol, alerts, touchRange, `encounterToTrigger`). Five C# state classes implement `IEnemyState` (Idle, Patrol, Investigate, Chase, Attack). State instances created once in `Awake`, reused — zero per-tick allocations.
2. **Detection = vision cone (range + half-angle) + 360° close-radius + LOS raycast** against a `sightObstacles` LayerMask. Facing direction = last non-zero movement vector, rotated via `Quaternion.Slerp`.
3. **Patrol = Transform-array waypoints**, wired per-instance in scene (NOT prefab children, to avoid prefab serialization trap). PatrolMode enum on SO: Loop / PingPong. `idleWaitAtWaypoint` pause between segments.
4. **Investigate = walk to last-known player position, look-around for `investigateLookAroundTime` seconds, then return to Patrol.** `loseSightGracePeriod` (~0.5s) prevents corner-flicker thrashing between Chase and Investigate.
5. **Movement = `CharacterController`** (matching `PlayerController` exactly — same XZ-plane, same gravity pattern). Project is 3D-with-top-down-camera, not 2D. Touch-trigger uses per-frame `Vector3.Distance` against `touchRange`, not a collider.
6. **Alerts = `!` bubble on Idle/Patrol→Chase + `?` bubble on Chase→Investigate + in-game vision cone gizmo** (LineRenderer fan, color-tinted by state). Placeholder is empty UI prefabs; re-skinnable later.
7. **Defeated-enemy persistence = `DefeatedEnemyRegistry` static class with `HashSet<string>` of unique IDs, lives on `GameController` (`DontDestroyOnLoad`).** Critical because *every battle reloads the overworld scene*, so naive "despawn this GameObject" would respawn the enemy on return. On Awake, each enemy checks the registry and self-deactivates if its ID is in it. On battle win, the originating enemy's ID is added before scene reload. Registry clears on scene-change to a non-battle scene (different region/area), so revisiting an old area re-populates it.
8. **Integration hooks confirmed (no new APIs needed in battle code):** `BattleSystem.OnBattleOver(bool playerWon)` already exists — registry add happens in `GameController.OnBattleOver` handler when `playerWon == true` and a `currentOverworldTrigger` is cached. `GameController.GameState` enum already covers `FreeRoam/Battle/Dialog/Cutscene` — just add a `State` accessor + `OnStateChanged` event so enemies pause their FSM cleanly when not in FreeRoam.

**File plan (for next session):**
```
NusantaraRPG_Scripts/
  Data/
    EnemyAIData.cs                ← new SO, [CreateAssetMenu("RPG/Enemy AI Data")]
  World/
    AI/
      IEnemyState.cs
      OverworldEnemyController.cs ← MonoBehaviour, CharacterController-based
      EnemyPerception.cs           ← pure C# class, cone + radius + LOS
      AlertBubble.cs                ← scale-in/hold/scale-out popup helper
      DefeatedEnemyRegistry.cs     ← static HashSet<string>
      States/
        IdleState.cs
        PatrolState.cs
        InvestigateState.cs
        ChaseState.cs
        AttackState.cs
```
Surgical edits: `GameController.cs` — add `State` property + `OnStateChanged` event + clear `DefeatedEnemyRegistry` on overworld scene transitions to non-battle scenes. No changes to `PlayerController.cs`, `EncounterTrigger.cs`, `BattleSystem.cs`, `EnemyEncounterData.cs`.

**Recommended implementation order (one feature per "first PR"):**
1. `EnemyAIData` SO + `IEnemyState` + `OverworldEnemyController` skeleton + `IdleState` + `PatrolState` — get a patroller walking waypoints in the scene with no perception yet.
2. Add `EnemyPerception` + `ChaseState` + `AttackState` — connect to existing `TriggerEncounter` flow.
3. Add `InvestigateState` + `loseSightGracePeriod` polish.
4. Add `AlertBubble` + in-game cone gizmo.
5. Add `DefeatedEnemyRegistry` + GameController hook.

**Open questions / next steps:**
- **Next session: write the code** for files in the plan above. Paste this design doc and lock the implementation order. Deliver short Inspector notes per file (per workflow rules), NOT a full editor setup guide.
- **Session after that: full editor walkthrough** — step-by-step in-Unity tutorial covering: creating the `EnemyAIData` SO assets, building the enemy GameObject hierarchy (root + CharacterController + child sprite + child waypoint markers + alert bubble pivot), the alert bubble UI prefab, the vision cone LineRenderer setup, layer setup for `sightObstacles`, and wiring the `GameController` reference. Per CLAUDE.md Rule 3 + workflow preference #3: editor setup is a dedicated session, not mixed with code requests.
- Other GDD features still not started: Skill/Ability, Level/EXP, Party Recruitment, Quest, Item/Inventory, Cultural Encyclopedia, multiple areas/scenes, story/cutscene.

---

## 2026-05-22 — Battle System Features: Parry (Osu-style), Target Selector, Parry Counter-Attack

**Shipped:** Three major battle systems fully coded and ready for Unity wiring.

**Files touched:**
- `Assets/_Game/Scripts/Battle/ParryButton.cs` — full rewrite (Osu-style approach ring, sequential tap)
- `Assets/_Game/Scripts/Battle/ParrySystem.cs` — full rewrite (sequential circles, all must be tapped, fail-fast)
- `Assets/_Game/Scripts/Battle/TargetSelector.cs` — new file (enemy target picker UI)
- `Assets/_Game/Scripts/Battle/BattleSystem.cs` — multiple edits (HandleAttack, EnemyTurn, PerformParryCounter, CheckFainted)

**Key decisions:**
1. Parry is Osu-style: circles appear one at a time, each with shrinking approach ring. Player must tap ALL circles; missing any one immediately fails the parry.
2. Successful parry = 0 damage taken + player fires a counter-attack (its own damage calc via `parryCounterMultiplier = 1.5f`). Old `parryDamageReduction` removed.
3. Attack targeting: if multiple enemies alive, show `TargetSelector` UI panel. If only one alive, auto-target. `TargetSelector` uses `Action<BattleUnit>` callback so BattleSystem doesn't need to poll.

**Open questions / next steps:**
- Unity prefab wiring still needed for all three systems (see paste note below for full details).
- GDD features not yet started: Skill/Ability system, Level/EXP, Party Recruitment, Quest, Item/Inventory, Cultural Encyclopedia, multiple areas/scenes, story/cutscene.

---

## 2026-05-22 — Workflow setup

**Shipped:** Established collaboration rules for the project.
**Files touched:**
- `CLAUDE.md` (new) — project memory: workflow rules, Unity-noise blocklist, handover protocol, current architecture.
- `PROGRESS.md` (new) — this log.

**Key decisions:**
- Claude will proactively suggest "memo + new session" at natural break points (feature done, ~20 turns, topic shift, heavy context).
- Claude will not read Unity-generated folders, `.meta`, `.unity`, `.prefab`, `.asset` files unless explicitly asked.
- Code edits happen in `NusantaraRPG_Scripts/`; user applies them manually in Unity via VSC. Output one-line summary per file.

**Open questions / next steps:**
- None for workflow. Next session: actual game dev work — start by reading `CLAUDE.md` and the latest entry here.
