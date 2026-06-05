using UnityEngine;

// A ScriptableObject holding the prefab references for "world markers": little visual
// things we spawn in the overworld based on game state (like bone or scorch markers where
// you beat an enemy).
//
// Why an SO instead of just a prefab field on GameController:
//   - You can swap per region. A jungle area uses bones, a volcano area uses scorch
//     marks; just swap the SO on the GameController.
//   - Designers can change it. No code edits when the prefab changes.
//   - Room to grow. New marker types (quest pins, lore spots, treasure flags) drop in
//     here without cluttering GameController's Inspector.
//
// Make one with: Right-click in Project -> RPG -> World Marker Data
[CreateAssetMenu(fileName = "New World Markers", menuName = "RPG/World Marker Data")]
public class WorldMarkerData : ScriptableObject
{
    [Header("Bone Marker (defeated overworld enemy)")]
    [Tooltip("Prefab instantiated at the position where the player defeated an overworld enemy. " +
             "Should have a BoneMarker component on the root. Lives in the scene until the registry " +
             "is cleared (region change or rest action).")]
    [SerializeField] private GameObject boneMarkerPrefab;

    [Tooltip("Y-offset added when spawning the marker. Handy if the defeat position is the enemy's " +
             "feet (Y near 0) but the prefab's pivot is centred; bump this up so the visual sits on the ground.")]
    [SerializeField] private float boneMarkerYOffset = 0f;

    public GameObject BoneMarkerPrefab  => boneMarkerPrefab;
    public float      BoneMarkerYOffset => boneMarkerYOffset;
}
