using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace Nusantara.SaveSystem
{
    // The brain behind saving and loading. It's static (nothing to wire up in a scene)
    // and leans on Newtonsoft Json (com.unity.nuget.newtonsoft-json, grab it from the
    // Package Manager via "Add package by name").
    //
    // What it does:
    //   - Snapshots the live game into a SaveData.
    //   - Reads/writes JSON with atomic writes (temp file then replace), so a crash
    //     mid-save can't corrupt an existing save.
    //   - Supports several slots, each with a small .meta header for load menus.
    //   - Has a version + migration hook so old saves keep loading as the game grows.
    //   - Restores on the next scene load (party + player position), after already
    //     importing the static world registry BEFORE the scene loads (so it doesn't
    //     race GameController's bone-marker spawn).
    //
    // The core stuff (party, player position, world registry) gets captured directly.
    // Bolt-on systems later plug in through ISaveParticipant + Register().
    public static class SaveManager
    {
        public const int SlotCount = 3;

        // Registry for any bolt-on systems that want in on saves.
        private static readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();
        public static void Register(ISaveParticipant p)   { if (p != null && !participants.Contains(p)) participants.Add(p); }
        public static void Unregister(ISaveParticipant p) { participants.Remove(p); }

        // Keeping track of total playtime.
        private static float _loadedPlaySeconds;
        private static float _sessionStartRealtime = Time.realtimeSinceStartup;
        public static float CurrentPlaySeconds =>
            _loadedPlaySeconds + (Time.realtimeSinceStartup - _sessionStartRealtime);

        // A save waiting to be applied on the next matching scene load.
        private static SaveData _pendingRestore;

        // Hook into scene loads once, automatically, when the game starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Where the save files live.
        private static string SaveDir => Application.persistentDataPath;
        private static string SavePath(int slot) => Path.Combine(SaveDir, $"save_{slot}.json");
        private static string MetaPath(int slot) => Path.Combine(SaveDir, $"save_{slot}.meta.json");

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static bool HasSave(int slot = 0) => File.Exists(SavePath(slot));

        // The small slot header for a load menu. Null if the slot's empty.
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

        // Call this when starting a brand-new game so playtime + world state reset cleanly.
        public static void NewGame()
        {
            _loadedPlaySeconds   = 0f;
            _sessionStartRealtime = Time.realtimeSinceStartup;
            _pendingRestore      = null;
            DefeatedEnemyRegistry.ClearAll();
            PartySystem.ResetParty();   // wipe the persistent party so the starting party rebuilds in the new scene
        }

        // Snapshots the live game and writes it (atomically) to the given slot.
        // Only call this from FreeRoam, never mid-battle: we don't serialize live battle
        // coroutines, and a loaded game always comes back in the overworld anyway.
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

        // Reads a slot, imports the static world registry right away, then loads the saved
        // overworld scene. The party + player position get restored once that scene
        // finishes loading (see OnSceneLoaded). Returns false if the slot's empty or busted.
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

                // The world registry is static and scene-independent, so import it BEFORE
                // the scene loads, that way GameController's bone-marker spawn sees the right data.
                DefeatedEnemyRegistry.Import(data.world);

                // Set the playtime baseline from the metadata, if it's there.
                var meta = GetMetadata(slot);
                _loadedPlaySeconds    = meta?.playSeconds ?? 0f;
                _sessionStartRealtime = Time.realtimeSinceStartup;

                _pendingRestore = data;

                if (string.IsNullOrEmpty(data.player.sceneName))
                {
                    Debug.LogError("[SaveManager] Save has no scene name, can't load it.");
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

        // --- Capturing the snapshot ---

        private static SaveData Capture()
        {
            var data = new SaveData { saveVersion = SaveData.CurrentVersion };

            // Where the player is, and which scene.
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                data.player.position = player.transform.position;
                data.player.yaw      = player.transform.eulerAngles.y;
            }
            else
            {
                Debug.LogWarning("[SaveManager] No PlayerController around at save time, so position didn't get captured.");
            }
            data.player.sceneName = SceneManager.GetActiveScene().name;

            // The party.
            var party = UnityEngine.Object.FindFirstObjectByType<PartySystem>();
            if (party != null)
            {
                foreach (var m in party.Members)
                {
                    string id = m.Base != null ? m.Base.Id : null;
                    if (string.IsNullOrEmpty(id))
                    {
                        Debug.LogWarning($"[SaveManager] Party member '{m.Name}' has no CharacterData.Id, so we skipped them. " +
                                         "Add it to the GameDatabase / assign an id.");
                        continue;
                    }
                    data.party.members.Add(new PartyMemberSaveData
                    {
                        characterId      = id,
                        currentHp        = m.CurrentHp,
                        currentMp        = m.CurrentMp,
                        equippedSkillIds = m.GetEquippedIds(),
                        isActive         = m.IsActiveInBattle,
                        level            = m.Level,
                        currentExp       = m.CurrentExp
                    });
                }
            }

            // The world (static, split by region).
            data.world = DefeatedEnemyRegistry.Export();

            // Any bolt-on systems get their turn.
            foreach (var p in participants) p.Capture(data);

            return data;
        }

        // --- Restoring (when the scene loads) ---

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingRestore == null) return;
            if (scene.name != _pendingRestore.player.sceneName) return;

            var data = _pendingRestore;
            _pendingRestore = null;  // only do this once

            // The party.
            var party = UnityEngine.Object.FindFirstObjectByType<PartySystem>();
            if (party != null) party.LoadFromSave(data.party.members);
            else Debug.LogWarning("[SaveManager] No PartySystem in the loaded scene, so the party didn't get restored.");

            // Player position. We toggle the CharacterController off/on to actually move the transform.
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position    = data.player.position;
                player.transform.eulerAngles = new Vector3(0f, data.player.yaw, 0f);
                if (cc != null) cc.enabled = true;
            }

            // Bolt-on systems.
            foreach (var p in participants) p.Restore(data);

            Debug.Log($"[SaveManager] Restored the save into scene '{scene.name}'.");
        }

        // --- Versioning / migration ---

        private static SaveData Migrate(SaveData data)
        {
            if (data.saveVersion == SaveData.CurrentVersion) return data;

            Debug.Log($"[SaveManager] Migrating save v{data.saveVersion} to v{SaveData.CurrentVersion}.");

            // v1 to v2: PartyMemberSaveData got currentMp. Old saves have no value, so mark
            // them "unset" (-1) and restore fills MP up to max instead of leaving it at 0.
            if (data.saveVersion < 2)
            {
                if (data.party?.members != null)
                    foreach (var m in data.party.members)
                        m.currentMp = -1;
                data.saveVersion = 2;
            }

            // v2 to v3: members got equippedSkillIds + isActive. Old members have neither,
            // so leave equippedSkillIds empty (that means default loadout) and force isActive
            // true so the whole party fights (the old behaviour was "all healthy members").
            if (data.saveVersion < 3)
            {
                if (data.party?.members != null)
                    foreach (var m in data.party.members)
                    {
                        if (m.equippedSkillIds == null) m.equippedSkillIds = new System.Collections.Generic.List<string>();
                        m.isActive = true;
                    }
                data.saveVersion = 3;
            }

            // v3 to v4: members got level + currentExp. Old members have neither, so leave
            // level = 0 (PartyMember restore then falls back to StartingLevel) and
            // currentExp = 0, so old saves load at their characters' starting level.
            if (data.saveVersion < 4)
            {
                if (data.party?.members != null)
                    foreach (var m in data.party.members)
                    {
                        m.level      = 0;
                        m.currentExp = 0;
                    }
                data.saveVersion = 4;
            }

            data.saveVersion = SaveData.CurrentVersion;
            return data;
        }

        // --- File helpers ---

        // Write to a temp file, then swap it in. That way a crash mid-write can't trash the live save.
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
