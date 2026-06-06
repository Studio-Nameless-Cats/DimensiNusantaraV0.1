using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Nusantara.UI.Motion;

// The big boss manager. It sticks around across scenes (DontDestroyOnLoad), runs the
// game's state machine, and handles bouncing between the Overworld and Battle scenes.
//
// Works on: Unity 6 (6000.x) and Unity 2022.3 LTS
//
// Setup:
//   1. Make an empty GameObject in the Overworld scene called "GameController".
//   2. Put this component on it.
//   3. Add a Canvas with a Fader Image (see Fader.cs).
//   4. Set overworldSceneName and battleSceneName to match your scene names exactly.
//   5. In Build Settings, add both scenes (Overworld = index 0, Battle = index 1).
public enum GameState { FreeRoam, Battle, Dialog, Cutscene, Paused }

public class GameController : MonoBehaviour
{
    // The one and only instance.
    public static GameController Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Must match the scene name exactly (without .unity extension).")]
    [SerializeField] private string overworldSceneName = "Overworld";
    [SerializeField] private string battleSceneName    = "Battle";
    [Tooltip("Main menu scene name. When this scene loads, the persistent GameController destroys itself so it doesn't linger on the menu.")]
    [SerializeField] private string mainMenuSceneName  = "MainMenu";

    [Header("References")]
    [SerializeField] private Fader fader;

    [Header("Scene transition")]
    [Tooltip("If on, battle enter/exit uses the Persona screen-wipe (a ScreenTransition in the scene) instead of the black fade. Falls back to the fader if no ScreenTransition is found. Each scene that wipes needs its own ScreenTransition with Reveal On Start ticked, so it uncovers itself on arrival.")]
    [SerializeField] private bool preferScreenWipe = true;

    [Tooltip("Marker prefabs spawned in the overworld (e.g. bones at defeated-enemy positions). Optional — if null, no markers spawn.")]
    [SerializeField] private WorldMarkerData worldMarkerData;

    // Set when we covered a scene change with a wipe, so OnSceneLoaded knows to skip the
    // fader's fade-from-black (the destination scene's own ScreenTransition reveals itself).
    private bool _wipeCoveredLastLoad;

    private GameState        state;
    private PlayerController player;
    private BattleSystem     battleSystem;

    // Read-only peek at the current state. The overworld AI (OverworldEnemyController)
    // uses this to freeze its behaviour whenever we're not in FreeRoam.
    public GameState State => state;

    // True while the in-game menu has the game paused.
    public bool IsPaused => state == GameState.Paused;

    // Pause or unpause for the in-game menu. Pausing freezes time (Time.timeScale = 0)
    // and flips the state to Paused, which stops player input (Update) and the overworld
    // enemy AI (they only run when State == FreeRoam). It only ever pauses FROM FreeRoam
    // and unpauses back to it, so it can't accidentally stomp a battle/dialog/cutscene.
    public void SetPaused(bool paused)
    {
        if (paused)
        {
            if (state != GameState.FreeRoam) return;
            state = GameState.Paused;
            Time.timeScale = 0f;
        }
        else
        {
            if (state != GameState.Paused) return;
            state = GameState.FreeRoam;
            Time.timeScale = 1f;
        }
    }

    // Data that needs to survive a scene load, so it's static.
    // Heads up: we keep a List<PartyMember>, NOT the PartySystem itself, because
    // PartySystem is a MonoBehaviour living on the Player in the Overworld. When the
    // Overworld unloads, Unity destroys that GameObject and the reference goes null.
    // List<PartyMember> are plain C# objects, so they ride through scene swaps just fine.
    private static EnemyEncounterData  pendingEncounter;
    private static List<PartyMember>   pendingPartyMembers;

    // Id of the overworld-AI enemy that kicked off the current battle (empty for grass
    // encounters). Static so it survives the Overworld-to-Battle scene reload.
    private static string              pendingOverworldEnemyId;

    // Position of that enemy at the moment it triggered the battle. Used after the win
    // to drop a bone marker at the exact spot. Static for the same scene-reload reason
    // as the id above. Vector3.zero when no overworld enemy is pending.
    private static Vector3             pendingOverworldDefeatPosition;

    // Tracks the last overworld scene we were in, so we can detect region changes
    // and wipe the defeated-enemy registry when the player moves to a new area.
    private static string              lastOverworldSceneName;

