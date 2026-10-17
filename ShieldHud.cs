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
        private const float BorderWidth = 16f;
        private const float Gap = 6f;

        internal void Tick(bool enabled)
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
            root.gameObject.SetActive(true);
            string display = HudPolicy.Text(remaining, capacity);
            float panelWidth = Mathf.Max(hud.m_staminaBar2Root.rect.width, hud.m_eitrBarRoot.rect.width);
            if (hud.m_adrenalineBarRoot != null) panelWidth = Mathf.Max(panelWidth, hud.m_adrenalineBarRoot.rect.width);
            float width = Mathf.Max(panelWidth - BorderWidth, text.GetPreferredValues(display).x + 8f);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + BorderWidth);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
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
            Position(hud);
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
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(hud.m_eitrBarRoot.rect.width, hud.m_eitrBarRoot.rect.height);
            visual = (RectTransform)Object.Instantiate(source.gameObject, root, false).transform;
            fast = MapClone(hud.m_eitrBarFast, source, visual);
            slow = MapClone(hud.m_eitrBarSlow, source, visual);
            text = MapClone(hud.m_eitrText, source, visual);
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

        private void Position(Hud hud)
        {
            Transform parent = root.parent;
            Bounds stamina = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_staminaBar2Root);
            float top = stamina.max.y;
            if (hud.m_eitrBarRoot != null) top = Mathf.Max(top, RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_eitrBarRoot).max.y);
            if (hud.m_adrenalineBarRoot != null) top = Mathf.Max(top, RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_adrenalineBarRoot).max.y);
            Bounds artwork = RectTransformUtility.CalculateRelativeRectTransformBounds(root, visual);
            root.localPosition = new Vector3(stamina.center.x - artwork.center.x, top + Gap - artwork.min.y, 0f);
        }

        private void Hide() { if (root != null) root.gameObject.SetActive(false); }
        internal void Destroy()
        {
            if (root != null) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
            root = null; visual = null; fast = null; slow = null; text = null; owner = null;
        }
    }
}
