using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.SaveSystem
{
    // A plain serializable snapshot of everything we need to bring a session back to life.
    // Strongly typed and split into clear sections so the JSON stays easy to read, diff,
    // and version. Only stuff that CHANGES lives here; base stats stay in the assets.
    //
    // On versioning: saveVersion is the FIRST field. Bump CurrentVersion whenever the
    // shape changes and add a matching migration step in SaveManager so old saves still load.
    [Serializable]
    public class SaveData
    {
        // Bump this whenever SaveData's shape changes, and add a migration in SaveManager.
        public const int CurrentVersion = 4;   // v4: added level + currentExp per member

        public int saveVersion = CurrentVersion;

        public PlayerSaveData player = new PlayerSaveData();
        public PartySaveData  party  = new PartySaveData();
        public WorldSaveData  world  = new WorldSaveData();

        // Open slot for bolt-on systems later (quests, inventory, flags). ISaveParticipant
        // implementations stash their own string-keyed blob here, so adding a system never
        // means touching the core sections above.
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

    // --- The sections ---

    [Serializable]
    public class PlayerSaveData
    {
        public string sceneName;   // which overworld scene to drop back into
        public Vec3   position;
        public float  yaw;         // just future-proofing; the player root doesn't rotate yet
    }

    [Serializable]
    public class PartySaveData
    {
        public List<PartyMemberSaveData> members = new List<PartyMemberSaveData>();
    }

    [Serializable]
    public class PartyMemberSaveData
    {
        public string characterId;  // CharacterData.Id, looked up via GameDatabase on load
        public int    currentHp;
        public int    currentMp = -1;   // -1 means "unset", so restore to full MP (v1 saves migrate to this)

        // v3: loadout + battle selection.
        // equippedSkillIds = the SkillData.Id of the equipped NORMAL skills (matched
        // against the character's own pool on load). null/empty means restore the default loadout.
        public List<string> equippedSkillIds = new List<string>();
        // Whether this member fights or sits on the bench. Defaults to true so older saves
        // with no value (and freshly built parties) treat everyone as active.
        public bool isActive = true;

        // v4: leveling. level = 0 means "unset", so restore falls back to the character's
        // StartingLevel (that way old saves keep their starting level).
        public int level = 0;
        public int currentExp = 0;

        // Down the line: equipment ids, learned skill ids, etc. Add fields here.
    }

    [Serializable]
    public class WorldSaveData
    {
        // Defeated enemies remembered per region: one record per overworld scene.
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
        public bool   hasPosition;  // false when it's just a "this is dead" mark with no bone marker
        public Vec3   position;
    }

    // --- Helpers ---

    // A serializer-friendly stand-in for Vector3. Newtonsoft chokes on UnityEngine.Vector3
    // (it tries to recurse into normalized/magnitude), but this flat little struct dodges
    // that completely.
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

    // A small slot header saved next to each save, so a load menu can list slots
    // (playtime, location, party preview, timestamp) without having to read and unpack
    // the whole save file.
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
