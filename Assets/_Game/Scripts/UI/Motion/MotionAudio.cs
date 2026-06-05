using UnityEngine;

namespace Nusantara.UI.Motion
{
    // The ears of the menu. Listens to MotionEvents and plays the matching clip
    // from the profile - move blip, confirm hit, cancel thud, the entrance
    // whoosh. Motion code never touches audio directly; it just raises events
    // and this catches them, so designers can swap sounds in the profile without
    // anyone editing motion scripts.
    [RequireComponent(typeof(AudioSource))]
    public class MotionAudio : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private MotionProfile profile;

        [Header("Source")]
        [Tooltip("Where the SFX play from. Defaults to this object's AudioSource.")]
        [SerializeField] private AudioSource source;

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
        }

        void OnEnable()
        {
            MotionEvents.Move      += PlayMove;
            MotionEvents.Confirm   += PlayConfirm;
            MotionEvents.Cancel    += PlayCancel;
            MotionEvents.MenuEnter += PlayMenuEnter;
        }

        void OnDisable()
        {
            MotionEvents.Move      -= PlayMove;
            MotionEvents.Confirm   -= PlayConfirm;
            MotionEvents.Cancel    -= PlayCancel;
            MotionEvents.MenuEnter -= PlayMenuEnter;
        }

        private void PlayMove()      => Play(profile != null ? profile.moveSfx : null);
        private void PlayConfirm()   => Play(profile != null ? profile.confirmSfx : null);
        private void PlayCancel()    => Play(profile != null ? profile.cancelSfx : null);
        private void PlayMenuEnter() => Play(profile != null ? profile.menuEnterSfx : null);

        private void Play(AudioClip clip)
        {
            if (clip == null || source == null) return;
            source.PlayOneShot(clip, profile.sfxVolume);
        }
    }
}
