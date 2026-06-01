namespace Nusantara.SaveSystem
{
    /// <summary>
    /// Optional extension seam for modular systems (quests, inventory, flags, etc.).
    ///
    /// The CORE systems — party, player position, defeated-enemy registry — are
    /// captured directly by <c>SaveManager</c> because they need precise ordering
    /// and are foundational. FUTURE systems implement this interface and register
    /// with <c>SaveManager.Register(this)</c> in OnEnable / Unregister in OnDisable.
    /// They read and write their own blob via <see cref="SaveData.GetModule"/> /
    /// <see cref="SaveData.SetModule"/> under a unique <see cref="Key"/>, so adding
    /// a system never edits the core SaveData sections.
    /// </summary>
    public interface ISaveParticipant
    {
        /// <summary>Unique, stable module key (e.g. "quests", "inventory").</summary>
        string Key { get; }

        /// <summary>Write this system's state into the snapshot (typically via data.SetModule).</summary>
        void Capture(SaveData data);

        /// <summary>Read this system's state back from the snapshot (typically via data.GetModule).</summary>
        void Restore(SaveData data);
    }
}
