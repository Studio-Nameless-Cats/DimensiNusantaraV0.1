using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main Menu controller. Handles New Game / Continue / Quit buttons and
/// greys out Continue when no save file exists.
///
/// ── Unity setup ────────────────────────────────────────────────────────────
///   1. Create a new scene "MainMenu" and add it to Build Settings (index 0).
///   2. Build this Canvas hierarchy:
///        Canvas (Screen Space - Overlay)
///          └ Background (ScrollingBackground layers go here)
///          └ MenuPanel (empty RectTransform, centred)
///               └ Title (TextMeshProUGUI or Text)
///               └ ContinueButton (Button)
///               └ NewGameButton (Button)
///               └ QuitButton (Button)
///          └ VersionText (Text/TMP anchored bottom-left or bottom-right)
///   3. Add THIS component to any persistent GameObject (e.g. "MainMenuUI").
///   4. Assign all Inspector references.
///   5. Set gameSceneName to whatever your overworld/gameplay scene is named.
///
/// ── Continue button greyed out ──────────────────────────────────────────────
///   If SaveSystem.HasSave() returns false, ContinueButton is set non-interactable
///   and its CanvasGroup alpha drops to disabledAlpha so it reads as locked.
///   Add a CanvasGroup component to ContinueButton for the alpha to work.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Continue disabled appearance")]
    [Tooltip("Alpha applied to the Continue button when no save exists.")]
    [SerializeField] private float disabledAlpha = 0.4f;

    [Header("Scene names")]
    [Tooltip("Name of the scene to load on New Game.")]
    [SerializeField] private string gameSceneName = "Overworld";

    [Header("Version")]
    [SerializeField] private Text versionText;
    [Tooltip("Shown in the version label, e.g. 'v0.1'. Leave blank to hide the label.")]
    [SerializeField] private string versionString = "v0.1";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        // Version label.
        if (versionText != null)
        {
            versionText.text = string.IsNullOrEmpty(versionString) ? "" : versionString;
            versionText.gameObject.SetActive(!string.IsNullOrEmpty(versionString));
        }

        // Wire up buttons.
        if (newGameButton  != null) newGameButton.onClick.AddListener(OnNewGame);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (quitButton     != null) quitButton.onClick.AddListener(OnQuit);

        // Grey out Continue if there's no save.
        RefreshContinueButton();
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnNewGame()
    {
        // TODO: if a save exists, prompt "overwrite?" before loading.
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinue()
    {
        if (!SaveSystem.HasSave()) return; // safety guard

        // TODO: load save data before transitioning.
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;

        bool hasSave = SaveSystem.HasSave();
        continueButton.interactable = hasSave;

        // Dim via CanvasGroup (add one to the button if not present).
        CanvasGroup cg = continueButton.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = hasSave ? 1f : disabledAlpha;
    }
}
