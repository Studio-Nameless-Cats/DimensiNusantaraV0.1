using System.Collections.Generic;
using UnityEngine;

// The sound cookbook. One asset that lists every music track and sound effect
// in the game, each under a short id like "battle_theme" or "ui_confirm". The
// AudioManager reads from here, so adding or swapping a sound is just editing
// this asset - no code changes, no hunting through scenes.
//
// Make one via: Create -> RPG -> Audio Library, fill in the two lists, then
// drop it on the AudioManager.
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "RPG/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    // One named sound. Works for both music and sfx - music usually loops,
    // sfx usually doesn't. Volume is a per-clip trim so you can tame that one
    // sound that's way too loud without re-exporting the file.
    [System.Serializable]
    public class Sound
    {
        [Tooltip("Short id you call it by, e.g. battle_theme or ui_confirm. Keep it unique.")]
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Loop it? Music usually yes, sfx usually no.")]
        public bool loop = false;
        [Tooltip("Randomizes pitch a bit each play so repeated sfx (hits, footsteps) don't sound robotic. 0 = always same pitch. Try 0.08 for a subtle wobble. Leave 0 for music.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0f;
    }

    [Header("Music tracks (BGM)")]
    public List<Sound> music = new List<Sound>();

    [Header("Sound effects")]
    public List<Sound> sfx = new List<Sound>();

    // Built once on first lookup so we're not scanning the lists every call.
    private Dictionary<string, Sound> _musicMap;
    private Dictionary<string, Sound> _sfxMap;

    // Grab a music entry by id, or null if it's not in the list.
    public Sound GetMusic(string id)
    {
        if (_musicMap == null) _musicMap = Build(music);
        if (string.IsNullOrEmpty(id)) return null;
        _musicMap.TryGetValue(id, out var s);
        return s;
    }

    // Grab a sfx entry by id, or null if it's not there.
    public Sound GetSfx(string id)
    {
        if (_sfxMap == null) _sfxMap = Build(sfx);
        if (string.IsNullOrEmpty(id)) return null;
        _sfxMap.TryGetValue(id, out var s);
        return s;
    }

    private static Dictionary<string, Sound> Build(List<Sound> list)
    {
        var map = new Dictionary<string, Sound>();
        if (list == null) return map;
        foreach (var s in list)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            // Last one wins if someone double-typed an id, and we warn so it's not a silent mystery.
            if (map.ContainsKey(s.id))
                Debug.LogWarning("[AudioLibrary] Duplicate id '" + s.id + "' - the later one will be used.");
            map[s.id] = s;
        }
        return map;
    }

    // If you edit the lists at runtime (rare), call this so the next lookup rebuilds.
    public void InvalidateCache()
    {
        _musicMap = null;
        _sfxMap = null;
    }
}
