using System.Collections.Generic;
using UnityEngine;

namespace ImprovedMagicShield
{
    internal sealed class ShieldLabels
    {
        private static readonly int protectionKey = "Slikfoul.ImprovedMagicShield.Protection".GetStableHashCode();
        private static readonly int sampleKey = "Slikfoul.ImprovedMagicShield.SampleTime".GetStableHashCode();
        private static readonly int publisherKey = "Slikfoul.ImprovedMagicShield.Publisher".GetStableHashCode();
        private readonly HashSet<ZNetView> published = new HashSet<ZNetView>();
        private readonly List<Label> labels = new List<Label>();
        private readonly List<ScreenLabel> frameLabels = new List<ScreenLabel>();
        private int preparedFrame = -1;
        private Camera preparedCamera;
        private float nextTick;
        private GUIStyle style;
        private sealed class Label
        {
            internal Character Character;
        }
        private struct ScreenLabel
        {
            internal Label Label;
            internal Vector3 Position;
        }

        internal void Tick(bool enabled, bool show)
        {
            if (!enabled || ZNet.instance == null)
            {
                Clear();
                return;
            }
            if (!show) { labels.Clear(); ClearFrame(); }
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.1f;
            labels.Clear();
            double now = ZNet.instance.GetTimeSeconds();
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character == null) continue;
                ZNetView view = character.GetComponent<ZNetView>();
                if (view == null || !view.IsValid()) continue;
                ZDO zdo = view.GetZDO();
                float remaining;
                SE_Shield localShield = null;
                if (view.IsOwner())
                {
                    localShield = ShieldRefresh.FindShield(character);
                    remaining = localShield == null ? 0f : ShieldRefresh.Remaining(localShield);
                    if (remaining > 0f)
                    {
                        // Keep supplying authoritative values when only local labels are hidden.
                        if (!published.Contains(view) || zdo.GetFloat(protectionKey, 0f) != remaining
                            || now - zdo.GetLong(sampleKey, 0L) / 1000.0 >= 0.5 || zdo.GetLong(publisherKey, 0L) != zdo.GetOwner())
                        {
                            zdo.Set(protectionKey, remaining);
                            zdo.Set(sampleKey, (long)(now * 1000.0));
                            zdo.Set(publisherKey, zdo.GetOwner());
                            published.Add(view);
                        }
                    }
                    else if (published.Remove(view)) zdo.Set(protectionKey, 0f);
                }
                else
                {
                    remaining = zdo.GetFloat(protectionKey, 0f);
                    if (!DisplayPolicy.IsFresh(remaining, zdo.GetLong(sampleKey, 0L) / 1000.0, now,
                        zdo.GetLong(publisherKey, 0L), zdo.GetOwner())) continue;
                }
                // Publishing above also includes our own character. Only its local label is excluded.
                if (show && remaining > 0f && character != Player.m_localPlayer)
                    labels.Add(new Label { Character = character });
            }
            published.RemoveWhere(v => v == null || !v.IsValid() || !v.IsOwner());
        }

        internal void Clear()
        {
            labels.Clear();
            ClearFrame();
            foreach (ZNetView view in published)
                if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(protectionKey, 0f);
            published.Clear();
            nextTick = 0f;
        }

        private void ClearFrame()
        {
            frameLabels.Clear();
            preparedFrame = -1;
            preparedCamera = null;
        }

        private static bool CanShow()
        {
            Player player = Player.m_localPlayer;
            return ZNet.instance != null && player != null && !player.IsDead()
                && !player.IsTeleporting() && !player.InCutscene()
                && (Hud.instance == null || Hud.instance.IsVisible());
        }

        private static bool TryProtection(Character character, out float remaining)
        {
            remaining = 0f;
            if (character == null || character == Player.m_localPlayer || character.IsDead()) return false;
            Player player = character as Player;
            if (player != null && player.IsTeleporting()) return false;
            ZNetView view = character.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return false;
            if (view.IsOwner())
            {
                SE_Shield shield = ShieldRefresh.FindShield(character);
                remaining = shield == null ? 0f : ShieldRefresh.Remaining(shield);
                return remaining > 0f && !float.IsInfinity(remaining);
            }
            ZDO zdo = view.GetZDO();
            remaining = zdo.GetFloat(protectionKey, 0f);
            return DisplayPolicy.IsFresh(remaining, zdo.GetLong(sampleKey, 0L) / 1000.0,
                ZNet.instance.GetTimeSeconds(), zdo.GetLong(publisherKey, 0L), zdo.GetOwner());
        }

        // Called by the main camera immediately before culling, after camera/character LateUpdate.
        // The 10 Hz discovery/publication tick never samples world or screen coordinates.
        internal void PrepareFrame(Camera camera, bool show)
        {
            if (camera == null || camera != Utils.GetMainCamera()) return;
            ClearFrame();
            if (!show || !CanShow()) return;
            preparedFrame = Time.frameCount;
            preparedCamera = camera;
            foreach (Label label in labels)
            {
                if (!TryProtection(label.Character, out _)) continue;
                // GetTopPoint uses TransformPoint(capsule.center), not physics collider.bounds.
                // Follow the rendered root, without animated head motion or added interpolation.
                Vector3 position = label.Character.GetTopPoint() + Vector3.up * 0.3f;
                if ((position - camera.transform.position).sqrMagnitude > 60f * 60f) continue;
                Vector3 screen = camera.WorldToScreenPointScaled(position);
                if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) continue;
                frameLabels.Add(new ScreenLabel { Label = label, Position = screen });
            }
        }

        internal void Draw(int scale)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || !CanShow()
                || preparedFrame != Time.frameCount || preparedCamera == null || preparedCamera != Utils.GetMainCamera()) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(20f * Mathf.Clamp(scale, 50, 300) / 100f);
            Color previous = GUI.color;
            try
            {
                foreach (ScreenLabel item in frameLabels)
                {
                    if (!TryProtection(item.Label.Character, out float remaining)) continue;
                    Vector3 screen = item.Position;
                    string text = "Shield: " + DisplayPolicy.WholeProtection(remaining);
                    float width = style.CalcSize(new GUIContent(text)).x + 12f;
                    float height = style.fontSize * 2f;
                    Rect rect = new Rect(screen.x - width / 2f, Screen.height - screen.y - height / 2f, width, height);
                    style.normal.textColor = Color.black;
                    GUI.color = previous;
                    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, width, height), text, style);
                    style.normal.textColor = new Color(0.85f, 0.65f, 1f);
                    GUI.Label(rect, text, style);
                }
            }
            finally { GUI.color = previous; }
        }
    }
}
