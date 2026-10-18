using UnityEngine;
using UnityEngine.UI;

namespace ImprovedMagicShield
{
    // An additive rim outside the native health rectangle. Never change its fill,
    // color, layout or controllers; inherit the same canvas and movement instead.
    internal sealed class ShieldHealthOutline
    {
        private RectTransform root;
        private RectTransform progress;
        private RectTransform rim, glow;
        private RectMask2D clip;
        private bool ownsSprites;
        private int spriteKey;
        private float spriteHeight;

        internal void Render(RectTransform health, float fraction)
        {
            if (health == null || !health.gameObject.activeInHierarchy
                || health.rect.width <= 0f || health.rect.height <= 0f
                || !(fraction > 0f) || float.IsInfinity(fraction)) { Hide(); return; }
            if (root != null && root.parent != health) Dispose();
            if (ownsSprites && spriteHeight != health.rect.height) Dispose();
            if (root == null)
            {
                // EnemyHud may already have destroyed our previous native parent.
                if (ownsSprites) Dispose();
                root = (RectTransform)new GameObject("ImprovedMagicShield.HealthOutline", typeof(RectTransform)).transform;
                root.SetParent(health, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = new Vector2(-5f, -5f);
                root.offsetMax = new Vector2(5f, 5f);
                progress = (RectTransform)new GameObject("Progress", typeof(RectTransform)).transform;
                progress.SetParent(root, false);
                progress.anchorMin = Vector2.zero;
                progress.anchorMax = Vector2.one;
                progress.offsetMin = progress.offsetMax = Vector2.zero;
                clip = progress.gameObject.AddComponent<RectMask2D>();
                spriteHeight = health.rect.height;
                spriteKey = ShieldOutlineSprites.Acquire(spriteHeight, out Sprite rimSprite, out Sprite glowSprite);
                ownsSprites = true;
                glow = Layer("Glow", glowSprite, new Color(0.65f, 0.25f, 1f, 0.20f));
                rim = Layer("Rim", rimSprite, new Color(0.75f, 0.40f, 1f, 0.85f));
            }
            float value = Mathf.Min(1f, fraction);
            progress.anchorMax = new Vector2(value, 1f);
            clip.softness = value < 1f ? new Vector2Int(2, 0) : Vector2Int.zero;
            // Clip a fixed full-width frame. Never close a shorter frame with a
            // vertical end cap through the middle of the native health fill.
            glow.sizeDelta = new Vector2(health.rect.width + 10f, health.rect.height + 10f);
            rim.sizeDelta = new Vector2(health.rect.width + 6f, health.rect.height + 6f);
            rim.anchoredPosition = new Vector2(2f, 0f);
            Canvas canvas = health.gameObject.GetComponentInParent<Canvas>();
            float referencePixels = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            glow.GetComponent<Image>().pixelsPerUnitMultiplier = referencePixels * 64f / (100f * glow.sizeDelta.y);
            rim.GetComponent<Image>().pixelsPerUnitMultiplier = referencePixels * 64f / (100f * rim.sizeDelta.y);
            root.gameObject.SetActive(true);
        }

        private RectTransform Layer(string name, Sprite sprite, Color color)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(progress, false);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        internal void Hide() { if (root != null) root.gameObject.SetActive(false); }

        internal void Dispose()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
            if (ownsSprites) ShieldOutlineSprites.Release(spriteKey);
            ownsSprites = false;
            root = null;
            progress = rim = glow = null;
            clip = null;
        }
    }
}