    // Where the player was standing (and facing) the moment a battle started, so we can
    // plonk them right back there afterwards instead of at the scene's default spawn.
    // Static so it survives the Overworld-Battle-Overworld reloads. We only set this when
    // WE start a battle, so save-loads (handled by SaveManager) don't fight over it.
    private static bool                hasPendingPlayerReturn;
    private static Vector3             pendingPlayerReturnPosition;
    private static float               pendingPlayerReturnYaw;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[GameController] Already got one of these, destroying the duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[GameController] Singleton set up and marked DontDestroyOnLoad.");
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        BindCurrentScene();

        if (fader != null)
            StartCoroutine(fader.FadeFromBlack(0.5f));
        else
            Debug.LogWarning("[GameController] Fader is NOT assigned in the Inspector, so screen fades won't work.");
    }

    // Runs every time a scene finishes loading.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameController] Scene loaded: '{scene.name}'");

        // Heading back to the main menu: this DontDestroyOnLoad singleton shouldn't hang
        // around (the menu has no player or battle, and a fresh GameController spawns when
        // a new game / continue loads the Overworld). So reset time and self-destruct.
        if (!string.IsNullOrEmpty(mainMenuSceneName) && scene.name == mainMenuSceneName)
        {
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            return;
        }

        BindCurrentScene();

        if (scene.name == battleSceneName)
        {
            Debug.Log($"[GameController] Entering battle scene. Checking data:" +
                      $"\n  battleSystem     = {(battleSystem       != null ? "FOUND" : "NULL")}" +
                      $"\n  pendingMembers   = {(pendingPartyMembers != null ? pendingPartyMembers.Count + " member(s)" : "NULL")}" +
                      $"\n  pendingEncounter = {(pendingEncounter    != null ? "FOUND" : "NULL")}");

            if (battleSystem != null && pendingEncounter != null && pendingPartyMembers != null && pendingPartyMembers.Count > 0)
            {
                Debug.Log("[GameController] Everything's here, calling BattleSystem.StartBattle().");
                battleSystem.StartBattle(pendingPartyMembers, pendingEncounter);
            }
            else
            {
                Debug.LogError("[GameController] Didn't call StartBattle(), one of the required refs is null. See the log above.");
            }

            // If we wiped in, the battle scene's own ScreenTransition reveals itself -
            // skip the fade so we don't draw black over the wipe.
            if (_wipeCoveredLastLoad)
                _wipeCoveredLastLoad = false;
            else if (fader != null)
                StartCoroutine(fader.FadeFromBlack(0.4f));
        }
        else if (scene.name == overworldSceneName)
        {
            // Point the defeated-enemy registry at this region. Kills now stick around
            // per region (it's a multi-region map) instead of getting wiped when you
            // change areas, so coming back to an old place remembers who you already beat.
            // (Resting still clears the current region; New Game clears everything.)
            if (!string.IsNullOrEmpty(lastOverworldSceneName) &&
                lastOverworldSceneName != scene.name)
            {
                Debug.Log($"[GameController] Region change: '{lastOverworldSceneName}' to '{scene.name}'. Switching registry region (kills kept).");
            }
            DefeatedEnemyRegistry.SetCurrentRegion(scene.name);
            lastOverworldSceneName = scene.name;

            // Drop bone markers for every enemy beaten in this region. We do this AFTER
            // the region switch so wiped ids don't leave ghost markers lying around.
            SpawnBoneMarkers();

            // Put the player back where they were when the fight started (not the scene's
            // default spawn). Only happens on battle returns; save-loads leave this flag
            // false and let SaveManager handle the restore instead.
            if (hasPendingPlayerReturn && player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;             // the CC blocks direct transform writes, so turn it off first
                player.transform.position    = pendingPlayerReturnPosition;
                player.transform.eulerAngles = new Vector3(0f, pendingPlayerReturnYaw, 0f);
                if (cc != null) cc.enabled = true;
                Debug.Log($"[GameController] Put the player back at their pre-battle spot {pendingPlayerReturnPosition}.");
            }
            hasPendingPlayerReturn = false;

            state = GameState.FreeRoam;
            Debug.Log("[GameController] Back in the Overworld, state set to FreeRoam.");
            // Wiped back? The overworld's ScreenTransition reveals itself; skip the fade.
            if (_wipeCoveredLastLoad)
                _wipeCoveredLastLoad = false;
            else if (fader != null)
                StartCoroutine(fader.FadeFromBlack(0.5f));
        }
        else
        {
            Debug.LogWarning($"[GameController] Loaded unknown scene '{scene.name}'. " +
                             $"Expected '{overworldSceneName}' or '{battleSceneName}'. " +
                             $"Check your scene names in the Inspector.");
        }
    }

    // Finds and hooks up the scene's own components after each scene load.
    private void BindCurrentScene()
    {
        player       = FindFirstObjectByType<PlayerController>();
        battleSystem = FindFirstObjectByType<BattleSystem>();

        if (player != null)
        {
            player.OnEncounterTriggered -= OnEncounterTriggered;
            player.OnEncounterTriggered += OnEncounterTriggered;
            Debug.Log("[GameController] PlayerController found and bound.");
        }
        else
        {
            Debug.Log("[GameController] No PlayerController in this scene (expected in Battle scene).");
        }

        if (battleSystem != null)
        {
            battleSystem.OnBattleOver -= OnBattleOver;
            battleSystem.OnBattleOver += OnBattleOver;
            Debug.Log("[GameController] BattleSystem found and bound.");
        }
        else
        {
            Debug.Log("[GameController] No BattleSystem in this scene (expected in Overworld scene).");
        }
    }

    // --- World markers ---

    // Walks DefeatedEnemyRegistry.DefeatPositions and drops one bone-marker prefab per
    // defeated enemy at the spot it died. The markers live and die with the scene; the
    // registry is the real source of truth, this just re-draws it every overworld load.
    private void SpawnBoneMarkers()
    {
        if (worldMarkerData == null || worldMarkerData.BoneMarkerPrefab == null)
        {
            // No marker data wired, or the prefab slot's empty. Just quietly skip it;
            // bone markers are nice-to-have polish, the game's fine without them.
            return;
        }

        int spawned = 0;
        var prefab  = worldMarkerData.BoneMarkerPrefab;
        var yOffset = worldMarkerData.BoneMarkerYOffset;

        foreach (var kvp in DefeatedEnemyRegistry.DefeatPositions)
        {
            Vector3 pos = kvp.Value + Vector3.up * yOffset;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.name = $"BoneMarker_{kvp.Key}";

            // If the prefab has a BoneMarker on its root, give it the id.
            var marker = go.GetComponent<BoneMarker>();
            if (marker != null) marker.Initialize(kvp.Key);

            spawned++;
        }

        if (spawned > 0)
            Debug.Log($"[GameController] Spawned {spawned} bone marker(s) for defeated overworld enemies.");
    }

    void Update()
    {
        if (state == GameState.FreeRoam)
            player?.HandleUpdate();

        // TEMP DEBUG: quick save/load hotkeys for testing the save system.
        // F5 = save slot 0 (FreeRoam only), F9 = load slot 0. New Input System.
        // DELETE this once a real RestPoint is in the scene.
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (state == GameState.FreeRoam && kb.f5Key.wasPressedThisFrame)
            {
                bool ok = SaveSystem.Save();
                Debug.Log(ok ? "[DEBUG] F5: saved slot 0." : "[DEBUG] F5: save failed (see error).");
            }
            if (kb.f9Key.wasPressedThisFrame)
            {
                if (SaveSystem.HasSave()) SaveSystem.Load();
                else Debug.Log("[DEBUG] F9: nothing saved in slot 0 yet.");
            }
        }
    }

    // --- Battle flow ---

    private void OnEncounterTriggered(EnemyEncounterData encounterData)
    {
        StartCoroutine(TransitionToBattle(encounterData));
    }

    // Covers the screen before a scene change. Prefers the current scene's
    // ScreenTransition wipe (Persona-style), falling back to the black fader if there
    // isn't one. Returns once the screen is fully covered, so it's safe to LoadScene
    // right after. The destination scene uncovers itself via its own ScreenTransition
    // (Reveal On Start) - that's why we set _wipeCoveredLastLoad, so OnSceneLoaded skips
    // the fader's fade-from-black (which would otherwise draw black over the wipe).
    private IEnumerator CoverForSceneChange()
    {
        ScreenTransition wipe = preferScreenWipe ? FindFirstObjectByType<ScreenTransition>() : null;

        if (wipe != null)
        {
            wipe.Play();
            yield return new WaitForSecondsRealtime(wipe.WipeDuration);
            _wipeCoveredLastLoad = true;
        }
        else if (fader != null)
        {
            yield return fader.FadeToBlack(0.5f);
            _wipeCoveredLastLoad = false;
        }
        else
        {
            _wipeCoveredLastLoad = false;
            yield return null;
        }
    }

    private IEnumerator TransitionToBattle(EnemyEncounterData encounterData)
    {
        state = GameState.Battle;

        pendingEncounter    = encounterData;
        // Grab the member list NOW, before the scene unloads and destroys the Player.
        // Only active + healthy members actually fight; if the player somehow benched
        // everyone, fall back to all healthy members so we're not sending in nobody.
        pendingPartyMembers = player.Party.ActiveHealthyBattleMembers;
        if (pendingPartyMembers.Count == 0)
            pendingPartyMembers = player.Party.HealthyMembers;

        // Note where the player's standing so we can drop them back here after the fight,
        // instead of at the Overworld's default spawn point.
        if (player != null)
        {
            pendingPlayerReturnPosition = player.transform.position;
            pendingPlayerReturnYaw      = player.transform.eulerAngles.y;
            hasPendingPlayerReturn      = true;
        }

        Debug.Log($"[GameController] Encounter triggered!" +
                  $"\n  Encounter data:  {(encounterData       != null ? encounterData.name : "NULL")}" +
                  $"\n  Party members:   {pendingPartyMembers.Count} healthy member(s) copied" +
                  $"\n  Loading scene:   '{battleSceneName}'");

        yield return CoverForSceneChange();

        SceneManager.LoadScene(battleSceneName);
    }

    private void OnBattleOver(bool playerWon)
    {
        // If an overworld AI enemy started this fight and the player won, jot down its id
        // and position so it stays gone (and drops a bone marker) through the next scene reload.
        if (playerWon && !string.IsNullOrEmpty(pendingOverworldEnemyId))
        {
            DefeatedEnemyRegistry.MarkDefeated(pendingOverworldEnemyId, pendingOverworldDefeatPosition);
            Debug.Log($"[GameController] Overworld enemy '{pendingOverworldEnemyId}' beaten at {pendingOverworldDefeatPosition}, added to DefeatedEnemyRegistry (now {DefeatedEnemyRegistry.Count} defeated).");
        }
        pendingOverworldEnemyId        = null;
        pendingOverworldDefeatPosition = Vector3.zero;

        StartCoroutine(TransitionToOverworld(playerWon));
    }

    // AttackState calls this right before it kicks off an encounter. We stash the enemy's
    // id (so we can mark it defeated if you win) and its world position (so we can drop a
    // bone marker there next overworld load). Pass an empty id for grass/random encounters.
    public void SetPendingOverworldDefeatInfo(string enemyId, Vector3 worldPosition)
    {
        pendingOverworldEnemyId        = enemyId;
        pendingOverworldDefeatPosition = worldPosition;
    }

    private IEnumerator TransitionToOverworld(bool playerWon)
    {
        // If the whole party went down, heal everyone so the game doesn't softlock.
        if (pendingPartyMembers != null && !playerWon)
        {
            bool allFainted = pendingPartyMembers.TrueForAll(m => m.IsFainted);
            if (allFainted)
            {
                Debug.Log("[GameController] Party got wiped, healing everyone before heading back to the overworld.");
                foreach (var member in pendingPartyMembers)
                    member.HealFull();
            }
        }

        yield return CoverForSceneChange();

        Debug.Log("[GameController] Returning to overworld.");
        SceneManager.LoadScene(overworldSceneName);
    }

    // --- Dialog ---

    // Shows some dialog lines. Right now it just logs them; swap in a real dialog UI later.
    public void ShowDialog(string[] lines)
    {
        // Placeholder for now. Hook up an actual dialog panel here when you want one.
        if (lines != null)
            foreach (var line in lines)
                Debug.Log($"[Dialog] {line}");
    }

    // --- Recruitment ---

    // Kicks off the "an NPC joins the party" flow. Swap in a proper dialog/UI prompt later.
    public void StartRecruitment(NPCController npc, PlayerController playerCtrl, GameObject followerPrefab)
    {
        StartCoroutine(RecruitmentSequence(npc, playerCtrl, followerPrefab));
    }

    private IEnumerator RecruitmentSequence(NPCController npc, PlayerController playerCtrl, GameObject followerPrefab)
    {
        state = GameState.Dialog;

        // TODO: show a real "Would you like [name] to join your party?" prompt here.
        yield return new WaitForSeconds(1.5f);

        bool accepted = playerCtrl.Party.AddMember(npc.CharacterData);

        if (accepted)
        {
            npc.OnJoinedParty();

            // Spawn a follower behind the last follower / the player.
            if (followerPrefab != null)
            {
                var followerGo = Instantiate(followerPrefab, playerCtrl.transform.position, Quaternion.identity);
                var follower   = followerGo.GetComponent<FollowerController>();
                follower?.SetLeader(playerCtrl.transform);
            }
        }

        state = GameState.FreeRoam;
    }
}
