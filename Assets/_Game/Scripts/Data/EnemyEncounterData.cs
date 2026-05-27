using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines which enemies can appear in a specific area.
/// Assign this to EncounterTrigger components on grass/encounter zones.
/// Create via: Right-click in Project → RPG → Enemy Encounter
/// </summary>
[CreateAssetMenu(fileName = "New Encounter", menuName = "RPG/Enemy Encounter")]
public class EnemyEncounterData : ScriptableObject
{
    [System.Serializable]
    public class EnemyEntry
    {
        public CharacterData characterData;
        [Range(1, 100)]
        [Tooltip("Relative spawn weight. Higher = more likely to appear.")]
        public int spawnWeight = 50;
    }

    [Header("Enemies")]
    [SerializeField] private List<EnemyEntry> possibleEnemies;

    [Header("Group Size")]
    [SerializeField] [Range(1, 4)] private int minEnemies = 1;
    [SerializeField] [Range(1, 4)] private int maxEnemies = 3;

    /// <summary>
    /// Returns a random list of CharacterData for the enemies in this encounter.
    /// </summary>
    public List<CharacterData> GetRandomEnemies()
    {
        var result = new List<CharacterData>();

        if (possibleEnemies == null || possibleEnemies.Count == 0)
        {
            Debug.LogWarning($"[EnemyEncounterData] {name} has no enemies configured!");
            return result;
        }

        int count = Random.Range(minEnemies, maxEnemies + 1);
        for (int i = 0; i < count; i++)
        {
            var enemy = PickRandomEnemy();
            if (enemy != null) result.Add(enemy);
        }

        return result;
    }

    private CharacterData PickRandomEnemy()
    {
        int totalWeight = 0;
        foreach (var entry in possibleEnemies)
            totalWeight += entry.spawnWeight;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var entry in possibleEnemies)
        {
            cumulative += entry.spawnWeight;
            if (roll < cumulative) return entry.characterData;
        }

        return possibleEnemies[0].characterData;
    }
}
