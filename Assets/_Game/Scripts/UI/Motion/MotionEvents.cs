using System;

namespace Nusantara.UI.Motion
{
    // Tiny event bus so motion components don't have to know about audio (or
    // anything else). A button just shouts "move!" and whoever cares - the
    // MotionAudio player, say - listens. Keeps sound swappable without touching
    // motion code, exactly like the plan's audio section wants.
    public static class MotionEvents
    {
        public static event Action Move;       // focus jumped to a new row
        public static event Action Confirm;    // a button was activated
        public static event Action Cancel;     // backed out
        public static event Action MenuEnter;  // the big entrance kicked off

        public static void RaiseMove()      => Move?.Invoke();
        public static void RaiseConfirm()   => Confirm?.Invoke();
        public static void RaiseCancel()    => Cancel?.Invoke();
        public static void RaiseMenuEnter() => MenuEnter?.Invoke();
    }
}
