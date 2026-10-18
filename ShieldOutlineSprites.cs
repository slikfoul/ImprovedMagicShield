using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImprovedMagicShield
{
    // Nine-slice an outer circle into a capsule, with a square inner opening
    // matching the native health rectangle. Share textures at each bar height.
    internal static class ShieldOutlineSprites
    {
        private const int Size = 64;
        private sealed class Pair { internal Sprite Rim, Glow; internal int Users; }
        private static readonly Dictionary<int, Pair> cache = new Dictionary<int, Pair>();
        private static int users;

        internal static int Acquire(float height, out Sprite rimSprite, out Sprite glowSprite)
        {
            int key = Mathf.RoundToInt(height * 100f);
            if (!cache.TryGetValue(key, out Pair pair))
            {
                pair = new Pair { Rim = Create("Rim", height, 3f, false), Glow = Create("Glow", height, 5f, true) };
                cache.Add(key, pair);
            }
            pair.Users++; users++;
            rimSprite = pair.Rim; glowSprite = pair.Glow;
            return key;
        }

        private static Sprite Create(string name, float height, float margin, bool soft)
        {
            var pixels = new Color[Size * Size];
            float opening = height / (height + 2f * margin) * Size * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float px = x + 0.5f - Size * 0.5f, py = y + 0.5f - Size * 0.5f;
                float outer = (float)Math.Sqrt(px * px + py * py) - Size * 0.5f;
                float inner = Math.Max(Math.Abs(px), Math.Abs(py)) - opening;
                float alpha = Clamp(0.5f - outer) * (1f - Clamp(0.5f - inner));
                if (soft) alpha *= Clamp((0.5f - outer) / 3f) * Clamp((inner + 0.5f) / 3f);
                pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "ImprovedMagicShield." + name;
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels); texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(32f, 32f, 32f, 32f));
            sprite.name = texture.name;
            return sprite;
        }

        private static float Clamp(float value) { return Math.Max(0f, Math.Min(1f, value)); }

        internal static void Release(int key)
        {
            if (!cache.TryGetValue(key, out Pair pair)) return;
            users--;
            if (--pair.Users != 0) return;
            UnityEngine.Object.Destroy(pair.Rim.texture); UnityEngine.Object.Destroy(pair.Glow.texture);
            UnityEngine.Object.Destroy(pair.Rim); UnityEngine.Object.Destroy(pair.Glow);
            cache.Remove(key);
        }
    }
}