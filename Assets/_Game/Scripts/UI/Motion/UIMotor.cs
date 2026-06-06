using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Nusantara.UI.Motion
{
    // The verbs. This is the ONLY place raw DOTween shows up - everywhere else
    // calls these so a call site reads like a sentence:
    //
    //     myPanel.SkewSlideIn(profile);
    //     myButton.SelectPop(profile, baseScale, baseX);
    //
    // Every number comes from the profile. If we ever swap tween libraries or
    // retune the feel, we touch this file and nowhere else.
    public static class UIMotor
    {
        // Slides an element in along the skew diagonal to wherever it currently
        // sits (we treat its current anchoredPos as "rest"). Optional CanvasGroup
        // fades it in at the same time. Call this while the element is already
        // parked at its rest spot - we snap it back to the offset ourselves.
        public static Tween SkewSlideIn(this RectTransform rt, MotionProfile p, CanvasGroup cg = null, float delay = 0f)
        {
            Vector2 rest = rt.anchoredPosition;
            rt.anchoredPosition = rest + p.skewSlideOffset;
            if (cg != null) cg.alpha = 0f;

            Sequence s = DOTween.Sequence();
            s.Append(rt.DOAnchorPos(rest, p.fastInDuration).SetEase(p.fastInEase, p.fastInOvershoot));
            if (cg != null) s.Join(cg.DOFade(1f, p.fastInDuration).SetEase(Ease.OutQuad));
            s.SetDelay(delay);
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Generic slide-in: same idea as SkewSlideIn but you hand it the offset, so
        // a panel can come from the left, the bottom, wherever. Treats the element's
        // current anchoredPos as "rest" - park it at home before calling this.
        public static Tween SlideIn(this RectTransform rt, MotionProfile p, Vector2 fromOffset, CanvasGroup cg = null, float delay = 0f)
        {
            Vector2 rest = rt.anchoredPosition;
            rt.anchoredPosition = rest + fromOffset;
            if (cg != null) cg.alpha = 0f;

            Sequence s = DOTween.Sequence();
            s.Append(rt.DOAnchorPos(rest, p.fastInDuration).SetEase(p.fastInEase, p.fastInOvershoot));
            if (cg != null) s.Join(cg.DOFade(1f, p.fastInDuration).SetEase(Ease.OutQuad));
            s.SetDelay(delay);
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Generic slide-out: shoves the element off by 'toOffset' from where it
        // rests now and fades it. Mirror of SlideIn for when a panel leaves.
        public static Tween SlideOut(this RectTransform rt, MotionProfile p, Vector2 toOffset, CanvasGroup cg = null, float delay = 0f)
        {
            Vector2 rest = rt.anchoredPosition;
            Sequence s = DOTween.Sequence();
            s.Append(rt.DOAnchorPos(rest + toOffset, p.fastOutDuration).SetEase(p.fastOutEase));
            if (cg != null) s.Join(cg.DOFade(0f, p.fastOutDuration).SetEase(Ease.InQuad));
            s.SetDelay(delay);
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Springs an element up from small to its home scale - good for a panel or
        // an icon that should "pop" into existence instead of sliding. homeScale is
        // whatever its resting localScale is (the caller owns that).
        public static Tween ScalePopIn(this RectTransform rt, MotionProfile p, Vector3 homeScale, CanvasGroup cg = null)
        {
            rt.localScale = homeScale * p.popInStartScale;
            if (cg != null) cg.alpha = 0f;

            Sequence s = DOTween.Sequence();
            s.Join(rt.DOScale(homeScale, p.fastInDuration).SetEase(p.fastInEase, p.fastInOvershoot));
            if (cg != null) s.Join(cg.DOFade(1f, p.fastInDuration).SetEase(Ease.OutQuad));
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Shrinks an element back down and fades it - reverse of ScalePopIn.
        public static Tween ScalePopOut(this RectTransform rt, MotionProfile p, Vector3 homeScale, CanvasGroup cg = null)
        {
            Sequence s = DOTween.Sequence();
            s.Join(rt.DOScale(homeScale * p.popInStartScale, p.fastOutDuration).SetEase(p.fastOutEase));
            if (cg != null) s.Join(cg.DOFade(0f, p.fastOutDuration).SetEase(Ease.InQuad));
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Plain fade up - for stuff that shouldn't move at all, just appear.
        public static Tween FadeIn(this CanvasGroup cg, MotionProfile p, GameObject link = null)
        {
            cg.alpha = 0f;
            return cg.DOFade(1f, p.fastInDuration).SetEase(Ease.OutQuad)
                     .ApplyMenuDefaults(p, link != null ? link : cg.gameObject);
        }

        // Plain fade down - reverse of FadeIn.
        public static Tween FadeOut(this CanvasGroup cg, MotionProfile p, GameObject link = null)
        {
            return cg.DOFade(0f, p.fastOutDuration).SetEase(Ease.InQuad)
                     .ApplyMenuDefaults(p, link != null ? link : cg.gameObject);
        }

        // Same move, but slower and a beat late - this is the shadow trailing the
        // main layer into place. That tiny desync is the whole signature.
        public static Tween ShadowSlideIn(this RectTransform shadow, MotionProfile p, CanvasGroup cg = null)
        {
            Vector2 rest = shadow.anchoredPosition;
            shadow.anchoredPosition = rest + p.skewSlideOffset;
            if (cg != null) cg.alpha = 0f;

            float dur = p.fastInDuration * p.shadowLagDurationMult;
            Sequence s = DOTween.Sequence();
            s.Append(shadow.DOAnchorPos(rest, dur).SetEase(p.fastInEase, p.fastInOvershoot));
            if (cg != null) s.Join(cg.DOFade(1f, dur).SetEase(Ease.OutQuad));
            s.SetDelay(p.shadowLagDelay);
            return s.ApplyMenuDefaults(p, shadow.gameObject);
        }

        // Reverse of SkewSlideIn - shoves the element back off along the diagonal
        // and fades it out. Used when a screen is leaving.
        public static Tween SkewSlideOut(this RectTransform rt, MotionProfile p, CanvasGroup cg = null, float delay = 0f)
        {
            Vector2 rest = rt.anchoredPosition;
            Sequence s = DOTween.Sequence();
            s.Append(rt.DOAnchorPos(rest + p.skewSlideOffset, p.fastOutDuration).SetEase(p.fastOutEase));
            if (cg != null) s.Join(cg.DOFade(0f, p.fastOutDuration).SetEase(Ease.InQuad));
            s.SetDelay(delay);
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Plays SkewSlideIn on a list of items one after another. groups is
        // optional and lines up 1:1 with items for the fades. Returns the whole
        // staggered sequence so the caller can chain off it.
        public static Sequence Cascade(this IReadOnlyList<RectTransform> items, MotionProfile p, IReadOnlyList<CanvasGroup> groups = null)
        {
            Sequence seq = DOTween.Sequence();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                CanvasGroup cg = (groups != null && i < groups.Count) ? groups[i] : null;
                seq.Insert(i * p.cascadeStagger, items[i].SkewSlideIn(p, cg));
            }
            return (Sequence)seq.ApplyMenuDefaults(p, items.Count > 0 && items[0] != null ? items[0].gameObject : null);
        }

        // Staggered scale-pop for a list of items. Use this instead of Cascade when
        // the items live inside a Layout Group (skill cards, command buttons, turn
        // slots): a layout group rewrites anchoredPosition every frame, so a position
        // slide fights it. Scale + fade are NOT touched by layout, so they're safe.
        // Each item's current localScale is treated as its "home". Grabs a CanvasGroup
        // off each item for the fade if one's there; works fine without one (scale only).
        public static Sequence ScaleCascade(this IReadOnlyList<RectTransform> items, MotionProfile p, float startDelay = 0f)
        {
            Sequence seq = DOTween.Sequence();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                CanvasGroup cg = items[i].GetComponent<CanvasGroup>();
                seq.Insert(startDelay + i * p.cascadeStagger, items[i].ScalePopIn(p, items[i].localScale, cg));
            }
            return (Sequence)seq.ApplyMenuDefaults(p, items.Count > 0 && items[0] != null ? items[0].gameObject : null);
        }

        // Same idea but in reverse - last item leaves first. Good for a menu
        // bailing out before a scene load.
        public static Sequence CascadeOut(this IReadOnlyList<RectTransform> items, MotionProfile p, IReadOnlyList<CanvasGroup> groups = null)
        {
            Sequence seq = DOTween.Sequence();
            int n = items.Count;
            for (int i = 0; i < n; i++)
            {
                if (items[i] == null) continue;
                CanvasGroup cg = (groups != null && i < groups.Count) ? groups[i] : null;
                int fromEnd = (n - 1) - i;
                seq.Insert(fromEnd * p.cascadeStagger, items[i].SkewSlideOut(p, cg));
            }
            return (Sequence)seq.ApplyMenuDefaults(p, n > 0 && items[0] != null ? items[0].gameObject : null);
        }

        // Pops a focused row up and shoves it sideways. baseScale / baseX are the
        // row's resting values, owned by whoever calls this (the MotionButton).
        public static Tween SelectPop(this RectTransform rt, MotionProfile p, Vector3 baseScale, float baseX)
        {
            rt.DOKill();
            Sequence s = DOTween.Sequence();
            s.Join(rt.DOScale(baseScale * p.selectPopScale, p.selectPopDuration).SetEase(p.selectPopEase, p.selectPopOvershoot));
            s.Join(rt.DOAnchorPosX(baseX + p.selectPopShiftX, p.selectPopDuration).SetEase(p.selectPopEase, p.selectPopOvershoot));
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Returns a row from its popped state back to rest.
        public static Tween Deselect(this RectTransform rt, MotionProfile p, Vector3 baseScale, float baseX)
        {
            rt.DOKill();
            Sequence s = DOTween.Sequence();
            s.Join(rt.DOScale(baseScale, p.deselectDuration).SetEase(p.deselectEase));
            s.Join(rt.DOAnchorPosX(baseX, p.deselectDuration).SetEase(p.deselectEase));
            return s.ApplyMenuDefaults(p, rt.gameObject);
        }

        // Hard color change - black to red fill, text to ivory, that kind of thing.
        // Pass whatever target color you want; the profile holds the standard ones.
        public static Tween ColorSlam(this Graphic g, Color target, MotionProfile p)
        {
            if (g == null) return null;
            return g.DOColor(target, p.colorSlamDuration).SetEase(p.colorSlamEase).ApplyMenuDefaults(p, g.gameObject);
        }

        // A one-shot punch on the scale, e.g. the dial when it lands or a button
        // on confirm. Snaps back on its own.
        public static Tween Pulse(this RectTransform rt, MotionProfile p)
        {
            return rt.DOPunchScale(Vector3.one * p.pulsePunch, p.pulseDuration, p.pulseVibrato, p.pulseElasticity)
                     .ApplyMenuDefaults(p, rt.gameObject);
        }

        // Shared tail on every menu tween: ignore timeScale (so paused menus still
        // move) and auto-kill when the object dies (so tweens never leak).
        public static T ApplyMenuDefaults<T>(this T t, MotionProfile p, GameObject link) where T : Tween
        {
            if (t == null) return null;
            t.SetUpdate(p != null && p.useUnscaledTime);
            if (link != null) t.SetLink(link);
            return t;
        }
    }
}
