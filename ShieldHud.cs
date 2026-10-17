using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImprovedMagicShield
{
    internal sealed class ShieldHud
    {
        private Hud owner;
        private RectTransform root;
        private RectTransform fill;
        private TMP_Text text;
        private const float Width = 240f;
        private const float Height = 28f;

        internal void Tick(bool enabled)
        {
            Hud hud = Hud.instance;
            Player player = Player.m_localPlayer;
            if (!enabled || hud == null || player == null || player.IsDead() || player.IsTeleporting()
                || !hud.IsVisible() || player.InCutscene())
            {
                Hide();
                return;
            }
            SE_Shield shield = ShieldRefresh.FindShield(player);
            if (!HudPolicy.TryRead(shield, out float remaining, out float capacity)) { Hide(); return; }
            if (owner != hud || root == null)
            {
                Destroy();
                if (hud.m_rootObject == null || hud.m_staminaBar2Root == null || hud.m_staminaText == null) return;
                Create(hud);
            }
            root.gameObject.SetActive(true);
            Position(hud);
            fill.anchorMax = new Vector2(HudPolicy.Fraction(remaining, capacity), 1f);
            text.text = HudPolicy.Text(remaining, capacity);
        }

        private void Create(Hud hud)
        {
            owner = hud;
            root = Rect("ImprovedMagicShield.ResourceBar", hud.m_rootObject.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(Width, Height);
            Image border = root.gameObject.AddComponent<Image>();
            border.color = new Color(0.08f, 0.04f, 0.11f, 0.95f);
            border.raycastTarget = false;
            RectTransform inside = Rect("Background", root);
            Stretch(inside, 3f);
            Image background = inside.gameObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.13f, 0.26f, 0.95f);
            background.raycastTarget = false;
            fill = Rect("Protection", inside);
            Stretch(fill, 0f);
            Image protection = fill.gameObject.AddComponent<Image>();
            protection.color = new Color(0.58f, 0.30f, 0.82f, 1f);
            protection.raycastTarget = false;
            // Reuse the game's actual font/material and inherit its HUD canvas scaling.
            text = Object.Instantiate(hud.m_staminaText, root);
            text.name = "ProtectionText";
            text.gameObject.SetActive(true);
            Stretch(text.rectTransform, 0f);
            text.rectTransform.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false;
            text.fontSize = 18f;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void Position(Hud hud)
        {
            Transform parent = root.parent;
            Bounds stamina = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_staminaBar2Root);
            float top = stamina.max.y;
            // Reserve space for all three native resources, even while a bar is hidden
            // or animating. This avoids jumping into Eitr/adrenaline when they appear.
            if (hud.m_eitrBarRoot != null)
                top = Mathf.Max(top, RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_eitrBarRoot).max.y);
            if (hud.m_adrenalineBarRoot != null)
                top = Mathf.Max(top, RectTransformUtility.CalculateRelativeRectTransformBounds(parent, hud.m_adrenalineBarRoot).max.y);
            root.localPosition = new Vector3(stamina.center.x, top + 12f, 0f);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void Hide() { if (root != null) root.gameObject.SetActive(false); }
        internal void Destroy()
        {
            if (root != null) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
            root = null; fill = null; text = null; owner = null;
        }
    }
}
