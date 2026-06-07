using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nusantara.UI.Motion;

// The one place all sound goes through. Sticks around across scenes
// (DontDestroyOnLoad) like GameController, so music keeps playing through a
// scene swap and any script can do AudioManager.Instance.PlaySfx("hit").
//
// What it does:
//   - BGM with crossfade: two music sources, one fades out while the other
//     fades in, so switching tracks never hard-cuts.
//   - SFX one-shots from a little pool of sources, so a bunch of sounds can
//     overlap without chopping each other off.
//   - Listens to the menu's MotionEvents bus and plays the UI blips/confirms,
//     so the existing button motion finally has a voice. Designers pick which
//     library sound is which via the four id fields below.
//
// Setup: drop this on a GameObject in your FIRST scene (MainMenu), assign an
// AudioLibrary asset, hit play. It survives from there on.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Library")]
    [Tooltip("The AudioLibrary asset with all music + sfx clips.")]
    [SerializeField] private AudioLibrary library;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;

    [Header("Music")]
    [Tooltip("How long a crossfade takes, in seconds, when no time is passed in.")]
    [SerializeField] private float defaultCrossfade = 1.0f;
    [Tooltip("Optional: play this music id automatically on boot. Leave empty for none.")]
    [SerializeField] private string playOnStart = "";

    [Header("SFX pool")]
    [Tooltip("How many sfx can overlap at once before the oldest gets reused.")]
    [SerializeField] private int sfxVoices = 8;

    [Header("UI sounds (from MotionEvents)")]
    [Tooltip("Library sfx id played when focus jumps to a new menu row. Empty = silent.")]
    [SerializeField] private string uiMoveSfx = "ui_move";
    [SerializeField] private string uiConfirmSfx = "ui_confirm";
    [SerializeField] private string uiCancelSfx = "ui_cancel";
    [SerializeField] private string uiMenuEnterSfx = "ui_menu_enter";

    // Two music players we ping-pong between for crossfades. _activeMusic is
    // whichever one is currently the "real" track; the other is the spare we
    // fade the next track in on.
    private AudioSource _musicA;
    private AudioSource _musicB;
    private AudioSource _activeMusic;
    private string _currentMusicId;

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    private int _sfxCursor;

    private Coroutine _fadeRoutine;

    void Awake()
    {
        // Standard singleton guard: if one already exists, this one's a dupe
        // (e.g. we walked back into a scene that also has an AudioManager) - bail.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSources();
        SubscribeUi();
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(playOnStart))
            PlayMusic(playOnStart);
    }

    void OnDestroy()
    {
        // Only unhook if WE are the live instance - a destroyed dupe must not
        // rip the events out from under the real one.
        if (Instance == this) UnsubscribeUi();
    }

    // Spin up the two music sources + the sfx pool, all as children so the
    // hierarchy stays tidy.
    private void BuildSources()
    {
        _musicA = MakeChildSource("Music A", loop: true);
        _musicB = MakeChildSource("Music B", loop: true);
        _activeMusic = _musicA;

        for (int i = 0; i < Mathf.Max(1, sfxVoices); i++)
            _sfxPool.Add(MakeChildSource("SFX " + i, loop: false));
    }

    private AudioSource MakeChildSource(string label, bool loop)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f; // pure 2D - this is UI/BGM, not positional world sound
        return src;
    }

    // ---- Music ----

    // Start a music track by its library id. Crossfades out whatever's playing.
    // Calling it with the track that's already playing does nothing (no restart).
    public void PlayMusic(string id) => PlayMusic(id, defaultCrossfade);

    public void PlayMusic(string id, float fadeSeconds)
    {
        if (library == null)
        {
            Debug.LogWarning("[AudioManager] No AudioLibrary assigned - can't play music '" + id + "'.");
            return;
        }
        if (id == _currentMusicId) return; // already on this track, leave it

        var sound = library.GetMusic(id);
        if (sound == null || sound.clip == null)
        {
            Debug.LogWarning("[AudioManager] Music id '" + id + "' not found in the library.");
            return;
        }

        _currentMusicId = id;

        // The spare source becomes the new active one; we fade the old active out
        // and the new track in over the same window.
        AudioSource incoming = (_activeMusic == _musicA) ? _musicB : _musicA;
        AudioSource outgoing = _activeMusic;

        incoming.clip = sound.clip;
        incoming.loop = sound.loop;
        incoming.volume = 0f;
        incoming.Play();

        float targetVol = musicVolume * sound.volume;
        StartFade(incoming, targetVol, outgoing, 0f, Mathf.Max(0f, fadeSeconds));
        _activeMusic = incoming;
    }

    // Fade the current music down to nothing and stop it.
    public void StopMusic(float fadeSeconds = 1f)
    {
        _currentMusicId = null;
        if (_activeMusic == null) return;
        StartFade(null, 0f, _activeMusic, 0f, Mathf.Max(0f, fadeSeconds));
    }

    private void StartFade(AudioSource inSrc, float inTarget, AudioSource outSrc, float outTarget, float seconds)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(inSrc, inTarget, outSrc, outTarget, seconds));
    }

    private IEnumerator FadeRoutine(AudioSource inSrc, float inTarget, AudioSource outSrc, float outTarget, float seconds)
    {
        float inStart = inSrc != null ? inSrc.volume : 0f;
        float outStart = outSrc != null ? outSrc.volume : 0f;

        if (seconds <= 0.001f)
        {
            // No fade asked for - just snap to the targets.
            if (inSrc != null) inSrc.volume = inTarget;
            if (outSrc != null) { outSrc.volume = outTarget; if (outTarget <= 0f) outSrc.Stop(); }
            _fadeRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            // Unscaled so pausing the game (timeScale 0) doesn't freeze the fade.
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            if (inSrc != null) inSrc.volume = Mathf.Lerp(inStart, inTarget, k);
            if (outSrc != null) outSrc.volume = Mathf.Lerp(outStart, outTarget, k);
            yield return null;
        }

        if (inSrc != null) inSrc.volume = inTarget;
        if (outSrc != null)
        {
            outSrc.volume = outTarget;
            if (outTarget <= 0f) outSrc.Stop(); // fully faded out, free it up
        }
        _fadeRoutine = null;
    }

    // ---- SFX ----

    // Play a sound effect by library id. Grabs the next free-ish voice from the
    // pool so overlapping sfx don't cut each other off.
    public void PlaySfx(string id)
    {
        if (library == null) return;
        var sound = library.GetSfx(id);
        if (sound == null || sound.clip == null)
        {
            if (!string.IsNullOrEmpty(id))
                Debug.LogWarning("[AudioManager] Sfx id '" + id + "' not found in the library.");
            return;
        }
        // Roll a fresh pitch each time if the clip wants jitter, so spammed sfx stay lively.
        float pitch = sound.pitchJitter > 0f
            ? 1f + Random.Range(-sound.pitchJitter, sound.pitchJitter)
            : 1f;
        PlaySfx(sound.clip, sound.volume, pitch);
    }

    // Play a raw clip directly, for cases where you already have the AudioClip
    // and don't need a library entry. clipVolume is a 0..1 trim on top of the
    // global sfx volume; pitch lets you speed/slow it (1 = normal).
    public void PlaySfx(AudioClip clip, float clipVolume = 1f, float pitch = 1f)
    {
        if (clip == null || _sfxPool.Count == 0) return;
        var src = NextSfxSource();
        src.pitch = pitch;
        src.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(clipVolume));
    }

    // Round-robin through the pool. Prefer a source that's gone quiet; if they're
    // all busy, reuse the oldest one (PlayOneShot layers anyway, so this is fine).
    private AudioSource NextSfxSource()
    {
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            var src = _sfxPool[i];
            if (!src.isPlaying) return src;
        }
        var pick = _sfxPool[_sfxCursor];
        _sfxCursor = (_sfxCursor + 1) % _sfxPool.Count;
        return pick;
    }

    // ---- Volume ----

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        // Nudge the live track so the change is heard right away (not on next swap).
        if (_activeMusic != null && _activeMusic.isPlaying && _currentMusicId != null)
        {
            var s = library != null ? library.GetMusic(_currentMusicId) : null;
            float per = s != null ? s.volume : 1f;
            _activeMusic.volume = musicVolume * per;
        }
    }

    public void SetSfxVolume(float v) => sfxVolume = Mathf.Clamp01(v);

    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;

    // What's playing right now (null if nothing). Handy for "only swap if different"
    // checks at call sites, or to restore the same track after something.
    public string CurrentMusicId => _currentMusicId;
    public bool IsMusicPlaying(string id) => _currentMusicId == id;

    // ---- UI motion hooks ----

    // The menu shouts these on the MotionEvents bus; we just translate each into
    // a library sfx. Empty id = that event stays silent, which is fine.
    private void SubscribeUi()
    {
        MotionEvents.Move      += OnUiMove;
        MotionEvents.Confirm   += OnUiConfirm;
        MotionEvents.Cancel    += OnUiCancel;
        MotionEvents.MenuEnter += OnUiMenuEnter;
    }

    private void UnsubscribeUi()
    {
        MotionEvents.Move      -= OnUiMove;
        MotionEvents.Confirm   -= OnUiConfirm;
        MotionEvents.Cancel    -= OnUiCancel;
        MotionEvents.MenuEnter -= OnUiMenuEnter;
    }

    private void OnUiMove()      => PlaySfx(uiMoveSfx);
    private void OnUiConfirm()   => PlaySfx(uiConfirmSfx);
    private void OnUiCancel()    => PlaySfx(uiCancelSfx);
    private void OnUiMenuEnter() => PlaySfx(uiMenuEnterSfx);
}
