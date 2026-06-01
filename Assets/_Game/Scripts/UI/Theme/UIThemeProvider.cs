using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the active <see cref="UITheme"/> for a scene and exposes it through a
/// static accessor so <see cref="ThemedElement"/>s can find it without each one
/// needing a manual reference. Drop ONE of these in each scene (e.g. on the
/// root Canvas) and assign the shared theme asset.
///
/// Re-skinning at runtime: call <see cref="ApplyTheme"/> with a different
/// UITheme and every ThemedElement in the scene refreshes immediately — handy
/// for theme-swap testing or future palette unlocks.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Add this component to the scene's root Canvas (or an empty "UI" object).
///   2. Assign the shared "Theme_Nusantara" asset to <c>theme</c>.
///   3. That's it — every ThemedElement pulls from here on Start.
/// </summary>
[DefaultExecutionOrder(-100)] // resolve the theme before ThemedElements run
public class UIThemeProvider : MonoBehaviour
{
    [Tooltip("The shared UI theme asset. Same asset in every scene keeps the look consistent.")]
    [SerializeField] private UITheme theme;

    /// <summary>The theme currently in effect (last provider to wake wins).</summary>
    public static UITheme Active { get; private set; }

    // Live elements that registered for runtime re-skinning.
    private static readonly List<ThemedElement> _subscribers = new List<ThemedElement>();

    void Awake()
    {
        if (theme != null)
            Active = theme;
    }

    /// <summary>Swaps the active theme and re-applies it to every live element.</summary>
    public void ApplyTheme(UITheme newTheme)
    {
        if (newTheme == null) return;
        theme  = newTheme;
        Active = newTheme;

        // Iterate over a copy — elements may unregister during Apply.
        foreach (var el in _subscribers.ToArray())
            if (el != null) el.Apply(newTheme);
    }

    // ── Registration (called by ThemedElement) ───────────────────────────────

    public static void Register(ThemedElement el)
    {
        if (el != null && !_subscribers.Contains(el))
            _subscribers.Add(el);
    }

    public static void Unregister(ThemedElement el)
    {
        _subscribers.Remove(el);
    }
}
