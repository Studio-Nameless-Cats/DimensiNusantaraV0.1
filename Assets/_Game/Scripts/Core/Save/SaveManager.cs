using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace Nusantara.SaveSystem
{
    /// <summary>
    /// Central save/load coordinator. Static (no scene object to wire) and
    /// dependency: Newtonsoft Json (com.unity.nuget.newtonsoft-json — install via
    /// Package Manager → Add package by name).
    ///
    /// Responsibilities:
    ///   • Capture the live game into a <see cref="SaveData"/> snapshot.
    ///   • Write/read JSON with ATOMIC writes (temp file + replace) so a crash
    ///     mid-save never corrupts an existing save.
    ///   • Multiple slots, each with a lightweight .meta header for load menus.
    ///   • Versioning + migration hook so old saves keep loading as the game grows.
    ///   • Restore on the next scene load (party + player position), having already
    ///     imported the static world registry BEFORE the scene loads (no race with
    ///     GameController's bone-marker spawn).
    ///
    /// Core systems (party, player position, world registry) are captured directly.
    /// Future modular systems plug in via <see cref="ISaveParticipant"/> + Register().
    /// </summary>
    public static class SaveManager
    {
        public const int SlotCount = 3;

        // ── Participant registry (future modular systems) ──────────────────────
        private static readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();
        public static void Register(ISaveParticipant p)   { if (p != null && !participants.Contains(p)) participants.Add(p); }
        public static void Unregister(ISaveParticipant p) { participants.Remove(p); }

        // ── Playtime tracking ──────────────────────────────────────────────────
        private static float _loadedPlaySeconds;
        private static float _sessionStartRealtime = Time.realtimeSinceStartup;
        public static float CurrentPlaySeconds =>
            _loadedPlaySeconds + (Time.realtimeSinceStartup - _sessionStartRealtime);

        // ── Pending restore (applied on next matching scene load) ───────────────
        private static SaveData _pendingRestore;

        // ── Bootstrap: hook scene loads once, automatically ─────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // ── Paths ────────────────────────────────────────────────────────────────
        private static string SaveDir => Application.persistentDataPath;
        private static string SavePath(int slot) => Path.Combine(SaveDir, $"save_{slot}.json");
        private static string MetaPath(int slot) => Path.Combine(SaveDir, $"save_{slot}.meta.json");

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        // ── Public API ─────────────────────────────────────────────────────────

        public static bool HasSave(int slot = 0) => File.Exists(SavePath(slot));

        /// <summary>Lightweight slot header for a load menu — null if the slot is empty.</summary>
        public static SaveMetadata GetMetadata(int slot = 0)
        {
            try
            {
                if (!File.Exists(MetaPath(slot))) return null;
                return JsonConvert.DeserializeObject<SaveMetadata>(File.ReadAllText(MetaPath(slot)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed reading metadata for slot {slot}: {e.Message}");
                return null;
            }
        }

        public static void DeleteSave(int slot = 0)
        {
            try
            {
                if (File.Exists(SavePath(slot))) File.Delete(SavePath(slot));
                if (File.Exists(MetaPath(slot))) File.Delete(MetaPath(slot));
                Debug.Log($"[SaveManager] Deleted save slot {slot}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed deleting slot {slot}: {e.Message}");
            }
        }

        /// <summary>Call when starting a fresh game so playtime + world state reset cleanly.</summary>
        public static void NewGame()
        {
            _loadedPlaySeconds   = 0f;
            _sessionStartRealtime = Time.realtimeSinceStartup;
            _pendingRestore      = null;
            DefeatedEnemyRegistry.ClearAll();
            PartySystem.ResetParty();   // wipe persistent party → starting party rebuilds in the new scene
        }

        /// <summary>
        /// Snapshots the live game and writes it (atomically) to the given slot.
        /// Must be called from FreeRoam — never mid-battle (we don't serialize live
        /// battle coroutines; a loaded game always lands in the overworld).
        /// </summary>
        public static bool Save(int slot = 0)
        {
            try
            {
                var data = Capture();
                if (data == null) return false;

                string json = JsonConvert.SerializeObject(data, JsonSettings);
                AtomicWrite(SavePath(slot), json);

                var meta = BuildMetadata(slot, data);
                File.WriteAllText(MetaPath(slot), JsonConvert.SerializeObject(meta, JsonSettings));

                Debug.Log($"[SaveManager] Saved to slot {slot} ({data.party.members.Count} member(s), scene '{data.player.sceneName}').");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save to slot {slot} failed: {e}");
                return false;
            }
        }

        /// <summary>
        /// Reads a slot, imports the static world registry immediately, then loads the
        /// saved overworld scene. Party + player position are restored once that scene
        /// finishes loading (see <see cref="OnSceneLoaded"/>). Returns false if empty/corrupt.
        /// </summary>
        public static bool Load(int slot = 0)
        {
            try
            {
                if (!File.Exists(SavePath(slot)))
                {
                    Debug.LogWarning($"[SaveManager] No save in slot {slot}.");
                    return false;
                }

                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(SavePath(slot)), JsonSettings);
                if (data == null) { Debug.LogError($"[SaveManager] Slot {slot} deserialized to null."); return false; }

                data = Migrate(data);

                // World registry is static & scene-independent — import BEFORE the scene
                // loads so GameController's bone-marker spawn sees the right data.
                DefeatedEnemyRegistry.Import(data.world);

                // Playtime baseline from metadata (if present).
                var meta = GetMetadata(slot);
                _loadedPlaySeconds    = meta?.playSeconds ?? 0f;
                _sessionStartRealtime = Time.realtimeSinceStartup;

                _pendingRestore = data;

                if (string.IsNullOrEmpty(data.player.sceneName))
                {
                    Debug.LogError("[SaveManager] Save has no scene name — cannot load.");
                    _pendingRestore = null;
                    return false;
                }

                SceneManager.LoadScene(data.player.sceneName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load of slot {slot} failed: {e}");
                _pendingRestore = null;
                return false;
            }
        }

        // ── Capture ───────────────────────────────────────────────────────────

        private static SaveData Capture()
        {
            var data = new SaveData { saveVersion = SaveData.CurrentVersion };

            // Player position + scene.
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                data.player.position = player.transform.position;
                data.player.yaw      = player.transform.eulerAngles.y;
            }
            else
            {
                Debug.LogWarning("[SaveManager] No PlayerController found at save time — position not captured.");
            }
            data.player.sceneName = SceneManager.GetActiveScene().name;

            // Party.
            var party = UnityEngine.Object.FindFirstObjectByType<PartySystem>();
            if (party != null)
            {
                foreach (var m in party.Members)
                {
                    string id = m.Base != null ? m.Base.Id : null;
                    if (string.IsNullOrEmpty(id))
                    {
                        Debug.LogWarning($"[SaveManager] Party member '{m.Name}' has no CharacterData.Id — skipped. " +
                                         "Add it to the GameDatabase / assign an id.");
                        continue;
                    }
                    data.party.members.Add(new PartyMemberSaveData { characterId = id, currentHp = m.CurrentHp, currentMp = m.CurrentMp });
                }
            }

            // World (static, multi-region).
            data.world = DefeatedEnemyRegistry.Export();

            // Modular systems.
            foreach (var p in participants) p.Capture(data);

            return data;
        }

        // ── Restore (on scene load) ─────────────────────────────────────────────

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingRestore == null) return;
            if (scene.name != _pendingRestore.player.sceneName) return;

            var data = _pendingRestore;
            _pendingRestore = null;  // one-shot

            // Party.
            var party = UnityEngine.Object.FindFirstObjectByType<PartySystem>();
            if (party != null) party.LoadFromSave(data.party.members);
            else Debug.LogWarning("[SaveManager] No PartySystem in loaded scene — party not restored.");

            // Player position (CharacterController must be toggled to move the transform).
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position    = data.player.position;
                player.transform.eulerAngles = new Vector3(0f, data.player.yaw, 0f);
                if (cc != null) cc.enabled = true;
            }

            // Modular systems.
            foreach (var p in participants) p.Restore(data);

            Debug.Log($"[SaveManager] Restored save into scene '{scene.name}'.");
        }

        // ── Versioning / migration ──────────────────────────────────────────────

        private static SaveData Migrate(SaveData data)
        {
            if (data.saveVersion == SaveData.CurrentVersion) return data;

            Debug.Log($"[SaveManager] Migrating save v{data.saveVersion} → v{SaveData.CurrentVersion}.");

            // v1 → v2: PartyMemberSaveData gained currentMp. Old saves have no value;
            // mark them "unset" (-1) so restore fills MP to max rather than to 0.
            if (data.saveVersion < 2)
            {
                if (data.party?.members != null)
                    foreach (var m in data.party.members)
                        m.currentMp = -1;
                data.saveVersion = 2;
            }

            data.saveVersion = SaveData.CurrentVersion;
            return data;
        }

        // ── IO helpers ────────────────────────────────────────────────────────

        /// <summary>Write via a temp file then replace, so a crash mid-write can't corrupt the live save.</summary>
        private static void AtomicWrite(string path, string contents)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, contents);
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }

        private static SaveMetadata BuildMetadata(int slot, SaveData data)
        {
            var names = new List<string>();
            var db = GameDatabase.Instance;
            foreach (var m in data.party.members)
            {
                var c = db != null ? db.GetCharacter(m.characterId) : null;
                names.Add(c != null ? c.Name : m.characterId);
            }
            return new SaveMetadata
            {
                slot          = slot,
                savedAtIso    = DateTime.UtcNow.ToString("o"),
                playSeconds   = CurrentPlaySeconds,
                locationScene = data.player.sceneName,
                partyCount    = data.party.members.Count,
                partyNames    = names.ToArray()
            };
        }
    }
}
