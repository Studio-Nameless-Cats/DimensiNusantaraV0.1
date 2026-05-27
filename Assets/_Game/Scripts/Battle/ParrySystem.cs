using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Osu-style Parry mini-game — enemy attacks trigger a sequence of TAP circles.
///
/// Flow:
///   1. BattleSystem calls Show() before enemy damage is calculated.
///   2. Overlay appears with a header bar ("⚠ Enemy menyerang Target!").
///   3. TAP circles appear ONE AT A TIME at random positions.
///      Each circle has a shrinking approach ring — tap it before the ring closes.
///   4. Player MUST tap EVERY circle. Missing even one circle → parry FAILS immediately.
///   5. If all circles were tapped → parry SUCCESS → damage reduced.
///   6. Overlay hides, onComplete(bool success) fires back to BattleSystem.
///
/// Component placement:
///   Put ParrySystem on the BattleSystem GameObject (always active),
///   NOT on the ParryOverlay panel. Assign the overlay panel to 'parryOverlay'.
/// </summary>
public class ParrySystem : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The full overlay panel. Disabled by default; toggled at runtime.")]
    [SerializeField] private GameObject parryOverlay;

    [Header("Header UI")]
    [SerializeField] private TextMeshProUGUI attackIndicatorText; // "⚠ Enemy menyerang Target!"
    [SerializeField] private TextMeshProUGUI hintText;            // "Tap semua lingkaran!"

    [Header("Buttons")]
    [Tooltip("Prefab with Button + Image + ApproachRing child + ParryButton script.")]
    [SerializeField] private GameObject   tapButtonPrefab;
    [Tooltip("RectTransform of the safe area where TAP circles can spawn.")]
    [SerializeField] private RectTransform buttonsContainer;

    [Header("Settings")]
    [Tooltip("How long (seconds) each circle's tap window stays open.")]
    [SerializeField] private float buttonWindow   = 1.2f;
    [Tooltip("Brief pause between one circle disappearing and the next appearing.")]
    [SerializeField] private float betweenDelay   = 0.15f;
    [Tooltip("Keep circles this far from the container edge so they're fully visible.")]
    [SerializeField] private float spawnMargin    = 60f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private List<ParryButton> pooledButtons = new List<ParryButton>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (parryOverlay != null) parryOverlay.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by BattleSystem before enemy damage is applied.
    /// buttonCount : how many sequential TAP circles to show.
    /// onComplete  : receives true only if ALL circles were tapped in time.
    /// </summary>
    public IEnumerator Show(string attackerName, string targetName,
                             int buttonCount, Action<bool> onComplete)
    {
        // ── Validate ──────────────────────────────────────────────────────────
        if (parryOverlay == null)
        {
            Debug.LogError("[ParrySystem] parryOverlay is not assigned! ❌");
            onComplete?.Invoke(false);
            yield break;
        }
        if (tapButtonPrefab == null)
        {
            Debug.LogError("[ParrySystem] tapButtonPrefab is not assigned! ❌");
            onComplete?.Invoke(false);
            yield break;
        }
        if (buttonsContainer == null)
        {
            Debug.LogError("[ParrySystem] buttonsContainer is not assigned! ❌");
            onComplete?.Invoke(false);
            yield break;
        }

        // ── Setup header text ─────────────────────────────────────────────────
        if (attackIndicatorText)
            attackIndicatorText.text = $"⚠ {attackerName} menyerang {targetName}!";

        if (hintText)
            hintText.text = "Tap semua lingkaran!";

        parryOverlay.SetActive(true);

        // ── Ensure we have enough pooled buttons ──────────────────────────────
        EnsurePool(buttonCount);

        // ── Sequential circle loop ────────────────────────────────────────────
        bool allTapped = true;

        for (int i = 0; i < buttonCount; i++)
        {
            var btn = pooledButtons[i];
            PlaceRandomly(btn.GetComponent<RectTransform>());

            yield return StartCoroutine(btn.Activate(buttonWindow));

            if (!btn.WasTapped)
            {
                allTapped = false;
                // Flash hintText red to signal failure feedback
                if (hintText)
                {
                    hintText.text  = "Miss! Parry gagal!";
                    hintText.color = new Color(0.90f, 0.25f, 0.20f);
                }
                break; // fail fast — no need to show remaining circles
            }

            // Short gap between circles
            if (i < buttonCount - 1)
                yield return new WaitForSeconds(betweenDelay);
        }

        // ── Close overlay ─────────────────────────────────────────────────────
        // Small pause so the last feedback colour is visible
        yield return new WaitForSeconds(0.2f);
        parryOverlay.SetActive(false);

        // Reset hint colour for next time
        if (hintText) hintText.color = Color.white;

        Debug.Log($"[ParrySystem] Parry {(allTapped ? "SUCCESS ✅" : "FAILED ❌")} ({buttonCount} circle(s))");
        onComplete?.Invoke(allTapped);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Grow the pool if needed — buttons are instantiated once, reused each battle.</summary>
    private void EnsurePool(int needed)
    {
        while (pooledButtons.Count < needed)
        {
            var go  = Instantiate(tapButtonPrefab, buttonsContainer);
            var btn = go.GetComponent<ParryButton>();
            if (btn == null)
            {
                Debug.LogError("[ParrySystem] tapButtonPrefab has no ParryButton component! ❌");
                Destroy(go);
                continue;
            }
            go.SetActive(false); // ParryButton.Awake() already does this, but be explicit
            pooledButtons.Add(btn);
        }
    }

    /// <summary>Move a circle to a random position inside the safe area of buttonsContainer.</summary>
    private void PlaceRandomly(RectTransform rt)
    {
        if (rt == null) return;

        float halfW = buttonsContainer.rect.width  * 0.5f;
        float halfH = buttonsContainer.rect.height * 0.5f;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        float x = UnityEngine.Random.Range(-halfW + spawnMargin, halfW - spawnMargin);
        float y = UnityEngine.Random.Range(-halfH + spawnMargin, halfH - spawnMargin);
        rt.anchoredPosition = new Vector2(x, y);
    }
}
