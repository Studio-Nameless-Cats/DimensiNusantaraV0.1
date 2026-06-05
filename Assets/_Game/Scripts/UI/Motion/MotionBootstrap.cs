using UnityEngine;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // Inits DOTween once at boot with a sane pool so we're not allocating tweens
    // every frame. Runs itself before the first scene loads - no GameObject, no
    // setup, nothing to drag into a scene. Just having this file in the project
    // is enough.
    public static class MotionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // recycleAllByDefault = reuse dead tweens instead of garbage collecting them.
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly)
                   .SetCapacity(tweenersCapacity: 200, sequencesCapacity: 50);
        }
    }
}
