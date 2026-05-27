using UnityEngine;

/// <summary>
/// Runtime visualisation of the enemy's perception cone as a filled pie slice.
/// Builds a procedural triangle-fan mesh in local space and tints all vertices
/// by the enemy's current state so the player can read "calm / chasing /
/// searching" at a glance.
///
/// Lives on a child GameObject of the enemy so that — unlike the sprite — it
/// rotates with the parent's <c>transform.forward</c> (the cone should always
/// point where the enemy is looking).
///
/// Setup:
///   1. Create a child GameObject under the enemy (e.g. "VisionCone").
///   2. Local Position = (0, 0, 0), Local Rotation = (0, 0, 0). Do NOT add
///      LockWorldRotation here — the cone must rotate with the parent.
///   3. Add a MeshFilter and a MeshRenderer.
///   4. On the MeshRenderer, assign a transparent material. Easiest placeholder:
///      "Sprites-Default" (built-in, two-sided, supports vertex colors).
///      Disable Cast Shadows / Receive Shadows.
///   5. Add this component. Leave <c>owner</c> empty — it auto-binds via
///      GetComponentInParent in Awake.
///   6. (Optional) Tune the three state colors in the Inspector.
///
/// The mesh is owned by this component and destroyed on teardown — no asset
/// pollution.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class VisionConeRenderer : MonoBehaviour
{
    [Tooltip("The enemy this cone belongs to. Auto-found in the parent hierarchy if left empty.")]
    [SerializeField] private OverworldEnemyController owner;

    [Header("Geometry")]
    [Tooltip("Number of segments along the cone arc. Higher = smoother curve.")]
    [Range(4, 64)]
    [SerializeField] private int segments = 24;

    [Tooltip("Vertical offset so the cone sits just above the floor and is visible.")]
    [SerializeField] private float yOffset = 0.05f;

    [Header("Colors (per state)")]
    [SerializeField] private Color idleColor        = new Color(1f,   0.85f, 0.1f, 0.35f); // yellow
    [SerializeField] private Color chaseColor       = new Color(1f,   0.2f,  0.2f, 0.5f);  // red
    [SerializeField] private Color investigateColor = new Color(0.5f, 0.6f,  1f,   0.4f);  // blue

    // ── Runtime ─────────────────────────────────────────────────────────────
    private MeshFilter   filter;
    private MeshRenderer mr;
    private Mesh         mesh;

    private Vector3[] vertices;
    private Color[]   colors;
    private int[]     triangles;
    private int       lastSegments = -1;

    void Awake()
    {
        filter = GetComponent<MeshFilter>();
        mr     = GetComponent<MeshRenderer>();

        if (owner == null)
            owner = GetComponentInParent<OverworldEnemyController>();

        RebuildScaffold();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    void LateUpdate()
    {
        if (owner == null || owner.AIData == null)
        {
            mr.enabled = false;
            return;
        }
        mr.enabled = true;

        // Inspector tweak to "segments" while running? Rebuild the index buffer.
        if (segments != lastSegments)
            RebuildScaffold();

        UpdateGeometry();
    }

    // ── Mesh construction ────────────────────────────────────────────────────

    /// <summary>
    /// Allocates buffers and writes the triangle list. Vertex positions and
    /// colors are filled per-frame in UpdateGeometry.
    /// </summary>
    private void RebuildScaffold()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "VisionCone (runtime)" };
            mesh.hideFlags = HideFlags.DontSave;
            mesh.MarkDynamic();
            filter.mesh = mesh;
        }

        int vertCount = segments + 2;     // origin + (segments + 1) arc points
        int triCount  = segments;         // one triangle per segment

        vertices  = new Vector3[vertCount];
        colors    = new Color  [vertCount];
        triangles = new int    [triCount * 3];

        for (int i = 0; i < segments; i++)
        {
            // Winding: clockwise when viewed from +Y (i.e. top-down camera) so
            // the cone is front-facing under default back-face culling. If you
            // use a two-sided material (Sprites-Default), winding is moot.
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        // Push the static index buffer once. Vertices/colors are written each frame.
        mesh.Clear();
        mesh.vertices  = vertices;
        mesh.colors    = colors;
        mesh.triangles = triangles;

        lastSegments = segments;
    }

    private void UpdateGeometry()
    {
        var   data      = owner.AIData;
        float halfAngle = data.VisionHalfAngle;
        float range     = data.VisionRange;
        Color c         = ColorForState(owner.CurrentState);

        Vector3 origin = new Vector3(0f, yOffset, 0f);
        vertices[0] = origin;
        colors[0]   = c;

        for (int i = 0; i <= segments; i++)
        {
            float t   = (float)i / segments;
            float deg = Mathf.Lerp(-halfAngle, halfAngle, t);
            float rad = deg * Mathf.Deg2Rad;

            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            vertices[i + 1] = origin + dir * range;
            colors  [i + 1] = c;
        }

        mesh.vertices = vertices;
        mesh.colors   = colors;
        mesh.RecalculateBounds(); // so frustum culling sees the cone's actual extent
    }

    private Color ColorForState(IEnemyState state)
    {
        if (state is ChaseState)       return chaseColor;
        if (state is InvestigateState) return investigateColor;
        // Idle, Patrol, Attack, null — all fall back to the calm color.
        return idleColor;
    }
}
