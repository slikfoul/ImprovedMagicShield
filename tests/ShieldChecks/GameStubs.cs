// Managed test doubles only. They do not execute Unity, network replication or the game.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImprovedMagicShield { internal static class Plugin { internal static bool IsEnabled = true; } }
public static class Game { public static int m_worldLevel; }
public class HitData { public short m_itemLevel; public float m_skillLevel; }
public class Character
{
    public static readonly List<Character> All = new List<Character>();
    public ZNetView View = new ZNetView();
    public SEMan Manager = new SEMan();
    public bool Dead;
    public T GetComponent<T>() where T : class { return View as T; }
    public SEMan GetSEMan() { return Manager; }
    public static List<Character> GetAllCharacters() { return All; }
    public Vector3 GetCenterPoint() { return new Vector3(); }
    public float GetRadius() { return 0.5f; }
    public bool IsDead() { return Dead; }
}
public class Player : Character
{
    public static Player m_localPlayer;
    public bool Teleporting,Cutscene;
    public bool IsTeleporting() { return Teleporting; }
    public bool InCutscene() { return Cutscene; }
}
public class Hud
{
    public static Hud instance;
    public bool Visible=true;
    public GameObject m_rootObject=new GameObject("Hud",typeof(RectTransform));
    public RectTransform m_staminaBar2Root=(RectTransform)new GameObject("Stamina",typeof(RectTransform)).transform;
    public RectTransform m_eitrBarRoot=(RectTransform)new GameObject("Eitr",typeof(RectTransform)).transform;
    public RectTransform m_adrenalineBarRoot=(RectTransform)new GameObject("Adrenaline",typeof(RectTransform)).transform;
    public TMPro.TMP_Text m_staminaText=new TMPro.TMP_Text();
    public bool IsVisible() { return Visible; }
}
public class StatusEffect
{
    public float m_ttl = 60f;
    public short m_hitVariant = -1;
    public float Time = 25f;
    public Character Source;
    public int LastItem;
    public float LastSkill;
    public int ResetCalls, LevelCalls;
    public virtual void ResetTime() { Time = 0f; ResetCalls++; }
    public virtual void SetLevel(int itemLevel, float skillLevel) { LastItem = itemLevel; LastSkill = skillLevel; LevelCalls++; }
    public virtual void SetAttacker(Character attacker) { Source = attacker; }
    public int NameHash() { return 123; }
    public float GetDuration() { return Time; }
}
public class SE_Shield : StatusEffect
{
    public float m_absorbDamage = 100f;
    public float m_absorbDamagePerSkillLevel = 4f;
    public float m_absorbDamageWorldLevel = 100f;
    public int m_ttlPerItemLevel = 60;
    private float m_totalAbsorbDamage;
    private float m_damage;
    public SE_Shield(float total, float absorbed) { m_totalAbsorbDamage = total; m_damage = absorbed; }
    public float Total { get { return m_totalAbsorbDamage; } }
    public float Absorbed { get { return m_damage; } }
    public void Hit(float damage) { m_damage += damage; }
    public override void SetLevel(int itemLevel, float skillLevel)
    {
        // Test-double implementation; separately verify the installed game's IL below.
        if (m_ttlPerItemLevel > 0) m_ttl = m_ttlPerItemLevel * itemLevel;
        m_totalAbsorbDamage = m_absorbDamage + m_absorbDamagePerSkillLevel * skillLevel;
        if (Game.m_worldLevel > 0) m_totalAbsorbDamage += m_absorbDamageWorldLevel * Game.m_worldLevel;
        base.SetLevel(itemLevel, skillLevel);
    }
}
public class SEMan
{
    public StatusEffect Effect;
    public StatusEffect GetStatusEffect(int hash) { return Effect; }
    public List<StatusEffect> GetStatusEffects() { return Effect == null ? new List<StatusEffect>() : new List<StatusEffect> { Effect }; }
    public StatusEffect AddStatusEffect(StatusEffect statusEffect, bool resetTime, int itemLevel, float skillLevel, short variant) { return null; }
}
public class ZNet { public static ZNet instance = new ZNet(); public double Now = 100; public double GetTimeSeconds() { return Now; } }
public class ZNetView
{
    public bool Owner = true, Valid = true;
    public ZDO Data = new ZDO();
    public bool IsValid() { return Valid; }
    public bool IsOwner() { return Owner; }
    public ZDO GetZDO() { return Data; }
}
public class ZDO
{
    public long Owner = 10;
    public int Writes;
    private readonly Dictionary<int, object> data = new Dictionary<int, object>();
    public long GetOwner() { return Owner; }
    public void Set(int key, float value) { data[key] = value; Writes++; }
    public void Set(int key, long value) { data[key] = value; Writes++; }
    public float GetFloat(int key, float fallback) { return data.TryGetValue(key, out object value) ? (float)value : fallback; }
    public long GetLong(int key, long fallback) { return data.TryGetValue(key, out object value) ? (long)value : fallback; }
}
public static class HashStub
{
    public static int GetStableHashCode(this string value) { unchecked { int hash = 17; foreach (char c in value) hash = hash * 31 + c; return hash; } }
}
namespace UnityEngine
{
    public static class Time { public static float unscaledTime; }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float a,float b,float c) { x=a;y=b;z=c; }
        public static Vector3 one { get { return new Vector3(1,1,1); } }
        public static Vector3 up { get { return new Vector3 { y = 1 }; } }
        public float sqrMagnitude { get { return x*x+y*y+z*z; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3 { x=a.x+b.x,y=a.y+b.y,z=a.z+b.z }; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3 { x=a.x-b.x,y=a.y-b.y,z=a.z-b.z }; }
        public static Vector3 operator *(Vector3 a, float n) { return new Vector3 { x=a.x*n,y=a.y*n,z=a.z*n }; }
    }
    public class Object
    {
        public static void Destroy(GameObject obj) { obj.Destroyed=true; }
        public static TMPro.TMP_Text Instantiate(TMPro.TMP_Text source,Transform parent)
        {
            var text=new TMPro.TMP_Text(); text.rectTransform.SetParent(parent,false); return text;
        }
    }
    public class GameObject : Object
    {
        public string name; public bool activeSelf=true,Destroyed;
        public Transform transform;
        public readonly Dictionary<Type,object> Components=new Dictionary<Type,object>();
        public GameObject(string n,params Type[] types) { name=n; transform=Array.IndexOf(types,typeof(RectTransform))>=0 ? new RectTransform() : new Transform(); transform.gameObject=this; }
        public T AddComponent<T>() where T : new() { var result=new T(); Components[typeof(T)]=result; return result; }
        public void SetActive(bool active) { activeSelf=active; }
    }
    public class Transform
    {
        public Vector3 position,localPosition,localScale=Vector3.one;
        public GameObject gameObject;
        public Transform parent;
        public readonly List<Transform> Children=new List<Transform>();
        public void SetParent(Transform p,bool world) { parent=p; p.Children.Add(this); }
    }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float a,float b) { x=a;y=b; }
        public static Vector2 zero { get { return new Vector2(); } }
        public static Vector2 one { get { return new Vector2(1,1); } }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin,anchorMax,pivot,sizeDelta,offsetMin,offsetMax;
        public Bounds TestBounds;
    }
    public struct Bounds { public Vector3 center,max; }
    public static class RectTransformUtility
    {
        public static Bounds CalculateRelativeRectTransformBounds(Transform parent,RectTransform rect) { return rect.TestBounds; }
    }
    public class Camera
    {
        public static Camera main = new Camera();
        public Transform transform = new Transform();
        public Vector3 WorldToScreenPoint(Vector3 p) { return new Vector3 { x=100,y=100,z=1 }; }
    }
    public static class Screen { public static int width=1920,height=1080; }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float red,float green,float blue) { r=red;g=green;b=blue;a=1; }
        public Color(float red,float green,float blue,float alpha) { r=red;g=green;b=blue;a=alpha; }
        public static Color black { get { return new Color(0,0,0); } }
        public static Color white { get { return new Color(1,1,1); } }
    }
    public struct Rect { public float x,y,width,height; public Rect(float a,float b,float w,float h) { x=a;y=b;width=w;height=h; } }
    public enum TextAnchor { MiddleCenter }
    public enum FontStyle { Bold }
    public class GUIStyleState { public Color textColor; }
    public class GUIContent { public string text; public GUIContent(string t) { text=t; } }
    public class GUIStyle
    {
        public TextAnchor alignment; public FontStyle fontStyle; public int fontSize;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle(GUIStyle other) {}
        public GUIStyle() {}
        public Vector3 CalcSize(GUIContent c) { return new Vector3 { x=c.text.Length*fontSize }; }
    }
    public class GUISkin { public GUIStyle label=new GUIStyle(); }
    public static class GUI
    {
        public static GUISkin skin=new GUISkin(); public static Color color;
        public static readonly List<string> Texts=new List<string>();
        public static int LastFontSize;
        public static void Label(Rect r,string t,GUIStyle s) { Texts.Add(t); LastFontSize=s.fontSize; }
    }
    public static class Mathf
    {
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static int Clamp(int i,int min,int max) { return Math.Min(max,Math.Max(min,i)); }
        public static float Max(float a,float b) { return Math.Max(a,b); }
    }
}
namespace UnityEngine.UI
{
    public class Image { public UnityEngine.Color color; public bool raycastTarget; }
}
namespace TMPro
{
    public enum TextAlignmentOptions { Center }
    public class TMP_Text : UnityEngine.Object
    {
        public string name,text; public bool enableAutoSizing,raycastTarget; public float fontSize;
        public UnityEngine.Color color; public TextAlignmentOptions alignment;
        public UnityEngine.GameObject gameObject=new UnityEngine.GameObject("Text",typeof(UnityEngine.RectTransform));
        public UnityEngine.RectTransform rectTransform { get { return (UnityEngine.RectTransform)gameObject.transform; } }
    }
}
