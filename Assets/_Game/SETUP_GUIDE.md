# Nusantara RPG — Unity Setup Guide
### Compatible with: Unity 6 (6000.x) ✅  |  Unity 2022.3 LTS ✅

This guide walks you through setting up the full project from scratch using the provided scripts.

---

## 1. CREATE THE UNITY PROJECT

1. Open **Unity Hub → New Project**
2. Select template: **3D (Core)**
3. Editor Version: **Unity 6 (6000.3.6f1)** or **2022.3.x LTS** — both work
4. Name it `NusantaraRPG`

---

## 2. INSTALL PACKAGES

Go to **Window → Package Manager → Unity Registry** and install:

| Package | Unity 6 | Unity 2022.3 |
|---|---|---|
| **Input System** | Install from Package Manager | Install from Package Manager |
| **TextMeshPro** | ✅ Built-in — no install needed | Install from Package Manager |
| **Cinemachine** *(optional)* | Install from Package Manager | Install from Package Manager |

> **Unity 6 note:** TextMeshPro is included by default — skip installing it separately.
> After installing the Input System, Unity will ask you to restart — click **Yes**.

---

## 3. IMPORT THE SCRIPTS

1. In your Unity project, go to `Assets/` and create this folder structure:

```
Assets/
  Scripts/
    Core/       ← GameController.cs, Fader.cs
    Data/       ← CharacterData.cs, EnemyEncounterData.cs
    Player/     ← PlayerController.cs, PlayerAnimator.cs
    Party/      ← PartySystem.cs, PartyMember.cs, FollowerController.cs
    World/      ← EncounterTrigger.cs, NPCController.cs
    Battle/     ← BattleSystem.cs, BattleUnit.cs, BattleHud.cs, BattleDialogBox.cs
```

2. Copy all `.cs` files from this folder into the matching locations above.

---

## 4. CREATE SCRIPTABLE OBJECTS

### Character Data (one per character/enemy)
- Right-click in `Assets/_Game/Data/Characters/` → **Create → RPG → Character Data**
- Fill in: Name, MaxHp, Attack, Defense, Speed
- **BattleAnimator** → drag your Battle Animator Controller here *(see Section 10A)*
- **OverworldAnimator** → drag your Overworld Animator Controller here *(see Section 10B)*
- **BattleSprite** → drag a sprite here if you're not using a 3D model in battle *(optional)*

> 💡 **No art yet?** Leave BattleAnimator and BattleSprite empty for now — the game will still compile and run, just without visible animations. You can add them later.

### Enemy Encounter Data (one per area/zone)
- Right-click in `Assets/_Game/Data/Encounters/` → **Create → RPG → Enemy Encounter**
- Add enemy entries with CharacterData and spawn weights
- Set min/max enemies per encounter

---

## 5. CREATE THE INPUT ACTIONS ASSET

1. Right-click in `Assets/` → **Create → Input Actions**
2. Name it `PlayerInputActions`
3. Double-click to open it
4. Add an **Action Map** called `Player`
5. Add these actions:

| Action | Type | Control Type |
|---|---|---|
| Move | Value | Vector2 |
| Interact | Button | — |

6. Bind `Move` to **WASD** and **Left Stick**
7. Bind `Interact` to **E** and **South Button (gamepad)**
8. Click **Save Asset**

---

## 6. BUILD THE OVERWORLD SCENE

### Scene name must be: `Overworld`

### 6a. Terrain
- **GameObject → 3D Object → Terrain**
- Paint some grass texture on it

### 6b. Player GameObject
- Create an empty GameObject called `Player`
- Add components:
  - `CharacterController` (Height: 2, Radius: 0.4)
  - `PlayerController` script
  - `PartySystem` script — assign your starting CharacterData SOs
  - `PlayerInput` (New Input System):
    - Assign your `PlayerInputActions` asset
    - Behavior: **Send Messages**
    - Default Map: `Player`
- Add a child GameObject with your player model/sprite
  - Add `PlayerAnimator` script to the child
  - Add an `Animator` component with a controller that has these parameters:
    - `isMoving` (Bool)
    - `moveX` (Float)
    - `moveY` (Float)

### 6c. Camera
- Add a **Cinemachine Virtual Camera** (or just parent your Main Camera to the Player)
- If using Cinemachine: set Follow = Player transform

### 6d. GameController (IMPORTANT — only in Overworld scene)
- Create an empty GameObject called `GameController`
- Add `GameController` script
- Set `overworldSceneName` = `"Overworld"`, `battleSceneName` = `"Battle"`

### 6e. Fader Canvas
- Create a **Canvas** (Screen Space - Overlay, Sort Order: 999) on the GameController
- Add an **Image** child that fills the entire canvas
  - Color: Black (R:0, G:0, B:0, A:0 to start transparent)
- Add the `Fader` script to the GameController — assign the Image

