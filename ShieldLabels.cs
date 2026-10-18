using System.Collections.Generic;
using UnityEngine;

namespace ImprovedMagicShield
{
    internal sealed class ShieldLabels
    {
        private static readonly int protectionKey = "Slikfoul.ImprovedMagicShield.Protection".GetStableHashCode();
        private static readonly int sampleKey = "Slikfoul.ImprovedMagicShield.SampleTime".GetStableHashCode();
        private static readonly int publisherKey = "Slikfoul.ImprovedMagicShield.Publisher".GetStableHashCode();
        private static readonly int activityKey = "Slikfoul.ImprovedMagicShield.ActivityTime".GetStableHashCode();
        private static readonly int capacityKey = "Slikfoul.ImprovedMagicShield.Capacity".GetStableHashCode();
        private static readonly int capacitySampleKey = "Slikfoul.ImprovedMagicShield.CapacitySampleTime".GetStableHashCode();
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
            internal readonly ShieldLabelAnchor Anchor = new ShieldLabelAnchor();
            internal readonly ShieldHealthOutline Outline = new ShieldHealthOutline();
            internal void Dispose() { Anchor.Dispose(); Outline.Dispose(); }
            internal void Hide() { Anchor.Hide(); Outline.Hide(); }
        }
        private struct ScreenLabel
        {
            internal Label Label;
            internal Vector3 Position;
            internal float Gap;
            internal float UiScale;
            internal bool AboveName;
        }

        internal void Tick(bool enabled, bool show, bool showOutline = false)
        {
            if (!enabled || ZNet.instance == null)
            {
                Clear();
                return;
            }
            if (!show && !showOutline) { ReleaseLabels(); ClearFrame(); }
            if (!showOutline) foreach (Label label in labels) label.Outline.Hide();
            if (!show) foreach (Label label in labels) label.Anchor.Hide();
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.1f;
            var previous = new Dictionary<Character, Label>();
            foreach (Label label in labels) previous[label.Character] = label;
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
                        float capacity = ShieldRefresh.Capacity(localShield);
                        long activity = (long)((now - ShieldActivity.QuietTime(character, localShield, remaining)) * 1000.0);
                        // Keep supplying authoritative values when only local labels are hidden.
                        if (!published.Contains(view) || zdo.GetFloat(protectionKey, 0f) != remaining
                            || zdo.GetFloat(capacityKey, 0f) != capacity
                            || now - zdo.GetLong(sampleKey, 0L) / 1000.0 >= 0.5 || zdo.GetLong(publisherKey, 0L) != zdo.GetOwner()
                            || System.Math.Abs(zdo.GetLong(activityKey, -1L) - activity) >= 50L)
                        {
                            zdo.Set(protectionKey, remaining);
                            zdo.Set(capacityKey, capacity);
                            zdo.Set(sampleKey, (long)(now * 1000.0));
                            zdo.Set(capacitySampleKey, (long)(now * 1000.0));
                            zdo.Set(publisherKey, zdo.GetOwner());
                            zdo.Set(activityKey, activity);
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
                if ((show || showOutline) && remaining > 0f && character != Player.m_localPlayer)
                {
                    if (!previous.TryGetValue(character, out Label label)) label = new Label { Character = character };
                    labels.Add(label);
                    previous.Remove(character);
                }
            }
            foreach (Label removed in previous.Values) removed.Dispose();
            published.RemoveWhere(v => v == null || !v.IsValid() || !v.IsOwner());
        }

        internal void Clear()
        {
            ReleaseLabels();
            ClearFrame();
            foreach (ZNetView view in published)
                if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(protectionKey, 0f);
            published.Clear();
            nextTick = 0f;
            ShieldActivity.Clear();
        }

        private void ClearFrame()
        {
            frameLabels.Clear();
            preparedFrame = -1;
            preparedCamera = null;
        }

