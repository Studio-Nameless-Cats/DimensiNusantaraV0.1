using UnityEngine;
using TMPro;

// Drives the top-right info stack in the overworld: the location plate, the quest
// tracker, and the (not-yet-built) day/time chip. Nothing here knows about combat -
// the overworld stays clean, no party HP bars (that's a battle thing).
//
// Most of this is just "set a string, show or hide a slab". The day/time chip and the
// quest tracker hang off systems that don't exist yet (Phase B day/night, the quest
// backend), so they ship hidden and we flip them on once those land. Everything is
// null-safe, so a half-wired HUD won't throw.
//
// Reskin notes (no code, done in the scene per UI_REWORK_PLAN section 2):
//   - Every slab is a sheared charcoal Image (UIShear ~11) bleeding off the RIGHT edge.
//   - Labels are italic TMP. Location = OnDark bold. Quest tag "TUGAS" = Warning edge.
//   - Day chip "HARI 1" gold + "SENJA" muted.
public class OverworldHud : MonoBehaviour
{
    // Handy for other systems (quest backend, Phase B clock) to find the HUD without
    // a scene reference. Last one alive in the scene wins; there should only be one.
    public static OverworldHud Instance { get; private set; }

    [Header("Location plate")]
    [Tooltip("Bold italic name of the current area, e.g. HUTAN WANAMARTA.")]
    [SerializeField] private TMP_Text locationLabel;
    [Tooltip("The location name THIS scene shows. Per-scene for now - just type it here in each overworld scene.")]
    [SerializeField] private string locationName = "HUTAN WANAMARTA";

    [Header("Quest tracker")]
    [Tooltip("The whole tracker slab (TUGAS tag + objective line). Stays hidden until the quest backend feeds it.")]
    [SerializeField] private GameObject questTrackerRoot;
    [Tooltip("The active objective line shown inside the tracker.")]
    [SerializeField] private TMP_Text questObjectiveLabel;
    [Tooltip("Show the tracker on start with the placeholder text below. Leave OFF to ship it hidden until quests exist.")]
    [SerializeField] private bool showQuestTracker = false;
    [Tooltip("Placeholder objective used when Show Quest Tracker is on and nothing real is driving it yet.")]
    [SerializeField] private string placeholderObjective = "Temukan jalan keluar dari hutan.";

    [Header("Day / time chip")]
    [Tooltip("The HARI 1 / SENJA chip. Day/night is Phase B - keep this hidden until then.")]
    [SerializeField] private GameObject dayTimeRoot;
    [Tooltip("Day part of the chip, e.g. HARI 1.")]
    [SerializeField] private TMP_Text dayLabel;
    [Tooltip("Time-of-day part, e.g. SENJA.")]
    [SerializeField] private TMP_Text timeLabel;
    [Tooltip("Day/night isn't built yet - leave OFF so the chip ships hidden.")]
    [SerializeField] private bool dayTimeEnabled = false;

    void Awake()
    {
        Instance = this;

        // location: just push the serialized name into the label.
        SetLocation(locationName);

        // quest tracker: stays off unless someone explicitly wants the placeholder.
        if (showQuestTracker) SetQuest(placeholderObjective);
        else HideQuest();

        // day/time chip: built but parked until Phase B turns it on.
        if (dayTimeRoot != null) dayTimeRoot.SetActive(dayTimeEnabled);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- location plate ----

    // Set the area name. Call from a per-scene setup if you'd rather drive it from
    // code than the serialized field.
    public void SetLocation(string area)
    {
        locationName = area;
        if (locationLabel != null) locationLabel.text = area;
    }

    // ---- quest tracker (hook the quest backend in here later) ----

    // Point the tracker at the active quest's current objective, and show the slab.
    // This is the seam the quest system plugs into: one tracked quest -> one call.
    public void SetQuest(string objective)
    {
        if (questObjectiveLabel != null) questObjectiveLabel.text = objective;
        if (questTrackerRoot != null) questTrackerRoot.SetActive(true);
    }

    // No active tracked quest - tuck the slab away.
    public void HideQuest()
    {
        if (questTrackerRoot != null) questTrackerRoot.SetActive(false);
    }

    // ---- day / time chip (Phase B) ----

    // Flip the chip on and set its text. Does nothing visible until the chip is wired
    // and day/night exists, but Phase B's clock just calls this each time-of-day change.
    public void SetDayTime(string day, string timeOfDay)
    {
        dayTimeEnabled = true;
        if (dayLabel != null) dayLabel.text = day;
        if (timeLabel != null) timeLabel.text = timeOfDay;
        if (dayTimeRoot != null) dayTimeRoot.SetActive(true);
    }

    public void HideDayTime()
    {
        dayTimeEnabled = false;
        if (dayTimeRoot != null) dayTimeRoot.SetActive(false);
    }
}
