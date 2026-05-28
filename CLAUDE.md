# CLAUDE.md — Nusantara Game (Dimensi Nusantara)

**Read this file at the start of every session.** It is the source of truth for how to collaborate on this project. If anything here is outdated, update it before doing other work.

---

## Project at a glance

- **Title:** Dimensi Nusantara (Nusantara RPG)
- **Engine:** Unity (C#)
- **Genre:** RPG with party system, turn-based battle, world exploration
- **Cultural setting:** Indonesian / Nusantara
- **Code folder:** `Assets/_Game/Scripts/` — edited directly in the Unity project at `C:\Users\Fantom\Documents\Unity\DimensiNusantaraV0.1`. Cowork's selected folder is this Unity project root.
- **Docs:** GDD, Storyboard, Production Plan, Proposal files at project root (`.docx`)

---

## Workflow rules (token efficiency)

These exist so we don't burn usage on overhead. Follow them by default.

### Rule 1 — Proactively suggest handover at session boundaries

When a session is getting long or has reached a natural break, **suggest the user end this session and start a new one**. Don't wait to be asked.

Triggers to suggest a handover:
- We've completed a feature or logical chunk of work (natural break).
- The conversation has gone past ~20 substantive turns.
- We've shifted topics significantly (e.g., from combat code to UI design).
- Context is getting heavy with files, search results, or large outputs.
- The user is about to ask for something unrelated to the current thread.

How to suggest it (be brief):
> "We've wrapped up [X]. Good time to hand off — want me to write a memo to `PROGRESS.md` and you start a fresh session for the next thing?"

The memo format (append to `PROGRESS.md`):
- **Date** + one-line summary of what shipped.
- **Files touched** (paths, one line each).
- **Key decisions** made (1–3 bullets).
- **Open questions / next steps** (so the next session knows where to pick up).
Keep it tight — a memo is for reloading state, not for storytelling.

### Rule 2 — Keep Unity noise out of context

NEVER read these unless the user explicitly points at them:
- `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/` — Unity-generated, huge, useless.
- `*.meta` files — Unity metadata, not relevant to gameplay logic.
- `*.unity` (scene files) — huge YAML blobs. Only open if debugging a specific scene serialization issue.
- `*.prefab`, `*.asset` — similar YAML. Skip unless the user is debugging serialization.
- `Packages/`, `ProjectSettings/` — only if specifically asked about package or project config.

When searching or globbing, restrict to `Assets/_Game/Scripts/**/*.cs` by default. The Unity project root contains massive `Library/PackageCache/` content that poisons unfiltered globs.

### Rule 3 — Code workflow (direct edit)

- We edit Unity scripts directly in `Assets/_Game/Scripts/`. The old `NusantaraRPG_Scripts/` mirror folder is retired — Cowork's selected folder is now the Unity project root, so writes land where Unity expects them.
- **⚠️ Unity must be out of Play Mode when I edit scripts.** Unity recompiles on focus, and editing during Play Mode either no-ops the change or corrupts in-memory state. Always confirm Play Mode is stopped before script edits.
- For edits: use the `Edit` tool with the smallest possible diff. Don't rewrite whole files for small changes.
- After editing, output a **one-line summary per file**: what changed and where (method name / line region).
  - Example: `PlayerController.cs: added dash cooldown — new field _dashTimer, hooked into Update() around line 45.`
- Don't paste the full file back unless the user asks.

### Rule 4 — Be specific about files; don't explore blindly

- If the user names a file, go straight to it. No grep/glob sweep.
- If a file isn't specified and you need to find one, do ONE targeted search restricted to `Assets/_Game/Scripts/`, not a wide exploration.
- Don't re-read a file you've already loaded earlier in the same session.

### Rule 5 — Batch related work, split unrelated work

- Three related tweaks in one prompt → handle in one pass.
- Unrelated work → suggest a session split (see Rule 1).

### Rule 6 — Match tool to task

Use Claude for: new systems, refactors, debugging from stack traces, architecture trade-offs, boilerplate (ScriptableObjects, data classes, editor scripts), test code, design discussion.

Skip Claude for: Inspector tweaks, scene composition, asset imports, build settings, quick syntax lookups — these are faster done manually.

### Rule 7 — Don't edit while Unity is in Play Mode

Repeated for emphasis (also in Rule 3): if the user mentions Unity is running or in Play Mode, ask them to stop Play Mode before script edits.

---

## Current architecture (keep updated)

Folder layout inside `Assets/_Game/Scripts/`:

- `Core/` — `GameController`, `Fader` (state, scene transitions)
- `Player/` — `PlayerController`, `PlayerAnimator`
- `Party/` — `PartySystem`, `PartyMember`, `FollowerController`
- `Battle/` — `BattleSystem`, `BattleUnit`, `BattleHud`, `BattleDialogBox`
- `World/` — `NPCController`, `EncounterTrigger`, `RestPoint` (Phase A), `BoneMarker` (Phase A)
- `World/AI/` — `OverworldEnemyController`, `EnemyPerception`, `AlertBubble`, `VisionConeRenderer`, `LockWorldRotation`, `DefeatedEnemyRegistry`, `States/*`
- `Data/` — `CharacterData`, `EnemyEncounterData`, `EnemyAIData`, `WorldMarkerData` (Phase A) (ScriptableObjects)

---

## PROGRESS.md log

Running session-by-session memo log is in `PROGRESS.md` (sibling file at project root). Append; don't rewrite.