        private void ReleaseLabels()
        {
            foreach (Label label in labels) label.Dispose();
            labels.Clear();
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

        private static bool TryDisplay(Character character, out float remaining, out float alpha)
        {
            alpha = 0f;
            if (!TryProtection(character, out remaining)) return false;
            ZNetView view = character.GetComponent<ZNetView>();
            if (view.IsOwner())
                alpha = HudPolicy.Alpha(ShieldActivity.QuietTime(character, ShieldRefresh.FindShield(character), remaining));
            else
                alpha = DisplayPolicy.SnapshotAlpha(view.GetZDO().GetLong(activityKey, -1L) / 1000.0,
                    ZNet.instance.GetTimeSeconds());
            return true;
        }

        private static float ProtectionFraction(Character character, float remaining)
        {
            ZNetView view = character.GetComponent<ZNetView>();
            float capacity;
            if (view.IsOwner())
            {
                SE_Shield shield = ShieldRefresh.FindShield(character);
                capacity = shield == null ? 0f : ShieldRefresh.Capacity(shield);
            }
            else
            {
                ZDO zdo = view.GetZDO();
                // An older owner can refresh the original snapshot fields while
                // leaving a previous owner's capacity behind. Require one sample.
                if (zdo.GetLong(capacitySampleKey, -1L) != zdo.GetLong(sampleKey, 0L)) return 0f;
                capacity = zdo.GetFloat(capacityKey, 0f);
            }
            return DisplayPolicy.ProtectionFraction(remaining, capacity);
        }

        // Called by the main camera immediately before culling, after camera/character LateUpdate.
        // The 10 Hz discovery/publication tick never samples world or screen coordinates.
        internal void PrepareFrame(Camera camera, bool show, int scale = 100, int verticalOffset = 0, bool showOutline = false)
        {
            if (camera == null || camera != Utils.GetMainCamera()) return;
            ClearFrame();
            if ((!show && !showOutline) || !CanShow())
            {
                foreach (Label label in labels) label.Hide();
                return;
            }
            preparedFrame = Time.frameCount;
            preparedCamera = camera;
            foreach (Label label in labels)
            {
                if (!TryProtection(label.Character, out float remaining)) { label.Hide(); continue; }
                // GetTopPoint uses TransformPoint(capsule.center), not physics collider.bounds.
                // Follow the rendered root, without animated head motion or added interpolation.
                Vector3 position = label.Character.GetTopPoint() + Vector3.up * 0.3f;
                if ((position - camera.transform.position).sqrMagnitude > 60f * 60f) { label.Hide(); continue; }
                Vector3 screen = camera.WorldToScreenPointScaled(position);
                if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) { label.Hide(); continue; }
                if (showOutline) label.Outline.Render(label.Anchor.VisibleHealth(label.Character), ProtectionFraction(label.Character, remaining));
                else label.Outline.Hide();
                if (!show || !TryDisplay(label.Character, out remaining, out float alpha) || alpha <= 0f) { label.Anchor.Hide(); continue; }
                float gap = 8f;
                bool aboveName = label.Character is Player
                    || label.Character.gameObject.name.StartsWith("Skeleton_Friendly", System.StringComparison.Ordinal);
                if (label.Anchor.TryRender(label.Character, aboveName, remaining, scale, verticalOffset))
                {
                    label.Anchor.SetAlpha(alpha);
                    continue;
                }
                if (label.Anchor.HasNativeLabel) continue;
                Vector2 edge;
                float uiScale = 1f;
                bool nativeAnchor = aboveName
                    ? label.Anchor.TryUpperNameEdge(label.Character, out edge, out uiScale)
                    : label.Anchor.TryLowerEdge(label.Character, out edge, out uiScale);
                if (nativeAnchor)
                {
                    // This is the actual current native name/health header position. Its
                    // selected edge is authoritative even if its camera update differs from ours.
                    screen.x = edge.x; screen.y = edge.y;
                    gap = 4f * uiScale;
                }
                frameLabels.Add(new ScreenLabel { Label = label, Position = screen, Gap = gap, UiScale = uiScale, AboveName = aboveName });
            }
        }

        internal void Draw(int scale, int verticalOffset = 0)
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
                    if (!TryDisplay(item.Label.Character, out float remaining, out float alpha) || alpha <= 0f) continue;
                    Vector3 screen = item.Position;
                    string text = DisplayPolicy.WholeProtection(remaining).ToString();
                    float width = style.CalcSize(new GUIContent(text)).x + 12f;
                    float height = style.fontSize * 2f;
                    // Player/skeleton labels grow upward away from the name; other labels grow
                    // downward away from the health block. Positive manual offset means up.
                    float y = Screen.height - screen.y + (item.AboveName ? -item.Gap - height : item.Gap);
                    y -= verticalOffset * item.UiScale;
                    Rect rect = new Rect(screen.x - width / 2f, y, width, height);
                    style.normal.textColor = Color.black;
                    GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
                    GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, width, height), text, style);
                    style.normal.textColor = new Color(0.85f, 0.65f, 1f);
                    GUI.Label(rect, text, style);
                }
            }
            finally { GUI.color = previous; }
        }
    }
}
