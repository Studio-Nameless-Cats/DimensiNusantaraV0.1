using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    // One fragment of the menu's "energy layer" - the abstract brush / ink bursts
    // that bleed off the screen edge behind the panels.
    //
    // Setup: author the splatter PNGs in WHITE, drop one on an Image, add this,
    // pick a tint. The component colors it from the palette and locks the alpha
    // low so the layer stays a whisper behind the content. Add an IdleDrift
    // (UI/Motion) on top if you want it to slowly breathe.
    //
    // Keep the shard container OUTSIDE any RectMask2D - the whole point is that
    // these get cut by the screen edge, not by the panel.
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Nusantara/Ink Shard")]
    public class InkShard : MonoBehaviour
    {
        public enum Tint
        {
            Gold,      // bright kuning - use sparingly, it shouts
            DeepGold,  // emas tua - the workhorse shard color
            Red,       // bara merah - one or two per screen max, red means "look here"
            Gading     // off-white - subtle highlight slivers
        }

        [Header("Look")]
        [SerializeField] private Tint tint = Tint.DeepGold;
        [Tooltip("Shards live around 0.15 - 0.25. Past that they start competing with the content.")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 0.2f;

        [Header("Variety")]
        [Tooltip("On enable, randomly flip / rotate / scale a little so one PNG reads as many different shards.")]
        [SerializeField] private bool randomizeOnEnable = true;
        [SerializeField] private float rotationJitter = 12f;
        [SerializeField] private Vector2 scaleJitterRange = new Vector2(0.85f, 1.2f);

        void OnEnable()
        {
            Apply();
            if (randomizeOnEnable && Application.isPlaying) Randomize();
        }

        void OnValidate()
        {
            Apply();
        }

        void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null) return;

            Color c;
            switch (tint)
            {
                case Tint.Gold:     c = NusantaraPalette.Role.FieldBg; break;
                case Tint.DeepGold: c = NusantaraPalette.FieldDeep;    break;
                case Tint.Red:      c = NusantaraPalette.Role.Accent;  break;
                default:            c = NusantaraPalette.Role.OnDark;  break;
            }
            c.a = alpha;
            img.color = c;

            // pure decoration - never eat a click
            img.raycastTarget = false;
        }

        void Randomize()
        {
            var t = (RectTransform)transform;
            float flip = Random.value < 0.5f ? -1f : 1f;
            float s = Random.Range(scaleJitterRange.x, scaleJitterRange.y);
            t.localScale = new Vector3(s * flip, s, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-rotationJitter, rotationJitter));
        }
    }
}
