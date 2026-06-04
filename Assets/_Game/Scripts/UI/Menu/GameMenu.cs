using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nusantara.UI
{
    /// <summary>
    /// The in-game (Overworld) menu. Opened by a top-left pause-button icon or the
    /// Escape key. Pauses the game while open (via <see cref="GameController.SetPaused"/>)
    /// and lays out:
    ///   • a small SYSTEM-BUTTON column (Resume / Save / Load / Options / Quit), and
    ///   • a TAB BAR (Character / Party / Quest / Inventory) that swaps full-screen panels.
    ///
    /// The tab system is generic: add a <see cref="Tab"/> entry per (button, panel) pair
    /// and this controller handles show/hide + active highlight. Quest/Inventory panels
    /// can be plain GameObjects with a label — no script needed.
    ///
    /// ── Unity setup (lives on the Overworld Canvas) ──────────────────────────
    ///   Canvas
    ///     ├ PauseButton (top-left icon Button)            → assign to pauseButton
    ///     ├ Minimap (already exists, bottom-right)
    ///     └ GameMenuRoot (full-screen, starts INACTIVE)   → assign to menuRoot
    ///          ├ SystemButtons (bottom-left column): Resume/Save/Load/Options/Quit
    ///          ├ TabBar: one Button per tab
    ///          ├ Panels: CharacterPanel / PartyPanel / QuestPanel / InventoryPanel
    ///          ├ SaveSlotPanel (shared save+load picker)
    ///          └ ConfirmDialog (shared yes/no)
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public string     name;
            public Button     button;
            public GameObject panel;
            public GameObject SelectedOverlay;
        }

        [Header("Open / close")]
        [Tooltip("Top-left icon button that opens the menu.")]
        [SerializeField] private Button     pauseButton;
        [Tooltip("Full-screen menu root. Starts inactive.")]
        [SerializeField] private GameObject menuRoot;
        [Tooltip("Allow the Escape key to toggle the menu.")]
        [SerializeField] private bool       escapeToggles = true;

        [Header("System buttons (bottom-left)")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Tabs (Character / Party / Quest / Inventory)")]
        [SerializeField] private List<Tab> tabs = new List<Tab>();
        [Tooltip("Tint applied to the active tab button's targetGraphic.")]
        [SerializeField] private Color activeTabColor   = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color inactiveTabColor = Color.white;

        [Header("Sub-panels")]
        [SerializeField] private SaveSlotPanel saveSlotPanel;
        [SerializeField] private GameObject    optionsPanel;
        [SerializeField] private ConfirmDialog confirmDialog;

        [Header("Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _open;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            if (pauseButton   != null) pauseButton.onClick.AddListener(Open);
            if (resumeButton  != null) resumeButton.onClick.AddListener(Close);
            if (saveButton    != null) saveButton.onClick.AddListener(OpenSave);
            if (loadButton    != null) loadButton.onClick.AddListener(OpenLoad);
            if (optionsButton != null) optionsButton.onClick.AddListener(ToggleOptions);
            if (quitButton    != null) quitButton.onClick.AddListener(QuitToMainMenu);

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture
                if (tabs[i].button != null)
                    tabs[i].button.onClick.AddListener(() => SelectTab(index));
            }

            if (menuRoot != null) menuRoot.SetActive(false);
            _open = false;
        }

        void Update()
        {
            if (!escapeToggles) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_open) Close();
                else       Open();
            }
        }

        // ── Open / close ────────────────────────────────────────────────────────

        public void Open()
        {
            // Only open from FreeRoam (don't pop the menu mid-battle/dialog/cutscene).
            if (GameController.Instance != null && GameController.Instance.State != GameState.FreeRoam)
                return;

            _open = true;
            if (menuRoot != null) menuRoot.SetActive(true);
            GameController.Instance?.SetPaused(true);

            // Default to the first tab; hide transient sub-panels.
            CloseSubPanels();
            if (tabs.Count > 0) SelectTab(0);
        }

        public void Close()
        {
            _open = false;
            CloseSubPanels();
            if (menuRoot != null) menuRoot.SetActive(false);
            GameController.Instance?.SetPaused(false);
        }

        private void CloseSubPanels()
        {
            saveSlotPanel?.Close();
            confirmDialog?.Hide();
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        // ── System buttons ────────────────────────────────────────────────────

        private void OpenSave() => saveSlotPanel?.Open(SaveSlotPanel.Mode.Save);
        private void OpenLoad() => saveSlotPanel?.Open(SaveSlotPanel.Mode.Load);

        private void ToggleOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(!optionsPanel.activeSelf);
        }

        private void QuitToMainMenu()
        {
            Action quit = () =>
            {
                // GameController self-destructs when the MainMenu scene loads and resets
                // time itself, but reset here too in case it isn't in the scene.
                Time.timeScale = 1f;
                SceneManager.LoadScene(mainMenuSceneName);
            };

            if (confirmDialog != null)
                confirmDialog.Show("Kembali ke Menu Utama? Progres yang belum disimpan akan hilang.", quit);
            else
                quit();
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        public void SelectTab(int index)
        {
            // Switching tabs should dismiss any open save/load/options sub-panel.
            CloseSubPanels();

            for (int i = 0; i < tabs.Count; i++)
            {
                bool active = i == index;
                if (tabs[i].panel != null) tabs[i].panel.SetActive(active);

                if (tabs[i].button != null && tabs[i].button.targetGraphic != null)
                    tabs[i].button.targetGraphic.color = active ? activeTabColor : inactiveTabColor;
                    tabs[i].SelectedOverlay?.SetActive(active); // change active tab button's SelectedOverlay active or not
            }
        }
    }
}
