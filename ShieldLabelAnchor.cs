using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ImprovedMagicShield
{
    // Read the rendered native header, never reposition or resize its UI.
    internal sealed class ShieldLabelAnchor
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo hudsField = typeof(EnemyHud).GetField("m_huds", Fields);
        private static readonly FieldInfo guiField = typeof(EnemyHud).GetNestedType("HudData", BindingFlags.NonPublic)?.GetField("m_gui", Fields);
        private readonly Vector3[] corners = new Vector3[4];
        private GameObject cachedRoot;
        private RectTransform name, health;
        private Canvas canvas;
        private TMP_Text label;
        internal bool HasNativeLabel { get { return label != null; } }

        internal void SetAlpha(float alpha)
        {
            if (label != null) label.color = new Color(0.85f, 0.65f, 1f, alpha);
        }

        internal void Hide()
        {
            if (label != null) label.gameObject.SetActive(false);
        }

        internal void Dispose()
        {
            if (label != null) { label.gameObject.SetActive(false); Object.Destroy(label.gameObject); }
            label = null;
        }

        // Keep the caption in the same canvas and transform hierarchy as the native
        // name. It then shares the header's render timing, scaling and pixel rounding
        // instead of converting its position into a separate IMGUI drawing pass.
        internal bool TryRender(Character character, bool aboveName, float remaining, int scale, int offset)
        {
            if (!TryEdge(character, aboveName, out _, out _)) { Hide(); return false; }
            TMP_Text source = name != null ? name.GetComponent<TMP_Text>() : null;
            if (source == null) { Hide(); return false; }
            if (label == null)
            {
                GameObject clone = Object.Instantiate(source.gameObject, cachedRoot.transform, false);
                clone.name = "ImprovedMagicShield.Protection";
                label = clone.GetComponent<TMP_Text>();
                foreach (Behaviour behaviour in clone.GetComponentsInChildren<Behaviour>(true))
                    if (!(behaviour is Graphic)) behaviour.enabled = false;
                label.enabled = true;
                label.raycastTarget = false;
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.85f, 0.65f, 1f);
            }
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            IncludeLocal(name, ref left, ref right, ref bottom, ref top);
            if (!aboveName) IncludeLocal(health, ref left, ref right, ref bottom, ref top);
            float fontSize = 20f * Mathf.Clamp(scale, 50, 300) / 100f;
            float height = fontSize * 2f;
            label.fontSize = fontSize;
            label.text = DisplayPolicy.WholeProtection(remaining).ToString();
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(Mathf.Max(300f, right - left), height);
            rect.localPosition = new Vector3((left + right) * 0.5f,
                (aboveName ? top + 4f + height * 0.5f : bottom - 4f - height * 0.5f) + offset, 0f);
            label.gameObject.SetActive(true);
            return true;
        }

        private void IncludeLocal(RectTransform rect, ref float left, ref float right, ref float bottom, ref float top)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = cachedRoot.transform.InverseTransformPoint(corner);
                left = Mathf.Min(left, local.x); right = Mathf.Max(right, local.x);
                bottom = Mathf.Min(bottom, local.y); top = Mathf.Max(top, local.y);
            }
        }

        internal bool TryLowerEdge(Character character, out Vector2 edge, out float uiScale)
        {
            return TryEdge(character, false, out edge, out uiScale);
        }

        internal bool TryUpperNameEdge(Character character, out Vector2 edge, out float uiScale)
        {
            return TryEdge(character, true, out edge, out uiScale);
        }

        private bool TryEdge(Character character, bool aboveName, out Vector2 edge, out float uiScale)
        {
            edge = Vector2.zero; uiScale = 1f;
            if (EnemyHud.instance == null || character.IsBoss() || hudsField == null || guiField == null) return false;
            var huds = hudsField.GetValue(EnemyHud.instance) as IDictionary;
            if (huds == null || !huds.Contains(character)) return false;
            var root = guiField.GetValue(huds[character]) as GameObject;
            if (root == null || !root.activeInHierarchy) return false;
            if (root != cachedRoot)
            {
                Dispose();
                cachedRoot = root;
                name = root.transform.Find("Name") as RectTransform;
                health = root.transform.Find("Health") as RectTransform;
                canvas = root.GetComponentInParent<Canvas>();
            }
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            Include(name, uiCamera, ref left, ref right, ref bottom, ref top);
            if (!aboveName) Include(health, uiCamera, ref left, ref right, ref bottom, ref top);
            if (float.IsInfinity(bottom) || float.IsNaN(bottom)) return false;
            edge = new Vector2((left + right) * 0.5f, aboveName ? top : bottom);
            if (canvas != null) uiScale = Mathf.Max(0.5f, canvas.scaleFactor);
            return true;
        }

        private void Include(RectTransform rect, Camera camera, ref float left, ref float right, ref float bottom, ref float top)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x); bottom = Mathf.Min(bottom, screen.y);
                top = Mathf.Max(top, screen.y);
            }
        }
    }
}
