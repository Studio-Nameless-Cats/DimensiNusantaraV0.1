using Nusantara.SaveSystem;

/// <summary>
/// Thin, global-namespace facade over <see cref="SaveManager"/> so existing callers
/// (MainMenuUI, etc.) keep a simple API. All real work lives in SaveManager.
///
///   SaveSystem.HasSave()    → does slot 0 have a save?
///   SaveSystem.Save()       → snapshot + write slot 0 (call from FreeRoam only)
///   SaveSystem.Load()       → load slot 0 (loads the saved scene, then restores)
///   SaveSystem.DeleteSave() → wipe slot 0
///   SaveSystem.NewGame()    → reset playtime + world state for a fresh game
///
/// Pass a slot index (0..SaveManager.SlotCount-1) to target a specific slot.
/// </summary>
public static class SaveSystem
{
    public static int  SlotCount                => SaveManager.SlotCount;
    public static bool HasSave(int slot = 0)   => SaveManager.HasSave(slot);
    public static bool Save(int slot = 0)      => SaveManager.Save(slot);
    public static bool Load(int slot = 0)      => SaveManager.Load(slot);
    public static void DeleteSave(int slot = 0) => SaveManager.DeleteSave(slot);
    public static void NewGame()                => SaveManager.NewGame();

    /// <summary>Lightweight slot header for a load/save menu — null if the slot is empty.</summary>
    public static SaveMetadata GetMetadata(int slot = 0) => SaveManager.GetMetadata(slot);

    /// <summary>Legacy no-op kept for compatibility — saving now writes real data via Save().</summary>
    public static void MarkSaveExists() => SaveManager.Save();
}
