using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Osu-style parry mini-game. When an enemy attacks, a string of TAP circles pops up.
//
// How it goes:
//   1. BattleSystem calls Show() before it works out the enemy's damage.
//   2. An overlay appears with a header bar ("Enemy menyerang Target!").
//   3. TAP circles show up ONE AT A TIME at random spots. Each has a shrinking ring,
//      so tap it before the ring closes.
//   4. You have to tap EVERY circle. Miss even one and the whole parry fails right there.
//   5. Tap them all and the parry succeeds, so the incoming damage gets reduced.
//   6. Overlay hides and onComplete(grade) reports back to BattleSystem.
//
// Where to put it:
//   ParrySystem goes on the BattleSystem GameObject (which is always active), NOT on the
//   ParryOverlay panel. Assign the overlay panel to 'parryOverlay'.
public class ParrySystem : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The full overlay panel. Disabled by default; toggled at runtime.")]
    [SerializeField] private GameObject parryOverlay;

    [Header("Header UI")]
    [SerializeField] private TextMeshProUGUI attackIndicatorText; // "Enemy menyerang Target!"
    [SerializeField] private TextMeshProUGUI hintText;            // "Tap semua lingkaran!"
    [Tooltip("Optional timer bar (Slider, 0..1). Refills per circle and drains over its tap window. Leave null to disable.")]
    [SerializeField] private Slider          timerBar;

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

    private List<ParryButton> pooledButtons = new List<ParryButton>();

    void Awake()
    {
        if (parryOverlay != null) parryOverlay.SetActive(false);
    }

    // BattleSystem calls this before applying the enemy's damage.
    // buttonCount: how many TAP circles to show, one after another.
    // onComplete: gets the overall parry grade. Miss if ANY circle was missed, otherwise
    //             the worst tier landed (all Perfect = Perfect, any Good = Good). This
    //             decides how good the block and counter are.
    public IEnumerator Show(string attackerName, string targetName,
                             int buttonCount, Action<ParryTier> onComplete)
    {
        // Bail early (and count it as a miss) if something isn't wired up.
        if (parryOverlay == null)
        {
            Debug.LogError("[ParrySystem] parryOverlay is not assigned!");
            onComplete?.Invoke(ParryTier.Miss);
            yield break;
        }
        if (tapButtonPrefab == null)
        {
            Debug.LogError("[ParrySystem] tapButtonPrefab is not assigned!");
            onComplete?.Invoke(ParryTier.Miss);
            yield break;
        }
        if (buttonsContainer == null)
        {
            Debug.LogError("[ParrySystem] buttonsContainer is not assigned!");
            onComplete?.Invoke(ParryTier.Miss);
            yield break;
        }

        // Set up the header text.
        if (attackIndicatorText)
            attackIndicatorText.text = $"{attackerName} menyerang {targetName}!";

        if (hintText)
            hintText.text = "Tap semua lingkaran!";

        parryOverlay.SetActive(true);

        // Make sure we've got enough circles ready to go.
        EnsurePool(buttonCount);

        // Show the circles one at a time. We start hopeful (Perfect) and drop down to the
        // worst tier landed. A flat-out Miss fails the whole parry on the spot.
        ParryTier grade  = ParryTier.Perfect;
        bool      failed = false;

        for (int i = 0; i < buttonCount; i++)
        {
            var btn = pooledButtons[i];
            PlaceRandomly(btn.GetComponent<RectTransform>());

            yield return StartCoroutine(btn.Activate(buttonWindow, timerBar));

            if (btn.Result == ParryTier.Miss)
            {
                failed = true;
                if (hintText)
                {
                    hintText.text  = "Miss! Parry gagal!";
                    hintText.color = new Color(0.90f, 0.25f, 0.20f);
                }
                break; // no point showing the rest, the parry's already blown
            }

            // Drop the overall grade down to the worst non-miss tier we've seen.
            if (btn.Result == ParryTier.Good) grade = ParryTier.Good;

            // Tiny gap before the next circle.
            if (i < buttonCount - 1)
                yield return new WaitForSeconds(betweenDelay);
        }

        if (!failed && hintText)
        {
            hintText.text  = grade == ParryTier.Perfect ? "Sempurna!" : "Bagus!";
            hintText.color = grade == ParryTier.Perfect
                ? new Color(22f/255f, 17f/255f, 12f/255f)   // near-black for Perfect
                : new Color(0.30f, 0.85f, 0.40f);  // green
        }

        ParryTier finalGrade = failed ? ParryTier.Miss : grade;

        // Hold a moment so the last colour is readable, then close the overlay.
        yield return new WaitForSeconds(0.2f);
        parryOverlay.SetActive(false);

        // Put the hint colour back to white for next time.
        if (hintText) hintText.color = Color.white;

        Debug.Log($"[ParrySystem] Parry result: {finalGrade} ({buttonCount} circle(s))");
        onComplete?.Invoke(finalGrade);
    }

    // --- Helpers ---

    // Make more circles if we need them. We build them once and reuse them every battle.
    private void EnsurePool(int needed)
    {
        while (pooledButtons.Count < needed)
        {
            var go  = Instantiate(tapButtonPrefab, buttonsContainer);
            var btn = go.GetComponent<ParryButton>();
            if (btn == null)
            {
                Debug.LogError("[ParrySystem] tapButtonPrefab has no ParryButton component!");
                Destroy(go);
                continue;
            }
            go.SetActive(false); // ParryButton.Awake() already hides it, but let's be explicit
            pooledButtons.Add(btn);
        }
    }

    // Plops a circle down at a random spot inside the safe area of buttonsContainer.
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