### 6f. Grass / Encounter Zones
- Create a flat **Cube** stretched over the grass area → scale X/Z wide, Y thin (e.g. 0.1)
- Remove the MeshRenderer (or make it invisible)
- Add a `BoxCollider` → check **Is Trigger**
- Add the `EncounterTrigger` script
- Assign your `EnemyEncounterData` SO
- Set the layer to `Default` (or a custom "Grass" layer)

---

## 7. BUILD THE BATTLE SCENE

### Scene name must be: `Battle`

### 7a. Spawn Points
Create empty GameObjects as position markers:

```
BattleScene/
  PlayerSpawnPoints/
    SpawnPoint_0   ← left side of screen
    SpawnPoint_1
    SpawnPoint_2
  EnemySpawnPoints/
    SpawnPoint_0   ← right side of screen
    SpawnPoint_1
    SpawnPoint_2
```

### 7b. BattleUnit Prefab
Create this prefab in `Assets/Prefabs/`:

```
BattleUnit (empty GameObject)
  ├── Model (your sprite or 3D mesh + Animator)
  └── HudCanvas (World Space Canvas)
      ├── NameText (TextMeshPro)
      ├── HpSlider (Slider, Min=0, Max=1)
      └── HpText (TextMeshPro)
```

- Add `BattleUnit` script to the root — assign `BattleHud`
- Add `BattleHud` script to the HudCanvas — assign NameText, HpSlider, HpText

### 7c. Battle System GameObject
- Create an empty `BattleSystem` GameObject
- Add the `BattleSystem` script
- Assign: playerSpawnPoints, enemySpawnPoints, battleUnitPrefab

### 7d. Battle UI Canvas
Create a Screen Space UI Canvas:

```
BattleUI (Canvas)
  ├── DialogPanel
  │   └── DialogText (TextMeshPro)
  ├── ActionPanel
  │   ├── AttackButton (Button + Text "Attack")
  │   └── RunButton (Button + Text "Run")
```

- Add `BattleDialogBox` script to BattleUI
- Assign: dialogText, actionPanel, attackButton, runButton
- Wire attackButton.onClick → BattleDialogBox (handled automatically in script)

---

## 8. ADD BOTH SCENES TO BUILD SETTINGS

1. **File → Build Settings**
2. Click **Add Open Scenes** for both `Overworld` and `Battle`
3. Make sure `Overworld` is index 0 (top of the list)

---

## 9. SET UP NPCs (Optional — Party Recruitment)

For each recruitable NPC:
1. Create a character GameObject in the Overworld
2. Add `NPCController` script
3. Assign `CharacterData`, enable `canJoinParty`
4. Assign a `followerPrefab` (a prefab with `FollowerController` + CharacterController + model)
5. Set the layer to match PlayerController's `npcLayer` mask
6. In PlayerController's Inspector, set `npcLayer` to the NPC layer

---

## 10. ANIMATOR SETUP

You need **two separate Animator Controllers** per character — one for the overworld, one for battle.
Both live in `Assets/_Game/Art/Animations/Characters/`.

> 💡 **No art yet?** You can create these controllers with empty states now and assign
> animation clips later. The game logic will still run perfectly without clips.

---

### 10A. BATTLE ANIMATOR CONTROLLER
*(Controls Attack, Hit, and Faint animations in the Battle scene)*

**Step 1 — Create the controller**
Right-click `_Game/Art/Animations/Characters/` → **Create → Animator Controller**
Name it e.g. `Warrior_Battle` (make one per character type)

**Step 2 — Open the Animator window**
Double-click the controller → the **Animator** window opens

**Step 3 — Add Parameters**
In the Animator window, click the **Parameters** tab (top-left) → click **+** three times:

| Name | Type |
|---|---|
| `Attack` | Trigger |
| `Hit` | Trigger |
| `Faint` | Trigger |

**Step 4 — Create States**
Right-click anywhere in the grid → **Create State → Empty** and create four states:

| State name | Extra step |
|---|---|
| `Idle` | Right-click it → **Set as Layer Default State** (turns orange) |
| `Attack` | — |
| `Hit` | — |
| `Faint` | — |

**Step 5 — Create Transitions**
Right-click a state → **Make Transition** → click the target state.
Set up these transitions:

| From | To | Condition | Has Exit Time |
|---|---|---|---|
| Idle | Attack | Attack *(trigger)* | ☐ No |
| Attack | Idle | *(none)* | ☑ Yes (plays to end) |
| Idle | Hit | Hit *(trigger)* | ☐ No |
| Hit | Idle | *(none)* | ☑ Yes |
| Idle | Faint | Faint *(trigger)* | ☐ No |

To set these: click the transition arrow → in the **Inspector**:
- Uncheck **Has Exit Time** where the table says No
- Under **Conditions**, click **+** and choose the trigger

