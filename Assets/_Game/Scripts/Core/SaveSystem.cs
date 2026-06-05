using Nusantara.SaveSystem;

// A thin shortcut layer over SaveManager that lives in the global namespace, so older
// callers (MainMenuUI and friends) get a nice simple API. All the actual work happens
// inside SaveManager.
//
//   SaveSystem.HasSave()    -> does slot 0 have a save?
//   SaveSystem.Save()       -> snapshot + write slot 0 (only call from FreeRoam)
//   SaveSystem.Load()       -> load slot 0 (loads the saved scene, then restores it)
//   SaveSystem.DeleteSave() -> wipe slot 0
//   SaveSystem.NewGame()    -> reset playtime + world state for a brand-new game
//
// Pass a slot index (0..SaveManager.SlotCount-1) to aim at a specific slot.
public static class SaveSystem
{
    public static int  SlotCount                => SaveManager.SlotCount;
    public static bool HasSave(int slot = 0)   => SaveManager.HasSave(slot);
    public static bool Save(int slot = 0)      => SaveManager.Save(slot);
    public static bool Load(int slot = 0)      => SaveManager.Load(slot);
    public static void DeleteSave(int slot = 0) => SaveManager.DeleteSave(slot);
    public static void NewGame()                => SaveManager.NewGame();

    // Quick slot header for a load/save menu. Null if the slot's empty.
    public static SaveMetadata GetMetadata(int slot = 0) => SaveManager.GetMetadata(slot);

    // Old no-op we keep around for compatibility. Saving writes real data via Save() now.
    public static void MarkSaveExists() => SaveManager.Save();
}
