/// <summary>
/// Stub save system. Replace the body of each method when real save data is implemented.
///
/// Usage:
///   SaveSystem.HasSave()   → true if a save file exists (currently always false)
///   SaveSystem.DeleteSave() → wipes the save (no-op for now)
/// </summary>
public static class SaveSystem
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns true if a save file exists for the current player.</summary>
    public static bool HasSave()
    {
        // TODO: replace with real check, e.g.:
        //   return System.IO.File.Exists(SavePath);
        //   or: return PlayerPrefs.HasKey("SaveExists");
        return false;
    }

    /// <summary>Deletes the current save file.</summary>
    public static void DeleteSave()
    {
        // TODO: System.IO.File.Delete(SavePath);
        //       or: PlayerPrefs.DeleteKey("SaveExists");
    }

    /// <summary>Marks that a save exists (call this when the player saves).</summary>
    public static void MarkSaveExists()
    {
        // TODO: write save data here.
    }

    // ── Internals (uncomment when implementing) ───────────────────────────────
    // private static string SavePath =>
    //     System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "save.dat");
}
