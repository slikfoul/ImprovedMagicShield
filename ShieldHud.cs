using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImprovedMagicShield
{
    internal sealed class ShieldHud
    {
        private Hud owner;
        private Hud failedHud;
        private RectTransform root;
        private RectTransform visual;
        private GuiBar fast;
        private GuiBar slow;
        private TMP_Text text;
        private CanvasGroup visibility;
        private SE_Shield observedShield;
        private float previousAge, previousProtection, lastActivityAge;
        private static Player threatenedPlayer;
        private static uint threatVersion;
        private uint previousThreat;
        private const float BorderWidth = 16f;
        private const float RowHeight = 192f;

        internal static void ObserveThreat(Player player, bool alerted)
        {
            if (!alerted || player != Player.m_localPlayer) return;
            threatenedPlayer = player;
            unchecked { threatVersion++; }
        }

        internal void Tick(bool enabled, int protectionPerSegment = 50)
        {
            Hud hud = Hud.instance;
            Player player = Player.m_localPlayer;
            if (!enabled || hud == null || player == null || player.IsDead() || player.IsTeleporting()
                || !hud.IsVisible() || player.InCutscene()) { Hide(); return; }
            SE_Shield shield = ShieldRefresh.FindShield(player);
            if (!HudPolicy.TryRead(shield, out float remaining, out float capacity)) { Hide(); return; }
            if (owner != hud || root == null)
            {
                Destroy();
                if (failedHud == hud || hud.m_rootObject == null || hud.m_staminaBar2Root == null
                    || hud.m_eitrBarRoot == null || hud.m_eitrBarFast == null || hud.m_eitrBarSlow == null || hud.m_eitrText == null) return;
                try { Create(hud); }
                catch (Exception error)
                {
                    Destroy();
                    failedHud = hud;
                    Debug.LogWarning("Improved Magic Shield: native resource HUD could not be cloned. Shield mechanics remain active. " + error);
                    return;
                }
            }
            visibility.alpha = UpdateVisibility(player, shield, remaining);
            root.gameObject.SetActive(visibility.alpha > 0f);
            string display = HudPolicy.Text(remaining, capacity);
            float width = HudPolicy.Width(capacity, protectionPerSegment);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + BorderWidth);
            // The caption may extend beyond a short frame. Its preferred size must
            // never clamp the protection scale or shrink the native font.
            float captionWidth = text.GetPreferredValues(HudPolicy.Text(capacity, capacity)).x + 8f;
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(width, captionWidth));
            text.text = display;
            float fraction = HudPolicy.Fraction(remaining, capacity);
            fast.SetMaxValue(1f);
            slow.SetMaxValue(1f);
            // First SetValue initializes GuiBar's cached width; SetWidth must follow it.
            fast.SetValue(fraction);
            slow.SetValue(fraction);
            fast.SetWidth(width);
            slow.SetWidth(width);
            fast.SetColor(new Color(0.68f, 0.35f, 0.90f, 1f));
        }

        private float UpdateVisibility(Player player, SE_Shield shield, float remaining)
        {
            float age = shield.GetDuration();
            uint threat = threatenedPlayer == player ? threatVersion : 0;
            // The same effect instance survives accepted recasts; its timer resets.
            // A rejected weaker cast changes neither timer nor remaining protection.
            if (!ReferenceEquals(observedShield, shield) || age < previousAge
                || remaining < previousProtection || threat != previousThreat)
                lastActivityAge = age;
            observedShield = shield;
            previousAge = age;
            previousProtection = remaining;
            previousThreat = threat;
            return HudPolicy.Alpha(age - lastActivityAge);
        }

        private void Create(Hud hud)
        {
            // In the installed HUD this resolves to eitrpanel/Stamina: only the
            // resource artwork, two GuiBars and TMP label, below the panel Animator.
            Transform source = hud.m_eitrBarFast.transform;
            while (source != null && (!hud.m_eitrBarSlow.transform.IsChildOf(source) || !hud.m_eitrText.transform.IsChildOf(source))) source = source.parent;
            if (source == null || !source.IsChildOf(hud.m_eitrBarRoot)) throw new InvalidOperationException("Eitr resource visuals no longer share an expected subtree.");
            var container = new GameObject("ImprovedMagicShield.ResourceBar", typeof(RectTransform));
            container.SetActive(false);
            root = (RectTransform)container.transform;
            root.SetParent(hud.m_rootObject.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, RowHeight);
            root.sizeDelta = new Vector2(hud.m_eitrBarRoot.rect.width, hud.m_eitrBarRoot.rect.height);
            visibility = container.AddComponent<CanvasGroup>();
            visibility.interactable = false;
            visibility.blocksRaycasts = false;
            visual = (RectTransform)Object.Instantiate(source.gameObject, root, false).transform;
            // Do not inherit the Eitr child's current animated position/scale.
            // The shield occupies its own fixed row in the native HUD canvas.
            visual.anchoredPosition = Vector2.zero;
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            fast = MapClone(hud.m_eitrBarFast, source, visual);
            slow = MapClone(hud.m_eitrBarSlow, source, visual);
            text = MapClone(hud.m_eitrText, source, visual);
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = new Vector2(0.5f, text.rectTransform.anchorMin.y);
            text.rectTransform.anchorMax = new Vector2(0.5f, text.rectTransform.anchorMax.y);
            text.rectTransform.pivot = new Vector2(0.5f, text.rectTransform.pivot.y);
            text.rectTransform.anchoredPosition = new Vector2(0f, text.rectTransform.anchoredPosition.y);
            // Native GuiBar updates only its supplied value; it never reads Eitr.
            // Suppress any foreign controller added to this visual subtree by a HUD mod.
            foreach (Behaviour behaviour in visual.GetComponentsInChildren<Behaviour>(true))
                if (!(behaviour is Graphic) && !(behaviour is GuiBar)) behaviour.enabled = false;
            foreach (Graphic graphic in visual.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            visual.gameObject.SetActive(true); // The native panel's Animator may have hidden its visual child.
            fast.gameObject.SetActive(true);
            slow.gameObject.SetActive(true);
            text.gameObject.SetActive(true);
            fast.m_smoothDrain = false;
            fast.m_smoothFill = false;
            owner = hud;
        }

        private static T MapClone<T>(T original, Transform source, Transform clone) where T : Component
        {
            var path = new Stack<int>();
            Transform node = original.transform;
            while (node != source)
            {
                if (node == null) throw new InvalidOperationException("Native HUD reference escaped its visual subtree.");
                path.Push(node.GetSiblingIndex());
                node = node.parent;
            }
            while (path.Count > 0) clone = clone.GetChild(path.Pop());
            T component = clone.GetComponent<T>();
            if (component == null) throw new InvalidOperationException("A cloned native resource component is missing.");
            return component;
        }

        private void Hide() { if (root != null) root.gameObject.SetActive(false); }
        internal void Destroy()
        {
            if (root != null) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
            root = null; visual = null; fast = null; slow = null; text = null; visibility = null; owner = null;
            observedShield = null;
            previousAge = previousProtection = lastActivityAge = 0f;
            previousThreat = threatVersion = 0;
            threatenedPlayer = null;
        }
    }
}
