namespace Nusantara.SaveSystem
{
    // A plug-in point for bolt-on systems later (quests, inventory, flags, that kind of thing).
    //
    // The core stuff (party, player position, the defeated-enemy registry) gets captured
    // straight by SaveManager because it's foundational and the ordering matters. Anything
    // added later just implements this interface and registers itself with
    // SaveManager.Register(this) in OnEnable (and Unregister in OnDisable). It reads and
    // writes its own little blob through SaveData.GetModule / SetModule under its own
    // unique Key, so bolting on a new system never means touching the core SaveData.
    public interface ISaveParticipant
    {
        // A unique, never-changing key for this module (e.g. "quests", "inventory").
        string Key { get; }

        // Write this system's state into the snapshot (usually via data.SetModule).
        void Capture(SaveData data);

        // Read this system's state back out of the snapshot (usually via data.GetModule).
        void Restore(SaveData data);
    }
}
