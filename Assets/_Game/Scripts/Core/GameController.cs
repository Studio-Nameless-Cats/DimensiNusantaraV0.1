using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game manager. Persists across scenes (DontDestroyOnLoad).
/// Manages the game state machine and handles scene transitions between
/// the Overworld and the Battle scene.
///
/// Compatible with: Unity 6 (6000.x) and Unity 2022.3 LTS
///
/// Setup:
///   1. Create an empty GameObject in the Overworld scene called "GameController".
///   2. Add this component.
///   3. Add a Canvas with a Fader Image (see Fader.cs).
///   4. Fill in overworldSceneName and battleSceneName to match your scene names exactly.
///   5. In Build Settings → add both scenes (Overworld = index 0, Battle = index 1).
/// </summary>
public enum GameState { FreeRoam, Battle, Dialog, Cutscene }

public class GameController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GameController Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Must match the scene name exactly (without .unity extension).")]
    [SerializeField] private string overworldSceneName = "Overworld";
    [SerializeField] private string battleSceneName    = "Battle";

    [Header("References")]
    [SerializeField] private Fader fader;

    [Tooltip("Marker prefabs spawned in the overworld (e.g. bones at defeated-enemy positions). Optional — if null, no markers spawn.")]
    [SerializeField] private WorldMarkerData worldMarkerData;

    // ── State ─────────────────────────────────────────────────────────────────
    private GameState        state;
    private PlayerController player;
    private BattleSystem     battleSystem;

    /// <summary>
    /// Read-only view of the current game state. Used by overworld AI
    /// (OverworldEnemyController) to pause their FSM whenever we're not in FreeRoam.
    /// </summary>
    public GameState State => state;

    // ── Cross-scene data (static so it survives scene loads) ─────────────────
    // ⚠️ We store List<PartyMember> — NOT PartySystem — because PartySystem is a
    // MonoBehaviour that lives on the Player in the Overworld. When the Overworld
    // unloads, Unity destroys that GameObject and the reference becomes null.
    // List<PartyMember> are plain C# objects — they survive scene transitions safely.
    private static EnemyEncounterData  pendingEncounter;
    private static List<PartyMember>   pendingPartyMembers;

    // Id of the overworld-AI enemy that started the current battle (empty for grass encounters).
    // Static so it survives the scene reload between Overworld → Battle.
    private static string              pendingOverworldEnemyId;

    // Position of that enemy at the moment it triggered the battle. Used after the win
    // to drop a bone marker at the exact spot. Static for the same scene-reload reason
    // as the id above. Vector3.zero when no overworld enemy is pending.
    private static Vector3             pendingOverworldDefeatPosition;

    // Tracks the last overworld scene we were in, so we can detect region changes
    // and wipe the defeated-enemy registry when the player moves to a new area.
    private static string              lastOverworldSceneName;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[GameController] Duplicate instance detected — destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[GameController] Singleton initialised and marked DontDestroyOnLoad.");
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        BindCurrentScene();

        if (fader != null)
            StartCoroutine(fader.FadeFromBlack(0.5f));
        else
            Debug.LogWarning("[GameController] Fader is NOT assigned in the Inspector — screen fade will not work.");
    }

    // ── Scene loaded callback ─────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameController] Scene loaded: '{scene.name}'");
        BindCurrentScene();

        if (scene.name == battleSceneName)
        {
            Debug.Log($"[GameController] Entering battle scene. Checking data:" +
                      $"\n  battleSystem     = {(battleSystem       != null ? "FOUND" : "NULL ❌")}" +
                      $"\n  pendingMembers   = {(pendingPartyMembers != null ? pendingPartyMembers.Count + " member(s)" : "NULL ❌")}" +
                      $"\n  pendingEncounter = {(pendingEncounter    != null ? "FOUND" : "NULL ❌")}");

            if (battleSystem != null && pendingEncounter != null && pendingPartyMembers != null && pendingPartyMembers.Count > 0)
            {
                Debug.Log("[GameController] All data valid — calling BattleSystem.StartBattle().");
                battleSystem.StartBattle(pendingPartyMembers, pendingEncounter);
            }
            else
            {
                Debug.LogError("[GameController] StartBattle() NOT called — one or more required references are null. Check the log above.");
            }

            if (fader != null)
                StartCoroutine(fader.FadeFromBlack(0.4f));
        }
        else if (scene.name == overworldSceneName)
        {
            // Region change: if the last overworld scene we loaded had a
            // different name, wipe the defeated-enemy registry so enemies in
            // the new region populate as fresh. Same-name reloads (the
            // Battle → Overworld return trip) are NOT region changes.
            if (!string.IsNullOrEmpty(lastOverworldSceneName) &&
                lastOverworldSceneName != scene.name)
            {
                DefeatedEnemyRegistry.Clear();
                Debug.Log($"[GameController] Region change: '{lastOverworldSceneName}' → '{scene.name}'. Cleared DefeatedEnemyRegistry.");
            }
            lastOverworldSceneName = scene.name;

            // Spawn bone markers for every enemy that's been defeated in this region.
            // Done AFTER the region-change clear so wiped ids don't leave ghost markers.
            SpawnBoneMarkers();

            state = GameState.FreeRoam;
            Debug.Log("[GameController] Back in Overworld — state set to FreeRoam.");
            if (fader != null)
                StartCoroutine(fader.FadeFromBlack(0.5f));
        }
        else
        {
            Debug.LogWarning($"[GameController] Loaded unknown scene '{scene.name}'. " +
                             $"Expected '{overworldSceneName}' or '{battleSceneName}'. " +
                             $"Check your scene names in the Inspector.");
        }
    }

    /// <summary>Finds and wires up scene-local components after every scene load.</summary>
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

    // ── World markers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Walks <see cref="DefeatedEnemyRegistry.DefeatPositions"/> and instantiates
    /// one bone-marker prefab per defeated enemy at its recorded death position.
    /// Markers live in the scene and die with it — the registry is the source
    /// of truth, this just renders it on every overworld load.
    /// </summary>
    private void SpawnBoneMarkers()
    {
        if (worldMarkerData == null || worldMarkerData.BoneMarkerPrefab == null)
        {
            // Either no marker data wired, or the prefab slot is empty. Skip silently —
            // bone markers are polish, not required for the game to function.
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

            // If the prefab has a BoneMarker component on the root, hand it the id.
            var marker = go.GetComponent<BoneMarker>();
            if (marker != null) marker.Initialize(kvp.Key);

            spawned++;
        }

        if (spawned > 0)
            Debug.Log($"[GameController] Spawned {spawned} bone marker(s) for defeated overworld enemies.");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (state == GameState.FreeRoam)
            player?.HandleUpdate();
    }

    // ── Battle flow ───────────────────────────────────────────────────────────

    private void OnEncounterTriggered(EnemyEncounterData encounterData)
    {
        StartCoroutine(TransitionToBattle(encounterData));
    }

    private IEnumerator TransitionToBattle(EnemyEncounterData encounterData)
    {
        state = GameState.Battle;

        pendingEncounter    = encounterData;
        // Copy the member list NOW — before the scene unloads and destroys the Player
        pendingPartyMembers = player.Party.HealthyMembers;

        Debug.Log($"[GameController] Encounter triggered!" +
                  $"\n  Encounter data:  {(encounterData       != null ? encounterData.name : "NULL ❌")}" +
                  $"\n  Party members:   {pendingPartyMembers.Count} healthy member(s) copied" +
                  $"\n  Loading scene:   '{battleSceneName}'");

        if (fader != null)
            yield return fader.FadeToBlack(0.5f);
        else
            yield return null;

        SceneManager.LoadScene(battleSceneName);
    }

    private void OnBattleOver(bool playerWon)
    {
        // If an overworld AI enemy started this battle and the player won,
        // record its id + position so it stays despawned (and drops a bone
        // marker) across the upcoming scene reload.
        if (playerWon && !string.IsNullOrEmpty(pendingOverworldEnemyId))
        {
            DefeatedEnemyRegistry.MarkDefeated(pendingOverworldEnemyId, pendingOverworldDefeatPosition);
            Debug.Log($"[GameController] Overworld enemy '{pendingOverworldEnemyId}' defeated at {pendingOverworldDefeatPosition} — added to DefeatedEnemyRegistry (now {DefeatedEnemyRegistry.Count} defeated).");
        }
        pendingOverworldEnemyId        = null;
        pendingOverworldDefeatPosition = Vector3.zero;

        StartCoroutine(TransitionToOverworld(playerWon));
    }

    /// <summary>
    /// Called by AttackState right before it triggers an encounter. Records
    /// both the enemy id (so we can mark it defeated on win) and its world
    /// position (so we can spawn a bone marker there on the next overworld
    /// scene load). Pass an empty id for grass / random encounters.
    /// </summary>
    public void SetPendingOverworldDefeatInfo(string enemyId, Vector3 worldPosition)
    {
        pendingOverworldEnemyId        = enemyId;
        pendingOverworldDefeatPosition = worldPosition;
    }

    private IEnumerator TransitionToOverworld(bool playerWon)
    {
        // If the whole party fainted, heal them all so the game isn't softlocked
        if (pendingPartyMembers != null && !playerWon)
        {
            bool allFainted = pendingPartyMembers.TrueForAll(m => m.IsFainted);
            if (allFainted)
            {
                Debug.Log("[GameController] Party was wiped — healing all members before returning to overworld.");
                foreach (var member in pendingPartyMembers)
                    member.HealFull();
            }
        }

        if (fader != null)
            yield return fader.FadeToBlack(0.5f);
        else
            yield return null;

        Debug.Log("[GameController] Returning to overworld.");
        SceneManager.LoadScene(overworldSceneName);
    }

    // ── Dialog ────────────────────────────────────────────────────────────────

    /// <summary>Displays simple dialog lines. Extend with a full Dialog UI as needed.</summary>
    public void ShowDialog(string[] lines)
    {
        // Basic implementation — wire up a full dialog panel here if desired
        if (lines != null)
            foreach (var line in lines)
                Debug.Log($"[Dialog] {line}");
    }

    // ── Recruitment ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the NPC recruitment flow. Extend this with a proper dialog/UI prompt.
    /// </summary>
    public void StartRecruitment(NPCController npc, PlayerController playerCtrl, GameObject followerPrefab)
    {
        StartCoroutine(RecruitmentSequence(npc, playerCtrl, followerPrefab));
    }

    private IEnumerator RecruitmentSequence(NPCController npc, PlayerController playerCtrl, GameObject followerPrefab)
    {
        state = GameState.Dialog;

        // TODO: Show a proper "Would you like [name] to join your party?" dialog here.
        yield return new WaitForSeconds(1.5f);

        bool accepted = playerCtrl.Party.AddMember(npc.CharacterData);

        if (accepted)
        {
            npc.OnJoinedParty();

            // Spawn a follower behind the last follower / player
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
