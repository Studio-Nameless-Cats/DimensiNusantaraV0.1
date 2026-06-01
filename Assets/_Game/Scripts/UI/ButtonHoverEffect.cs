using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop this on any Button to add a scale punch + tint on hover and press.
/// Works alongside Unity's built-in Button transition — set the Button's
/// Transition to "None" if you want this script to own all visual feedback,
/// or leave it on "Color Tint" and this script will layer on top.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Add this component to the same GameObject as your Button.
///   2. Assign the <c>targetGraphic</c> (usually the Button's background Image).
///      If left null, the script grabs the Button's targetGraphic automatically.
///   3. Tune hoverScale, pressScale, hoverColor, pressColor, and duration
///      in the Inspector. Defaults look good for most UI.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Scale")]
    [Tooltip("Scale when the cursor hovers over the button.")]
    [SerializeField] private float hoverScale = 1.08f;

    [Tooltip("Scale when the button is pressed.")]
    [SerializeField] private float pressScale = 0.95f;

    [Tooltip("Seconds to reach the target scale.")]
    [SerializeField] private float duration = 0.08f;

    [Header("Tint")]
    [Tooltip("Tint applied on hover. White = no change.")]
    [SerializeField] private Color hoverColor = new Color(1f, 0.92f, 0.7f, 1f);  // warm gold tint

    [Tooltip("Tint applied on press.")]
    [SerializeField] private Color pressColor = new Color(0.8f, 0.7f, 0.5f, 1f);

    [Tooltip("Base/normal tint (restored on exit).")]
    [SerializeField] private Color normalColor = Color.white;

    [Header("Target")]
    [Tooltip("Graphic to tint. Leave null to use the Button's own targetGraphic.")]
    [SerializeField] private Graphic targetGraphic;

    // ── State ─────────────────────────────────────────────────────────────────
    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;
    private bool _isHovered;

    void Awake()
    {
        _originalScale = transform.localScale;

        if (targetGraphic == null)
        {
            Button btn = GetComponent<Button>();
            targetGraphic = btn != null ? btn.targetGraphic : GetComponent<Graphic>();
        }

        if (targetGraphic != null)
            targetGraphic.color = normalColor;
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        _isHovered = true;
        SetScale(hoverScale);
        SetColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData _)
    {
        _isHovered = false;
        SetScale(1f);
        SetColor(normalColor);
    }

    public void OnPointerDown(PointerEventData _)
    {
        SetScale(pressScale);
        SetColor(pressColor);
    }

    public void OnPointerUp(PointerEventData _)
    {
        SetScale(_isHovered ? hoverScale : 1f);
        SetColor(_isHovered ? hoverColor : normalColor);
    }

    // ── Selection events (keyboard / gamepad navigation) ─────────────────────

    public void OnSelect(BaseEventData _)
    {
        _isHovered = true;
        SetScale(hoverScale);
        SetColor(hoverColor);
    }

    public void OnDeselect(BaseEventData _)
    {
        _isHovered = false;
        SetScale(1f);
        SetColor(normalColor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetScale(float targetMultiplier)
    {
        if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale * targetMultiplier));
    }

    private void SetColor(Color target)
    {
        if (targetGraphic != null)
            targetGraphic.color = target;
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        transform.localScale = target;
    }

    void OnDisable()
    {
        // Reset visuals cleanly when the button is hidden.
        if (_scaleCoroutine != null) { StopCoroutine(_scaleCoroutine); _scaleCoroutine = null; }
        transform.localScale = _originalScale;
        if (targetGraphic != null) targetGraphic.color = normalColor;
        _isHovered = false;
    }
}
