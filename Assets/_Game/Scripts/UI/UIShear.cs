using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    // Skews a UI Graphic into a parallelogram - the house menu lean (~10-12 degrees).
    // Stick it next to any Image (chips, bars, buttons, header slabs), set the angle,
    // done. The RectTransform stays a normal rectangle, so layout groups, anchors and
    // clicks all behave like nothing happened.
    //
    // Two things to know:
    // - The raycast hit area stays rectangular (Unity tests the rect, not the mesh).
    //   For chip-sized elements nobody will ever notice.
    // - Don't put this on TMP text. Use the font's italic style instead - italic IS
    //   a vertex shear under the hood, so the leans match visually for free.
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Nusantara/UI Shear")]
    public class UIShear : BaseMeshEffect
    {
        [Tooltip("Lean angle in degrees. Positive leans the top edge to the right. House slant is 10-12.")]
        [Range(-30f, 30f)]
        [SerializeField] private float angle = 11f;

        public float Angle
        {
            get => angle;
            set
            {
                angle = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Approximately(angle, 0f)) return;

            float shear = Mathf.Tan(angle * Mathf.Deg2Rad);
            // pivot around the rect's vertical center so the slab leans in place
            // instead of sliding sideways
            float midY = ((RectTransform)transform).rect.center.y;

            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position.x += (v.position.y - midY) * shear;
                vh.SetUIVertex(v, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (graphic != null) graphic.SetVerticesDirty();
        }
#endif
    }
}
