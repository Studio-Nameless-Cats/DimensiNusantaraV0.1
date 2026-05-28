using UnityEngine;

/// <summary>
/// ScriptableObject holding the prefab references for *world markers* — visual
/// objects spawned in the overworld in response to gameplay state (e.g. bone /
/// scorch markers at defeated-enemy positions).
///
/// Why an SO and not a direct prefab field on GameController:
///   - Per-region swap. A jungle region can use bones; a volcano region can
///     use scorch marks. Just swap the SO on the GameController.
///   - Designer authorability. No code changes when the prefab changes.
///   - Future-proof. New marker types (quest pins, lore spots, treasure flags)
///     plug in here without bloating GameController's Inspector.
///
/// Create via: Right-click in Project → RPG → World Marker Data
/// </summary>
[CreateAssetMenu(fileName = "New World Markers", menuName = "RPG/World Marker Data")]
public class WorldMarkerData : ScriptableObject
{
    [Header("Bone Marker (defeated overworld enemy)")]
    [Tooltip("Prefab instantiated at the position where the player defeated an overworld enemy. " +
             "Should have a BoneMarker component on the root. Lives in the scene until the registry " +
             "is cleared (region change or rest action).")]
    [SerializeField] private GameObject boneMarkerPrefab;

    [Tooltip("Y-offset applied when spawning the marker. Useful if the defeat position is the enemy's " +
             "feet (Y≈0) but the prefab pivot is centred — bump this up so the visual sits on the ground.")]
    [SerializeField] private float boneMarkerYOffset = 0f;

    public GameObject BoneMarkerPrefab  => boneMarkerPrefab;
    public float      BoneMarkerYOffset => boneMarkerYOffset;
}
