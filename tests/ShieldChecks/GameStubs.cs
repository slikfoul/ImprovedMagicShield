// Managed test doubles only. They do not execute Unity, network replication or the game.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public Vector3 RenderedTop = new Vector3(0,2,0), PhysicsCenter;
    public int PhysicsReads;
    public T GetComponent<T>() where T : class { return View as T; }
    public SEMan GetSEMan() { return Manager; }
    public static List<Character> GetAllCharacters() { return All; }
    public Vector3 GetCenterPoint() { PhysicsReads++; return PhysicsCenter; }
    public Vector3 GetTopPoint() { return RenderedTop; }
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
    public TMPro.TMP_Text m_staminaText;
    public TMPro.TMP_Text m_eitrText;
    public GuiBar m_eitrBarFast,m_eitrBarSlow;
    public Hud()
    {
        foreach (var panel in new[] {m_staminaBar2Root,m_eitrBarRoot,m_adrenalineBarRoot})
        {
            panel.SetParent(m_rootObject.transform,false);
            panel.sizeDelta=new Vector2(180,32);
        }
        m_eitrBarRoot.gameObject.AddComponent<Animator>();
        var body=(RectTransform)new GameObject("Stamina",typeof(RectTransform)).transform;
        body.SetParent(m_eitrBarRoot,false); body.anchorMax=Vector2.one; body.sizeDelta=new Vector2(-16,-16);
        body.TestBounds=new Bounds { min=new Vector3(-82,31,0),max=new Vector3(82,47,0),center=new Vector3(0,39,0) };
        body.gameObject.AddComponent<NativeResourceUpdater>();
        var bg=new GameObject("Background",typeof(RectTransform)); bg.transform.SetParent(body,false);
        bg.AddComponent<UnityEngine.UI.Image>().sprite=new Sprite { name="native-frame" };
        m_eitrBarSlow=Bar("eitr_slow",body,true); m_eitrBarFast=Bar("eitr_fast",body,false);
        var label=new GameObject("EitrText",typeof(RectTransform)); label.transform.SetParent(body,false);
        m_eitrText=label.AddComponent<TMPro.TMP_Text>(); m_eitrText.fontSize=12.9f;
        m_eitrText.font=new UnityEngine.Object { name="native-font" }; m_eitrText.fontSharedMaterial=new UnityEngine.Object { name="native-font-material" };
        m_staminaText=m_eitrText;
    }
    private static GuiBar Bar(string name,Transform parent,bool smooth)
    {
        var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
        var bar=obj.AddComponent<GuiBar>(); bar.m_smoothDrain=smooth;
        var fill=new GameObject("bar",typeof(RectTransform)); fill.transform.SetParent(obj.transform,false);
        bar.m_bar=(RectTransform)fill.transform; bar.m_bar.sizeDelta=new Vector2(164,16);
        var image=fill.AddComponent<UnityEngine.UI.Image>(); image.sprite=new Sprite { name="native-resource-fill" }; image.type=UnityEngine.UI.Image.Type.Tiled;
        return bar;
    }
    public bool IsVisible() { return Visible; }
}
public class GuiBar : Behaviour
{
    public RectTransform m_bar; public bool m_smoothDrain,m_smoothFill;
    public float Value=1f,Width,Max=1f;
    public void SetValue(float value) { Value=value; }
    public void SetWidth(float width) { Width=width; }
    public void SetMaxValue(float max) { Max=max; }
    public void SetColor(Color color) { m_bar.GetComponent<UnityEngine.UI.Image>().color=color; }
}
public class NativeResourceUpdater : Behaviour {}
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
public static class Utils
{
    public static Camera GetMainCamera() { return Camera.main; }
    public static Vector3 WorldToScreenPointScaled(this Camera camera,Vector3 world)
    {
        Vector3 result=camera.WorldToScreenPoint(world);
        result.x*=Screen.width/(float)camera.pixelWidth;
        result.y*=Screen.height/(float)camera.pixelHeight;
        return result;
    }
}
namespace UnityEngine
{
    public static class Time { public static float unscaledTime; public static int frameCount; }
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
        public string name;
        public static void Destroy(GameObject obj) { obj.Destroyed=true; }
        public static GameObject Instantiate(GameObject source,Transform parent,bool world)
        {
            var objects=new Dictionary<Object,Object>();
            GameObject Clone(GameObject original,Transform newParent)
            {
                var copy=new GameObject(original.name,typeof(RectTransform)); copy.activeSelf=original.activeSelf;
                copy.transform.SetParent(newParent,false); objects[original]=copy; objects[original.transform]=copy.transform;
                foreach (var component in original.Components.Values.OfType<Component>())
                {
                    var clone=(Component)Activator.CreateInstance(component.GetType()); clone.gameObject=copy;
                    copy.Components[component.GetType()]=clone; objects[component]=clone;
                }
                foreach (Transform child in original.transform.Children) Clone(child.gameObject,copy.transform);
                return copy;
            }
            var result=Clone(source,parent);
            foreach (var pair in objects)
            {
                if (pair.Key is GameObject) continue;
                foreach (var field in pair.Key.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
                {
                    if (field.Name=="gameObject" || field.Name=="parent" || field.Name=="Children") continue;
                    object value=field.GetValue(pair.Key);
                    if (value is Object original && objects.TryGetValue(original,out Object clone)) value=clone;
                    field.SetValue(pair.Value,value);
                }
            }
            return result;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() { return gameObject.GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>(bool inactive) { return gameObject.GetComponentsInChildren<T>(inactive); }
    }
    public class Behaviour : Component { public bool enabled=true; }
    public class Animator : Behaviour {}
    public class Sprite : Object {}
    public static class Debug { public static readonly List<string> Warnings=new List<string>(); public static void LogWarning(string text) { Warnings.Add(text); } }
    public class GameObject : Object
    {
        public bool activeSelf=true,Destroyed;
        public Transform transform;
        public readonly Dictionary<Type,object> Components=new Dictionary<Type,object>();
        public GameObject(string n,params Type[] types) { name=n; transform=Array.IndexOf(types,typeof(RectTransform))>=0 ? new RectTransform() : new Transform(); transform.gameObject=this; }
        public T AddComponent<T>() where T : new() { var result=new T(); if (result is Component component) component.gameObject=this; Components[typeof(T)]=result; return result; }
        public T GetComponent<T>() { if (transform is T t) return t; return Components.Values.OfType<T>().FirstOrDefault(); }
        public T[] GetComponentsInChildren<T>(bool inactive)
        {
            var result=Components.Values.OfType<T>().ToList();
            foreach (Transform child in transform.Children) if (inactive || child.gameObject.activeSelf) result.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));
            return result.ToArray();
        }
        public void SetActive(bool active) { activeSelf=active; }
    }
    public class Transform : Component
    {
        public Vector3 position,localPosition,localScale=Vector3.one;
        public Transform parent;
        public readonly List<Transform> Children=new List<Transform>();
        public void SetParent(Transform p,bool world) { parent=p; p.Children.Add(this); }
        public bool IsChildOf(Transform ancestor) { for(Transform current=this;current!=null;current=current.parent) if(current==ancestor)return true; return false; }
        public int GetSiblingIndex() { return parent.Children.IndexOf(this); }
        public Transform GetChild(int index) { return Children[index]; }
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
        public enum Axis { Horizontal,Vertical }
        public Vector2 anchorMin,anchorMax,pivot,sizeDelta,offsetMin,offsetMax;
        public Bounds TestBounds;
        public Rect rect
        {
            get { var parentRect=parent as RectTransform; return new Rect(0,0,sizeDelta.x+(parentRect==null?0:(anchorMax.x-anchorMin.x)*parentRect.rect.width),sizeDelta.y+(parentRect==null?0:(anchorMax.y-anchorMin.y)*parentRect.rect.height)); }
        }
        public void SetSizeWithCurrentAnchors(Axis axis,float size)
        {
            var parentRect=parent as RectTransform;
            if(axis==Axis.Horizontal) sizeDelta.x=size-(parentRect==null?0:(anchorMax.x-anchorMin.x)*parentRect.rect.width);
            else sizeDelta.y=size-(parentRect==null?0:(anchorMax.y-anchorMin.y)*parentRect.rect.height);
        }
    }
    public struct Bounds { public Vector3 center,min,max; }
    public static class RectTransformUtility
    {
        public static Bounds CalculateRelativeRectTransformBounds(Transform parent,RectTransform rect) { return rect.TestBounds; }
    }
    public class Camera
    {
        public static Camera main = new Camera();
        public Transform transform = new Transform();
        public int pixelWidth=1920,pixelHeight=1080,Projections;
        public bool Behind;
        public Vector3 WorldToScreenPoint(Vector3 p) { Projections++; return new Vector3(100+p.x-transform.position.x,100+p.y-transform.position.y,Behind?-1:1); }
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
        public static Rect LastRect;
        public static void Label(Rect r,string t,GUIStyle s) { Texts.Add(t); LastFontSize=s.fontSize; LastRect=r; }
    }
    public enum EventType { Layout,Repaint }
    public class Event { public static Event current=new Event(); public EventType type=EventType.Repaint; }
    public static class Mathf
    {
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static int Clamp(int i,int min,int max) { return Math.Min(max,Math.Max(min,i)); }
        public static float Max(float a,float b) { return Math.Max(a,b); }
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour { public UnityEngine.Color color; public bool raycastTarget; }
    public class Image : Graphic { public enum Type { Simple,Sliced,Tiled }; public UnityEngine.Sprite sprite; public Type type; }
}
namespace TMPro
{
    public enum TextAlignmentOptions { Center }
    public class TMP_Text : UnityEngine.UI.Graphic
    {
        public string text; public bool enableAutoSizing; public float fontSize;
        public UnityEngine.Object font,fontSharedMaterial; public TextAlignmentOptions alignment;
        public UnityEngine.RectTransform rectTransform { get { return (UnityEngine.RectTransform)gameObject.transform; } }
        public UnityEngine.Vector2 GetPreferredValues(string value) { return new UnityEngine.Vector2(value.Length*fontSize*0.5f,16); }
    }
}
