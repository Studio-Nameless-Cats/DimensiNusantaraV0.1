using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    /// <summary>
    /// A simple reusable yes/no confirmation popup. One instance can serve many
    /// callers — each <see cref="Show"/> call swaps the message and rebinds the
    /// Confirm/Cancel callbacks.
    ///
    /// ── Unity setup ──────────────────────────────────────────────────────────
    ///   ConfirmDialogRoot (panel, starts INACTIVE)
    ///     ├ Backdrop (optional full-screen Button → counts as Cancel)
    ///     ├ MessageText (TextMeshProUGUI)
    ///     ├ ConfirmButton (Button)  → "Ya"
    ///     └ CancelButton  (Button)  → "Batal"
    ///   Put this component on ConfirmDialogRoot and assign the refs.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private GameObject      root;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button          confirmButton;
        [SerializeField] private Button          cancelButton;
        [Tooltip("Optional full-screen backdrop button behind the dialog; clicking it cancels.")]
        [SerializeField] private Button          backdropButton;

        private Action _onConfirm;
        private Action _onCancel;

        void Awake()
        {
            if (confirmButton  != null) confirmButton.onClick.AddListener(HandleConfirm);
            if (cancelButton   != null) cancelButton.onClick.AddListener(HandleCancel);
            if (backdropButton != null) backdropButton.onClick.AddListener(HandleCancel);
            Hide();
        }

        /// <summary>Pops the dialog with a message and result callbacks.</summary>
        public void Show(string message, Action onConfirm, Action onCancel = null)
        {
            _onConfirm = onConfirm;
            _onCancel  = onCancel;

            if (messageText != null) messageText.text = message;
            if (root != null) root.SetActive(true);
            else gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
            else gameObject.SetActive(false);
        }

        private void HandleConfirm()
        {
            var cb = _onConfirm;
            _onConfirm = _onCancel = null;
            Hide();
            cb?.Invoke();
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            _onConfirm = _onCancel = null;
            Hide();
            cb?.Invoke();
        }
    }
}
