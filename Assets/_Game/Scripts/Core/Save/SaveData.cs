using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.SaveSystem
{
    /// <summary>
    /// Plain serializable snapshot of everything the game needs to restore a session.
    /// Strongly typed and split into clear sections so JSON stays readable, diffable,
    /// and versioned. Only MUTABLE state lives here — base stats stay in the assets.
    ///
    /// Versioning: <see cref="saveVersion"/> is the FIRST field. Bump
    /// <see cref="CurrentVersion"/> whenever the shape changes and add a migration
    /// step in SaveManager so old saves keep loading.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Bump this whenever SaveData's shape changes; add a migration in SaveManager.</summary>
        public const int CurrentVersion = 4;   // v4: added level + currentExp per member

        public int saveVersion = CurrentVersion;

        public PlayerSaveData player = new PlayerSaveData();
        public PartySaveData  party  = new PartySaveData();
        public WorldSaveData  world  = new WorldSaveData();

        /// <summary>
        /// Open extension slot for future modular systems (quests, inventory, flags).
        /// ISaveParticipant implementations read/write their own string-keyed blob here
        /// so adding a system never touches the core sections above.
        /// </summary>
        public List<NamedBlob> modules = new List<NamedBlob>();

        public string GetModule(string key)
        {
            foreach (var m in modules)
                if (m.key == key) return m.json;
            return null;
        }

        public void SetModule(string key, string json)
        {
            foreach (var m in modules)
                if (m.key == key) { m.json = json; return; }
            modules.Add(new NamedBlob { key = key, json = json });
        }
    }

    // ── Sections ────────────────────────────────────────────────────────────────

    [Serializable]
    public class PlayerSaveData
    {
        public string sceneName;   // overworld scene to restore into
        public Vec3   position;
        public float  yaw;         // future-proofing; player root currently doesn't rotate
    }

    [Serializable]
    public class PartySaveData
    {
        public List<PartyMemberSaveData> members = new List<PartyMemberSaveData>();
    }

    [Serializable]
    public class PartyMemberSaveData
    {
        public string characterId;  // CharacterData.Id — resolved via GameDatabase on load
        public int    currentHp;
        public int    currentMp = -1;   // -1 = "unset" → restore to full MP (v1 saves migrate to this)

        // v3: loadout + battle selection.
        // equippedSkillIds = SkillData.Id of the equipped NORMAL skills (resolved against
        // the character's own pool on load). null/empty → restore the default loadout.
        public List<string> equippedSkillIds = new List<string>();
        // Whether this member fights (vs sits in reserve). Defaults true so legacy
        // saves with no value (and freshly built parties) treat everyone as active.
        public bool isActive = true;

        // v4: progression. level = 0 means "unset" → restore falls back to the
        // character's StartingLevel (so legacy saves keep their starting level).
        public int level = 0;
        public int currentExp = 0;

        // Future: equipment ids, learned skill ids — add fields here.
    }

    [Serializable]
    public class WorldSaveData
    {
        // Multi-region defeated-enemy persistence: one record per overworld scene.
        public List<RegionSaveData> regions = new List<RegionSaveData>();
    }

    [Serializable]
    public class RegionSaveData
    {
        public string             sceneName;
        public List<DefeatedEnemySaveData> defeated = new List<DefeatedEnemySaveData>();
    }

    [Serializable]
    public class DefeatedEnemySaveData
    {
        public string id;
        public bool   hasPosition;  // false for membership-only marks (no bone marker)
        public Vec3   position;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializer-friendly stand-in for Vector3. Newtonsoft chokes on UnityEngine.Vector3
    /// (it recurses into normalized/magnitude); this flat struct avoids that entirely.
    /// </summary>
    [Serializable]
    public struct Vec3
    {
        public float x, y, z;

        public Vec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static implicit operator Vec3(Vector3 v)  => new Vec3(v.x, v.y, v.z);
        public static implicit operator Vector3(Vec3 v)  => new Vector3(v.x, v.y, v.z);
    }

    [Serializable]
    public class NamedBlob
    {
        public string key;
        public string json;
    }

    /// <summary>
    /// Lightweight slot header written alongside each save so a load menu can list
    /// slots (playtime, location, party preview, timestamp) WITHOUT deserializing
    /// the full save file.
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        public int      slot;
        public string   savedAtIso;     // DateTime.UtcNow.ToString("o")
        public float    playSeconds;
        public string   locationScene;
        public int      partyCount;
        public string[] partyNames = Array.Empty<string>();
    }
}