**Step 6 — Assign animation clips** *(skip if no art yet)*
Click each state → in the Inspector, drag an animation clip into the **Motion** field.

---

### 10B. OVERWORLD ANIMATOR CONTROLLER
*(Controls Idle and 4-directional walking in the Overworld scene)*

**Step 1 — Create the controller**
Right-click `_Game/Art/Animations/Characters/` → **Create → Animator Controller**
Name it e.g. `Player_Overworld`

**Step 2 — Add Parameters**
Click **Parameters** tab → click **+** three times:

| Name | Type |
|---|---|
| `isMoving` | Bool |
| `moveX` | Float |
| `moveY` | Float |

**Step 3 — Create States**
- Right-click grid → **Create State → Empty** → name it `Idle` → **Set as Layer Default State**
- Right-click grid → **Create State → From New Blend Tree** → name it `Walk`

**Step 4 — Set up the Blend Tree**
Double-click the `Walk` state to enter it. You'll see a Blend Tree node — click it, then in the Inspector:

1. Change **Blend Type** to `2D Simple Directional`
2. Set **Parameters** to `moveX` and `moveY`
3. Click **+** four times → **Add Motion Field**
4. Assign your four walk direction clips:

| Motion | Pos X | Pos Y |
|---|---|---|
| Walk_Right *(clip)* | 1 | 0 |
| Walk_Left *(clip)* | -1 | 0 |
| Walk_Up *(clip)* | 0 | 1 |
| Walk_Down *(clip)* | 0 | -1 |

> If you have no clips yet, leave the Motion fields empty — add them later.

Click the breadcrumb **Base Layer** at the top to go back to the main graph.

**Step 5 — Create Transitions**

| From | To | Condition | Has Exit Time |
|---|---|---|---|
| Idle | Walk | `isMoving` = true | ☐ No |
| Walk | Idle | `isMoving` = false | ☐ No |

---

### 10C. ASSIGNING CONTROLLERS TO YOUR CHARACTER DATA

Once both controllers are created:
1. Open your **CharacterData** ScriptableObject in `_Game/Data/Characters/`
2. **Overworld Animator** field → drag your `Player_Overworld` controller
3. **Battle Animator** field → drag your `Warrior_Battle` controller

Each enemy character gets its **own** Battle Animator Controller (so they can have different attack animations). Party members each get their own Overworld Animator too.

---

### 10D. ADDING THE ANIMATOR TO YOUR CHARACTER PREFAB

The Animator component needs to live on your character model:

1. Select your player/character GameObject (or open the prefab)
2. On the **model child** (not the root), add an **Animator** component
3. Leave the **Controller** field empty — `PlayerController.cs` and `BattleUnit.cs`
   will assign it automatically at runtime from the CharacterData SO

---

## 11. CAMERA SETUP FOR BATTLE

The Battle scene needs a fixed camera that shows both sides:
- Position it above and slightly angled to see all spawn points
- Alternatively use a dedicated Battle Camera GameObject set to Orthographic

---

## 12. SCRIPT REFERENCE SUMMARY

| Script | Attach to | Key References to Assign |
|---|---|---|
| `GameController` | GameController (Overworld only) | Fader |
| `Fader` | GameController | fadeImage (black UI Image) |
| `PlayerController` | Player | npcLayer |
| `PlayerAnimator` | Player model child | — |
| `PartySystem` | Player | startingPartyData (list of CharacterData SOs) |
| `EncounterTrigger` | Grass zone | encounterData |
| `NPCController` | NPC | characterData, followerPrefab |
| `FollowerController` | Follower prefab | — (SetLeader called at runtime) |
| `BattleSystem` | BattleSystem object | spawnPoints, battleUnitPrefab, dialogBox |
| `BattleUnit` | BattleUnit prefab root | hud (BattleHud reference) |
| `BattleHud` | HudCanvas child | nameText, hpSlider, hpText |
| `BattleDialogBox` | BattleUI Canvas | dialogText, actionPanel, buttons |
| `CharacterData` | ScriptableObject asset | All stat fields |
| `EnemyEncounterData` | ScriptableObject asset | possibleEnemies list |

---

## QUICK PLAY CHECKLIST

- [ ] Both scenes added to Build Settings
- [ ] Overworld scene has: Player, GameController, Fader, Grass trigger zones
- [ ] Battle scene has: BattleSystem, spawn points, BattleUnit prefab assigned
- [ ] At least one CharacterData SO created for the player
- [ ] At least one CharacterData SO created for an enemy
- [ ] At least one EnemyEncounterData SO created and assigned to EncounterTrigger
- [ ] InputActions asset created with Move + Interact actions
- [ ] PlayerInput component on Player with the InputActions asset assigned
- [ ] TextMeshPro Essentials imported (Unity will prompt you the first time)

---

Good luck building Nusantara! 🎮
