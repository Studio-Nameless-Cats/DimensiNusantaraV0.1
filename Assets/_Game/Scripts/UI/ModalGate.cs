using UnityEngine;

namespace Nusantara.UI
{
    // Tiny shared counter so popups layered ON TOP of the pause menu (equip picker,
    // future confirm boxes, etc.) can tell the menu "Escape is mine right now".
    //
    // How it works: a modal calls Opened() when it shows and Closed() when it hides.
    // GameMenu checks BlocksEscape before reacting to Esc - if any modal is up, the
    // menu leaves Esc alone and the modal handles it itself.
    //
    // The _lastCloseFrame bit covers a same-frame race: when a modal closes ITSELF
    // on Esc, the menu's Update might run later that same frame, see zero open
    // modals, and eat the very same key press to close the whole menu. So a close
    // also blocks Esc for the rest of that frame.
    public static class ModalGate
    {
        static int _count;
        static int _lastCloseFrame = -1;

        public static void Opened()
        {
            _count++;
        }

        public static void Closed()
        {
            _count = Mathf.Max(0, _count - 1);
            _lastCloseFrame = Time.frameCount;
        }

        // True while any modal is open, or on the exact frame one closed.
        public static bool BlocksEscape => _count > 0 || _lastCloseFrame == Time.frameCount;

        // Scene reloads can leave a stale count if a modal got destroyed while open.
        // GameMenu pokes this when it opens fresh, just to be safe.
        public static void Reset()
        {
            _count = 0;
            _lastCloseFrame = -1;
        }
    }
}
