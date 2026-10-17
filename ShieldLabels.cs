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
        private float nextTick;
        private GUIStyle style;
        private sealed class Label
        {
            internal Character Character;
            internal float Protection;
            internal SE_Shield LocalShield;
        }

        internal void Tick(bool enabled, bool show)
        {
            if (!enabled || ZNet.instance == null)
            {
                Clear();
                return;
            }
            if (!show) labels.Clear();
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
                if (show && remaining > 0f) labels.Add(new Label { Character = character, Protection = remaining, LocalShield = localShield });
            }
            published.RemoveWhere(v => v == null || !v.IsValid() || !v.IsOwner());
        }

        internal void Clear()
        {
            labels.Clear();
            foreach (ZNetView view in published)
                if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(protectionKey, 0f);
            published.Clear();
            nextTick = 0f;
        }

        internal void Draw(int scale)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(20f * Mathf.Clamp(scale, 50, 300) / 100f);
            Color previous = GUI.color;
            try
            {
                foreach (Label label in labels)
                {
                    if (label.Character == null || label.Character.IsDead()) continue;
                    Vector3 position = label.Character.GetCenterPoint() + Vector3.up * (label.Character.GetRadius() + 0.5f);
                    if ((position - camera.transform.position).sqrMagnitude > 60f * 60f) continue;
                    Vector3 screen = camera.WorldToScreenPoint(position);
                    if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) continue;
                    float remaining = label.LocalShield == null ? label.Protection : ShieldRefresh.Remaining(label.LocalShield);
                    if (!(remaining > 0f)) continue;
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
