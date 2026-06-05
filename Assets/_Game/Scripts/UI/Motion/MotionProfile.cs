using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // The one knob box for all menu motion. Every duration, ease, offset and
    // sound the UI uses lives here so we tune the whole feel in one Inspector
    // and never hand-pick magic numbers per button. Defaults are the locked
    // values from ANIM_DESIGN_PLAN.md section 3 - change them here, the whole
    // menu follows.
    //
    // Make one asset: Assets > Create > Nusantara > Motion Profile, name it
    // "MainMenuMotionProfile", drag it onto every motion component.
    [CreateAssetMenu(fileName = "MainMenuMotionProfile", menuName = "Nusantara/Motion Profile")]
    public class MotionProfile : ScriptableObject
    {
        [Header("Skew slide (the main entrance move)")]
        [Tooltip("Where an element starts before it flies to rest. Points back along the skew diagonal so things slide in on the lean, not straight up.")]
        public Vector2 skewSlideOffset = new Vector2(-220f, 46f);

        [Header("Fast in (SkewSlideIn)")]
        [Tooltip("How long the snap-in takes. Persona is fast first, bouncy second.")]
        public float fastInDuration = 0.28f;
        public Ease fastInEase = Ease.OutBack;
        [Tooltip("How hard OutBack punches past the target before settling. 1.6 is a nice slam.")]
        public float fastInOvershoot = 1.6f;

        [Header("Fast out (SkewSlideOut)")]
        public float fastOutDuration = 0.20f;
        public Ease fastOutEase = Ease.InBack;

        [Header("Cascade")]
        [Tooltip("Gap between each item in a staggered list entrance.")]
        public float cascadeStagger = 0.06f;

        [Header("Select pop (focused item)")]
        public float selectPopDuration = 0.12f;
        public Ease selectPopEase = Ease.OutBack;
        public float selectPopOvershoot = 1.6f;
        [Tooltip("Scale multiplier on the focused row.")]
        public float selectPopScale = 1.10f;
        [Tooltip("Sideways shove on focus, in pixels. Negative nudges left.")]
        public float selectPopShiftX = -12f;

        [Header("Deselect (back to rest)")]
        public float deselectDuration = 0.10f;
        public Ease deselectEase = Ease.OutQuad;

        [Header("Color slam (hard color change on select)")]
        public float colorSlamDuration = 0.08f;
        public Ease colorSlamEase = Ease.OutQuad;
        [Tooltip("Fill color a row slams to when focused (Bara Merah red by default).")]
        public Color selectedFillColor = new Color(0.886f, 0.227f, 0.118f, 1f); // E23A1E
        [Tooltip("Text color a row slams to when focused (Gading ivory by default).")]
        public Color selectedTextColor = new Color(0.937f, 0.902f, 0.823f, 1f); // EFE6D2

        [Header("Idle drift (nothing rests)")]
        [Tooltip("One full loop of a resting wobble.")]
        public float idleDriftDuration = 3.0f;
        public Ease idleDriftEase = Ease.InOutSine;
        [Tooltip("How far a drifting element travels from rest, in pixels.")]
        public float idleDriftAmplitude = 6f;
        [Tooltip("How far a bobbing element rotates from rest, in degrees.")]
        public float idleDriftRotation = 1.5f;
        [Tooltip("Tiny scale wobble for shimmer drift, e.g. 0.02 = breathes +/-2%.")]
        public float idleDriftScale = 0.02f;

        [Header("Shadow lag (the signature desync)")]
        [Tooltip("Shadow layer takes this much longer than the main layer to arrive.")]
        public float shadowLagDurationMult = 1.1f;
        [Tooltip("Shadow starts moving this many seconds after the main layer.")]
        public float shadowLagDelay = 0.06f;
        [Tooltip("How lazily the continuous shadow follower chases the main layer. Bigger = snappier.")]
        public float shadowFollowSmoothing = 14f;

        [Header("Screen wipe (scene transition)")]
        public float screenWipeDuration = 0.35f;
        public Ease screenWipeEase = Ease.InOutQuad;

        [Header("Panel motion (PanelMotion component)")]
        [Tooltip("How small a ScalePop element starts before springing to full size. 0.6 = starts at 60%.")]
        public float popInStartScale = 0.6f;

        [Header("Pulse (one-shot attention beat)")]
        public float pulseDuration = 0.25f;
        [Tooltip("How much the punch scales up before snapping back.")]
        public float pulsePunch = 0.18f;
        public int pulseVibrato = 8;
        public float pulseElasticity = 0.6f;

        [Header("Global")]
        [Tooltip("Ignore timeScale so menus still animate while the game is paused.")]
        public bool useUnscaledTime = true;

        [Header("Audio clips")]
        [Tooltip("Short bright blip on focus change.")]
        public AudioClip moveSfx;
        [Tooltip("Punchy positive hit on confirm.")]
        public AudioClip confirmSfx;
        [Tooltip("Lower thud on cancel / back.")]
        public AudioClip cancelSfx;
        [Tooltip("One-shot whoosh under the entrance cascade.")]
        public AudioClip menuEnterSfx;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
    }
}
