using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Loads the production DLL in Unity. Component checks are not Valheim gameplay tests.
public static class ShieldVisualChecks
{
    private static Assembly mod, game, bep;
    private static int checks;
    private static string root;
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public static void Run()
    {
        root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string managed = Environment.GetEnvironmentVariable("SHIELD_CHECK_MANAGED") ?? @"G:\Games\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed";
        string core = Environment.GetEnvironmentVariable("SHIELD_CHECK_CORE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"r2modmanPlus-local\Valheim\profiles\Default\BepInEx\core");
        string plugins=Path.GetFullPath(Path.Combine(core,"..","plugins"));
        AppDomain.CurrentDomain.AssemblyResolve += (sender,args) => {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] { managed, core }) {
                string path = Path.Combine(directory,name);
                if (File.Exists(path) && (!name.StartsWith("UnityEngine") || name=="UnityEngine.UI.dll")) return Assembly.LoadFrom(path);
            }
            if (!name.StartsWith("UnityEngine") && Directory.Exists(plugins)) {
                string dependency=Directory.GetFiles(plugins,name,SearchOption.AllDirectories).FirstOrDefault();
                if(dependency!=null) return Assembly.LoadFrom(dependency);
            }
            return null;
        };
        Assembly.LoadFrom(Path.Combine(managed,"assembly_utils.dll"));
        game = Assembly.LoadFrom(Path.Combine(managed,"assembly_valheim.dll"));
        bep = Assembly.LoadFrom(Path.Combine(core,"BepInEx.dll"));
        string testRoot=Path.Combine(root,"artifacts","visual-test-bepinex");
        bep.GetType("BepInEx.Paths").GetMethod("SetExecutablePath",All).Invoke(null,new object[]{Path.Combine(testRoot,"valheim.exe"),testRoot,managed,new[]{core,managed}});
        Assembly harmony = Assembly.LoadFrom(Path.Combine(core,"0Harmony.dll"));
        mod = Assembly.LoadFrom(Path.Combine(root,"bin","Release","ImprovedMagicShield.dll"));
        Check(mod.GetName().Version == new Version(1,0,0,0), "Release assembly version");
        Check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Run with graphics initialized to exercise the real client controller");
        Visuals(); ShieldShape(); ConfigurationAndController(); NativeLabelGeometry(); NativeShieldRow();
        Type ht=harmony.GetType("HarmonyLib.Harmony"); object patcher=Activator.CreateInstance(ht,new object[]{"ImprovedMagicShield.VisualChecks"});
        try { ht.GetMethod("PatchAll",new[]{typeof(Assembly)}).Invoke(patcher,new object[]{mod}); Check(true,"All gameplay and visual Harmony hooks bind installed game signatures"); }
        finally { ht.GetMethod("UnpatchSelf").Invoke(patcher,null); }
        string message="ImprovedMagicShield 1.0.0 Unity component checks passed: "+checks;
        Debug.Log(message); File.WriteAllText(Path.Combine(root,"artifacts","visual-checks-1.0.0.txt"),message+"\nNo Valheim gameplay or multiplayer test.\n");
    }

    private static void ConfigurationAndController()
    {
        string path=Path.Combine(root,"artifacts","visual-config-fixture.cfg");
        if(File.Exists(path)) File.Delete(path);
        object config=Activator.CreateInstance(bep.GetType("BepInEx.Configuration.ConfigFile"),new object[]{path,false});
        object settings=New("VisualSettings",config);
        Check(!(bool)Value(settings,"Enabled"),"Visual master is disabled by default");
        Check((int)Value(settings,"Color")==269 && (int)Value(settings,"Glow")==72 && (int)Value(settings,"Light")==35 && (int)Value(settings,"Opacity")==30 && (int)Value(settings,"Ovality")==50,"Screenshot values are the initial slider defaults");
        Call(config,"Save"); string text=File.ReadAllText(path);
        Check(text.Contains("Magic Shield Effect = false") && !text.Contains("Change color ="),"One visual switch replaces Change color");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(text,@"[\u0400-\u04FF]"),"Generated UI text is English");
        foreach(var field in settings.GetType().GetFields(All))
            if(field.FieldType.IsGenericType) Check(new[]{typeof(bool),typeof(int)}.Contains(field.FieldType.GetGenericArguments()[0]),"Integer numeric control "+field.Name);
        SetValue(settings,"Color",999); Check((int)Value(settings,"Color")==360,"Hue upper range clamp");
        SetValue(settings,"Color",-1); Check((int)Value(settings,"Color")==0,"Hue lower range clamp");
        SetValue(settings,"Color",120); object options=Call(settings,"Get");
        Check((bool)Get(options,"TintEnabled") && (Color)Get(options,"Tint")==Color.green,"Color automatically applies with the visual master");
        object saved=New("VisualSettings",config); Check((int)Value(saved,"Color")==120,"Rebinding preserves a saved slider value");
        string managerPath=Environment.GetEnvironmentVariable("SHIELD_CHECK_CONFIG_MANAGER") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"r2modmanPlus-local\Valheim\profiles\Default\BepInEx\plugins\shudnal-ConfigurationManager\ConfigurationManager.dll");
        if(File.Exists(managerPath)) {
            Type entryType=Assembly.LoadFrom(managerPath).GetType("ConfigurationManager.ConfigSettingEntry",true);
            object enabledRow=Activator.CreateInstance(entryType,All,null,new[]{Get(settings,"Enabled"),null},null);
            int enabledOrder=(int)entryType.GetProperty("Order",All).GetValue(enabledRow,null);
            Check(enabledOrder==100,"Installed ConfigurationManager reads the master switch priority");
            foreach(string field in new[]{"Color","Glow","Light","Opacity","Ovality"}) {
                object row=Activator.CreateInstance(entryType,All,null,new[]{Get(settings,field),null},null);
                Check((int)entryType.GetProperty("Order",All).GetValue(row,null)<enabledOrder,"Magic Shield Effect precedes "+field);
            }
        } else Debug.LogWarning("ConfigurationManager ordering checks skipped: set SHIELD_CHECK_CONFIG_MANAGER to its DLL.");

        object controller=New("ShieldVisualController",settings,new Action<string>(Debug.LogWarning));
        Call(controller,"Tick",true); // Finish initial catalog rebuild while disabled.
        object catalog=Get(controller,"Catalog");
        var shield=ScriptableObject.CreateInstance(game.GetType("SE_Shield"));
        object list=ListWithPrefab("shield visual fixture"); Set(shield,"m_startEffects",list); Call(catalog,"Register",shield);
        var effect=new GameObject("shield visual fixture(Clone)"); var light=effect.AddComponent<Light>(); light.intensity=8;
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name="Sphere"; sphere.transform.SetParent(effect.transform);
        var filter=sphere.GetComponent<MeshFilter>(); Mesh nativeMesh=filter.sharedMesh;
        var renderer=sphere.GetComponent<MeshRenderer>(); Material nativeMaterial=renderer.sharedMaterial;
        var ambient=new GameObject("unrelated_torch(Clone)"); var otherLight=ambient.AddComponent<Light>(); otherLight.intensity=9;
        try {
            Call(controller,"ObserveNetwork",effect); Call(controller,"Tick",true);
            Near(light.intensity,8,"Disabled module makes no light writes");
            Check(filter.sharedMesh==nativeMesh && renderer.sharedMaterial==nativeMaterial,"Disabled module creates no mesh/material clones");
            SetValue(settings,"Enabled",true); Call(controller,"Tick",true);
            Near(light.intensity,2.8f,"Enabling discovers an already existing bubble");
            Near(filter.sharedMesh.bounds.size.x,nativeMesh.bounds.size.x*0.75f,"Controller applies ovality");
            Near(renderer.sharedMaterial.color.a,nativeMaterial.color.a*0.3f,"Controller applies opacity");
            Check(renderer.sharedMaterial.color.g>renderer.sharedMaterial.color.r,"Controller applies selected hue without a separate tint switch");
            for(int i=0;i<12;i++) { Call(controller,"ReleaseLightWrites"); Call(controller,"ObserveNetwork",effect); Call(controller,"Tick",true); }
            Near(light.intensity,2.8f,"Repeated observation and frames do not compound light");
            SetValue(settings,"Opacity",0); Call(controller,"Tick",true); Check(renderer.forceRenderingOff,"Zero opacity hides shield renderer");
            SetValue(settings,"Enabled",false); Call(controller,"Tick",true);
            Check(filter.sharedMesh==nativeMesh && renderer.sharedMaterial==nativeMaterial && !renderer.forceRenderingOff,"Disabling restores original resources and visibility");
            Near(light.intensity,8,"Disabling restores native light");
            SetValue(settings,"Enabled",true); Call(controller,"Tick",false);
            Check(filter.sharedMesh==nativeMesh,"Global mod switch also disables visuals");
            SetValue(settings,"Opacity",30); Call(controller,"Tick",true);
            Near(light.intensity,2.8f,"Re-enabling works on an existing bubble");
            Near(otherLight.intensity,9,"Unrelated scene effects are untouched");
            EffectsControlHandoff(controller,settings,effect,light);
            ConflictTransitions(controller,settings,effect,light);
        } finally {
            ((IDisposable)controller).Dispose();
            Near(light.intensity,8,"Controller unload restores current bubble");
            Check(filter.sharedMesh==nativeMesh && renderer.sharedMaterial==nativeMaterial,"Unload releases owned mesh and materials");
            UnityEngine.Object.DestroyImmediate(effect); UnityEngine.Object.DestroyImmediate(ambient); UnityEngine.Object.DestroyImmediate(shield);
            foreach(object data in (Array)Get(list,"m_effectPrefabs")) UnityEngine.Object.DestroyImmediate((GameObject)Get(data,"m_prefab"));
        }
    }

    private static void EffectsControlHandoff(object controller,object settings,GameObject effect,Light light)
    {
        // Exercise coexistence against the other mod's real production light sampler.
        Assembly effects=Assembly.LoadFrom(Path.GetFullPath(Path.Combine(root,"..","EffectsControl","bin","Release","EffectsControl.dll")));
        object ecState=Activator.CreateInstance(effects.GetType("EffectsControl.VisualState"),new[]{(object)effect,Enum.Parse(effects.GetType("EffectsControl.EffectStage"),"Shield")});
        object original=Activator.CreateInstance(effects.GetType("EffectsControl.VisualOptions"));
        try {
            // Restore the first capture and create it again at native intensity, just as the
            // EffectsControl master-off pass runs before this mod takes ownership.
            ((IDisposable)ecState).Dispose();
            SetValue(settings,"Enabled",false); Call(controller,"Tick",true);
            ecState=Activator.CreateInstance(effects.GetType("EffectsControl.VisualState"),new[]{(object)effect,Enum.Parse(effects.GetType("EffectsControl.EffectStage"),"Shield")});
            Call(ecState,"Apply",original); SetValue(settings,"Enabled",true); Call(controller,"Tick",true);
            for(int i=0;i<12;i++) {
                Call(controller,"ReleaseLightWrites");
                Call(ecState,"Apply",original);
                Call(controller,"Tick",true);
                Near(light.intensity,2.8f,"EffectsControl inactive shield sampler cannot accumulate light scaling");
            }
            Call(controller,"ReleaseLightWrites"); Call(ecState,"Apply",original);
            SetValue(settings,"Enabled",false); Call(controller,"Tick",true); Near(light.intensity,8,"Handoff back restores original light despite other mod sampling");
        } finally { ((IDisposable)ecState).Dispose(); }
    }

    private static void ConflictTransitions(object controller,object settings,GameObject effect,Light light)
    {
        Assembly effects=Assembly.LoadFrom(Path.GetFullPath(Path.Combine(root,"..","EffectsControl","bin","Release","EffectsControl.dll")));
        var owner=new GameObject("EffectsControl compatibility fixture"); owner.SetActive(false);
        Component plugin=owner.AddComponent(effects.GetType("EffectsControl.Plugin"));
        object config=plugin.GetType().GetProperty("Config").GetValue(plugin,null);
        object ecSettings=Activator.CreateInstance(effects.GetType("EffectsControl.Settings"),All,null,new[]{config},null);
        object info=Activator.CreateInstance(bep.GetType("BepInEx.PluginInfo"));
        object metadata=Activator.CreateInstance(bep.GetType("BepInEx.BepInPlugin"),new object[]{"Slikfoul.EffectsControl","EffectsControl","1.0.1"});
        info.GetType().GetProperty("Metadata",All).SetValue(info,metadata,null);
        info.GetType().GetProperty("Instance",All).SetValue(info,plugin,null);
        var plugins=(IDictionary)bep.GetType("BepInEx.Bootstrap.Chainloader").GetProperty("PluginInfos",All).GetValue(null,null);
        const string key="Slikfoul.EffectsControl";
        plugins.Add(key,info);
        try {
            SetValue(ecSettings,"Enabled",true); SetValue(ecSettings,"Shields",true);
            SetValue(settings,"Enabled",true); Call(controller,"Tick",true);
            Near(light.intensity,8,"Active EffectsControl shield section prevents new visual writes");
            SetValue(ecSettings,"Shields",false); Call(controller,"Tick",true);
            Near(light.intensity,2.8f,"Disabling only EffectsControl shield controls permits local controls");
            Call(controller,"ReleaseLightWrites"); SetValue(ecSettings,"Shields",true); Call(controller,"Tick",true);
            Near(light.intensity,8,"Enabling conflicting controls releases local light ownership");
            SetValue(ecSettings,"Enabled",false); Call(controller,"Tick",true);
            Near(light.intensity,2.8f,"Disabled EffectsControl master permits local controls");
            plugins.Remove(key);
            object subtle=Activator.CreateInstance(bep.GetType("BepInEx.PluginInfo"));
            subtle.GetType().GetProperty("Metadata",All).SetValue(subtle,Activator.CreateInstance(bep.GetType("BepInEx.BepInPlugin"),new object[]{"fixture.subtleshield","SubtleShield","1.0.0"}),null);
            plugins.Add("fixture.subtleshield",subtle); Call(controller,"Tick",true);
            Near(light.intensity,8,"SubtleShield automatically suspends local customization");
            plugins.Remove("fixture.subtleshield"); Call(controller,"Tick",true);
            Near(light.intensity,2.8f,"Removing conflict resumes customization on existing bubble");
            SetValue(settings,"Enabled",false); Call(controller,"Tick",true);
        } finally { plugins.Remove(key); plugins.Remove("fixture.subtleshield"); UnityEngine.Object.DestroyImmediate(owner); }
    }

    private static void NativeShieldRow()
    {
        var host=new GameObject("native resource fixture"); host.SetActive(false);
        Component hud=host.AddComponent(game.GetType("Hud",true));
        var canvasRoot=new GameObject("hudroot",typeof(RectTransform),typeof(Canvas)); canvasRoot.SetActive(false);
        Type hudType=mod.GetType("ImprovedMagicShield.ShieldHud",true);
        object bar=Activator.CreateInstance(hudType,true);
        Type barType=hudType.GetField("fast",All).FieldType;
        Type tmpType=Assembly.Load("Unity.TextMeshPro").GetType("TMPro.TextMeshProUGUI",true);
        RectTransform Panel(string name) {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(canvasRoot.transform,false); rect.sizeDelta=new Vector2(200,32); return rect;
        }
        Component ResourceBar(string name,RectTransform parent) {
            var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);
            var fill=(RectTransform)new GameObject("bar",typeof(RectTransform)).transform;
            fill.SetParent(rect,false); fill.sizeDelta=new Vector2(184,16);
            Component component=rect.gameObject.AddComponent(barType); Set(component,"m_bar",fill); return component;
        }
        var stamina=Panel("staminapanel"); var eitr=Panel("eitrpanel"); var adrenaline=Panel("adrenalinepanel");
        // Installed vanilla prefab: Eitr panel at 130, height 32 with bottom pivot;
        // artwork offset 23. Its center is 169, so the fixed shield center is 192.
        eitr.anchorMin=eitr.anchorMax=new Vector2(0.5f,0);
        eitr.pivot=new Vector2(0.5f,0); eitr.anchoredPosition=new Vector2(0,130);
        var body=(RectTransform)new GameObject("Stamina",typeof(RectTransform)).transform;
        body.SetParent(eitr,false); body.anchorMax=Vector2.one; body.sizeDelta=new Vector2(-16,-16);
        body.anchoredPosition=new Vector2(0,23); body.localScale=new Vector3(1,0.5f,1);
        Component fast=ResourceBar("eitr_fast",body), slow=ResourceBar("eitr_slow",body);
        var name=(RectTransform)new GameObject("EitrText",typeof(RectTransform)).transform; name.SetParent(body,false);
        Component text=name.gameObject.AddComponent(tmpType);
        Set(hud,"m_rootObject",canvasRoot); Set(hud,"m_eitrBarRoot",eitr); Set(hud,"m_staminaBar2Root",stamina);
        Set(hud,"m_adrenalineBarRoot",adrenaline); Set(hud,"m_eitrBarFast",fast); Set(hud,"m_eitrBarSlow",slow); Set(hud,"m_eitrText",text);
        try {
            Call(bar,"Create",hud);
            var row=(RectTransform)Get(bar,"root"); var art=(RectTransform)Get(bar,"visual");
            var group=row.GetComponent<CanvasGroup>();
            Check(row.parent==canvasRoot.transform && row.anchorMin==new Vector2(0.5f,0) && row.anchorMax==row.anchorMin,"Shield row uses the native canvas bottom-center anchor");
            Check(row.anchoredPosition==new Vector2(0,192) && row.pivot==new Vector2(0.5f,0.5f),"Shield row has a deterministic position and centered width changes");
            Check(art.anchoredPosition==Vector2.zero && art.localScale==Vector3.one,"Clone discards the current animated Eitr position/scale");
            Near(art.position.y-body.position.y,23f,"Shield uses the same native 23-unit resource-row spacing");
            Check(group!=null && !group.interactable && !group.blocksRaycasts,"Native shield CanvasGroup fades without intercepting controls");
            eitr.anchoredPosition=new Vector2(0,700); stamina.anchoredPosition=new Vector2(800,320); body.anchoredPosition=new Vector2(0,-300);
            eitr.gameObject.SetActive(false); adrenaline.gameObject.SetActive(false);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,80);
            Check(row.anchoredPosition==new Vector2(0,192) && art.anchoredPosition==Vector2.zero,"Resource movement, visibility and shield width leave its row unchanged");
            var alpha=mod.GetType("ImprovedMagicShield.HudPolicy").GetMethod("Alpha",All);
            foreach(var sample in new[]{new Vector2(0,1),new Vector2(5,1),new Vector2(5.25f,0.5f),new Vector2(5.5f,0),new Vector2(100,0)}) {
                group.alpha=(float)alpha.Invoke(null,new object[]{sample.x}); Near(group.alpha,sample.y,"Real CanvasGroup receives quiet-time fade alpha");
            }
        } finally {
            Call(bar,"Destroy"); UnityEngine.Object.DestroyImmediate(canvasRoot); UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void NativeLabelGeometry()
    {
        Type enemyType=game.GetType("EnemyHud",true), characterType=game.GetType("Character",true);
        var enemyRoot=new GameObject("native EnemyHud fixture"); enemyRoot.SetActive(false);
        Component enemy=enemyRoot.AddComponent(enemyType);
        FieldInfo singleton=enemyType.GetField("m_instance",All); object previous=singleton.GetValue(null);
        singleton.SetValue(null,enemy);
        var actor=new GameObject("native Character fixture"); actor.SetActive(false);
        Component character=actor.AddComponent(characterType);
        var header=new GameObject("native header",typeof(RectTransform),typeof(Canvas));
        Canvas canvas=header.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.scaleFactor=1.5f;
        var name=(RectTransform)new GameObject("Name",typeof(RectTransform)).transform;
        name.SetParent(header.transform,false); name.sizeDelta=new Vector2(160,24); name.anchoredPosition=new Vector2(0,45);
        var health=(RectTransform)new GameObject("Health",typeof(RectTransform)).transform;
        health.SetParent(header.transform,false); health.sizeDelta=new Vector2(180,10); health.anchoredPosition=new Vector2(0,15);
        object data=Activator.CreateInstance(enemyType.GetNestedType("HudData",BindingFlags.NonPublic),true); Set(data,"m_gui",header);
        var huds=(IDictionary)enemyType.GetField("m_huds",All).GetValue(enemy); huds.Add(character,data);
        object anchor=Activator.CreateInstance(mod.GetType("ImprovedMagicShield.ShieldLabelAnchor",true),true);
        var corners=new Vector3[4];
        try {
            Canvas.ForceUpdateCanvases();
            object[] args={character,Vector2.zero,1f};
            Check((bool)Call(anchor,"TryLowerEdge",args),"Production anchor resolves installed EnemyHud fields");
            health.GetWorldCorners(corners);
            Near(((Vector2)args[1]).y,corners.Min(p=>p.y),"Real RectTransform health lower edge");
            Near((float)args[2],canvas.scaleFactor,"Real canvas scale is preserved for the label gap");
            args=new object[]{character,Vector2.zero,1f};
            Check((bool)Call(anchor,"TryUpperNameEdge",args),"Production anchor resolves name-only upper edge");
            name.GetWorldCorners(corners); Near(((Vector2)args[1]).y,corners.Max(p=>p.y),"Above-name placement uses the real native name top");
            health.gameObject.SetActive(false); args=new object[]{character,Vector2.zero,1f}; Call(anchor,"TryLowerEdge",args);
            name.GetWorldCorners(corners); Near(((Vector2)args[1]).y,corners.Min(p=>p.y),"Hidden real health bar leaves the visible name as anchor");
            header.SetActive(false); args=new object[]{character,Vector2.zero,1f}; Check(!(bool)Call(anchor,"TryLowerEdge",args),"Hidden real header requests character fallback");
            header.SetActive(true); health.gameObject.SetActive(true);
            Type tmpType=Assembly.Load("Unity.TextMeshPro").GetType("TMPro.TextMeshProUGUI",true);
            Component nativeText=name.gameObject.AddComponent(tmpType);
            Call(anchor,"TryRender",character,true,365f,100,0);
            var caption=(RectTransform)header.transform.Find("ImprovedMagicShield.Protection");
            Check(caption!=null && caption.parent==header.transform,"Protection caption belongs to the native header canvas");
            Component captionText=caption.GetComponent(tmpType);
            Check((string)captionText.GetType().GetProperty("text").GetValue(captionText,null)=="365","Real TMP caption receives remaining protection");
            name.GetWorldCorners(corners); float nativeTop=corners.Max(p=>p.y);
            caption.GetWorldCorners(corners); Near(corners.Min(p=>p.y),nativeTop+4f*canvas.scaleFactor,"Real native caption stays above the name without overlap");
            Vector3 localPose=caption.localPosition;
            Vector3 captionWorld=caption.position;
            header.transform.position+=new Vector3(73,19,0);
            Near(caption.position.x-captionWorld.x,73,"Caption follows native header movement without a new screen projection");
            Near(caption.position.y-captionWorld.y,19,"Caption shares native header vertical movement");
            Call(anchor,"TryRender",character,true,364f,100,20);
            Near(caption.localPosition.y,localPose.y+20,"Real native caption manual offset");
            for(int frame=0;frame<25;frame++) Call(anchor,"TryRender",character,true,364f,100,20);
            Check(header.GetComponentsInChildren(tmpType,true).Length==2,"Native caption is reused without per-frame clones");
            Call(anchor,"TryRender",character,true,364f,300,0);
            name.GetWorldCorners(corners); nativeTop=corners.Max(p=>p.y);
            caption.GetWorldCorners(corners); Near(corners.Min(p=>p.y),nativeTop+4f*canvas.scaleFactor,"Large TMP caption grows away from the name");
            Call(anchor,"Hide"); Check(!caption.gameObject.activeSelf,"Real native caption hides with display lifecycle");
            Call(anchor,"TryRender",character,false,300f,100,-10);
            health.GetWorldCorners(corners); float nativeBottom=corners.Min(p=>p.y);
            caption.GetWorldCorners(corners); Near(corners.Max(p=>p.y),nativeBottom-14f*canvas.scaleFactor,"Other native caption stays below health with downward offset");
            header.SetActive(false);
            Check(!(bool)Call(anchor,"TryRender",character,true,300f,100,0) && !caption.gameObject.activeSelf,"Hidden real header hides its protection caption");
            Call(anchor,"Dispose");
            var widthPolicy=mod.GetType("ImprovedMagicShield.HudPolicy").GetMethod("Width",All);
            foreach(float capacity in new[]{200f,365f,700f}) {
                float previousWidth=float.PositiveInfinity;
                foreach(int segment in new[]{50,72,73,100,200,500}) {
                    float width=(float)widthPolicy.Invoke(null,new object[]{capacity,segment});
                    Near(width,capacity/segment*32f,"Real production width follows segment scale");
                    Check(width<previousWidth,"Real production width continues shrinking beyond 72"); previousWidth=width;
                }
            }
        } finally {
            singleton.SetValue(null,previous);
            UnityEngine.Object.DestroyImmediate(header); UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(enemyRoot);
            singleton.SetValue(null,previous);
        }
    }

    private static void Visuals()
    {
        GameObject root = new GameObject("weapon fixture"); root.transform.localScale = new Vector3(2,3,4);
        root.transform.position = new Vector3(4,5,6);
        var collider = root.AddComponent<SphereCollider>(); collider.radius = 7;
        var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.mass = 13;
        var audio = root.AddComponent<AudioSource>(); audio.volume = 0.7f;
        GameObject ambient = new GameObject("unrelated weather and building light");
        Light unrelated = ambient.AddComponent<Light>(); unrelated.intensity = 9;
        Light light = new GameObject("impact light").AddComponent<Light>(); light.transform.SetParent(root.transform); light.intensity=8; light.range=12;
        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere); bubble.name="bubble mesh"; bubble.transform.SetParent(root.transform);
        var filter = bubble.GetComponent<MeshFilter>(); Mesh mesh = filter.sharedMesh;
        var meshCollider = bubble.GetComponent<SphereCollider>(); float meshRadius=meshCollider.radius;
        Material material = new Material(Shader.Find("Standard")); material.color = new Color(0.8f,0.4f,0.2f,0.6f); material.SetColor("_EmissionColor", new Color(4,2,1,1));
        var renderer = bubble.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        Vector3[] originalVertices=mesh.vertices; Vector3 visualScale=bubble.transform.localScale;
        var ps = new GameObject("cloud").AddComponent<ParticleSystem>(); ps.transform.SetParent(root.transform);
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main; main.maxParticles=32; main.startLifetime=60; main.startSize=4; main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission; emission.enabled=false; emission.rateOverTime=new ParticleSystem.MinMaxCurve(20,40); emission.rateOverDistance=10;
        var burst=new ParticleSystem.Burst(0.2f, new ParticleSystem.MinMaxCurve(12,20)); burst.cycleCount=3; burst.repeatInterval=0.5f; burst.probability=0.8f;
        emission.SetBursts(new[]{burst});
        ps.Play(); ps.Pause();
        var initial=new[]{Particle(7,4,200,30),Particle(7,6,120,20)}; ps.SetParticles(initial,2);
        Check(ps.GetParticles(initial)==2,"Fixture contains two native particles before observation");
        bool fog=RenderSettings.fog; float fogDensity=RenderSettings.fogDensity;
        object state = New("VisualState",root,Stage("Shield"));
        try
        {
            object half=Options(0.5f,0.5f,0.5f,0.5f,0.5f);
            Call(state,"Apply",half);
            Near(light.intensity,4,"Light halved"); Near(light.range,12,"Light range preserved");
            Near(emission.rateOverTime.constantMin,10,"Emission lower bound"); Near(emission.rateOverTime.constantMax,20,"Emission upper bound");
            Near(emission.rateOverDistance.constant,5,"Distance emission");
            var actualBurst=new ParticleSystem.Burst[1]; emission.GetBursts(actualBurst);
            Near(actualBurst[0].count.constantMax,10,"Burst count"); Near(actualBurst[0].time,0.2f,"Burst timing unchanged");
            Check(actualBurst[0].cycleCount==3 && actualBurst[0].repeatInterval==0.5f && actualBurst[0].probability==0.8f,"Burst schedule preserved");
            var values=new ParticleSystem.Particle[2]; Check(ps.GetParticles(values)==2,"Particle count retained");
            Near(values[0].startSize,2,"Particle size halved"); Check(values[0].startColor.a==100 && values[1].startColor.a==60,"Particle alpha and duplicate seeds");
            Check(values[0].position==initial[0].position && values[0].velocity==initial[0].velocity && values[0].remainingLifetime==initial[0].remainingLifetime,"Particle physics and lifetime retained: position="+values[0].position+"/"+initial[0].position+" velocity="+values[0].velocity+"/"+initial[0].velocity+" lifetime="+values[0].remainingLifetime+"/"+initial[0].remainingLifetime);
            values=new[]{values[1],values[0]}; ps.SetParticles(values,2);
            for(int i=0;i<20;i++) Call(state,"Apply",half);
            ps.GetParticles(values); Check(values[0].startColor.a==60 && values[1].startColor.a==100,"Repeated and reordered particles do not compound");
            Near(light.intensity,4,"Repeated light does not compound"); Near(filter.sharedMesh.vertices[0].x,originalVertices[0].x*0.5f,"Mesh shrink from native vertices");
            Check(mesh.vertices.SequenceEqual(originalVertices) && filter.sharedMesh!=mesh,"Shared mesh untouched");
            Check(renderer.sharedMaterial!=material && material.color.a==0.6f,"Shared material untouched");
            Near(renderer.sharedMaterial.color.a,0.3f,"Mesh material alpha"); Near(renderer.sharedMaterial.GetColor("_EmissionColor").r,2,"HDR emission scaled");
            // Simulate a native light fade writing a new intensity before LateUpdate.
            light.intensity=3; Call(state,"Apply",half); Near(light.intensity,1.5f,"Native animated light fade respected");
            Call(state,"Apply",Options(0,0,0,0,0));
            Check(renderer.forceRenderingOff && ps.GetComponent<ParticleSystemRenderer>().forceRenderingOff,"Zero hides renderers"); Near(light.intensity,0,"Zero removes light contribution");
            Near(emission.rateOverTime.constantMax,0,"Zero removes automatic emission");
            Call(state,"Apply",New("VisualOptions")); ps.GetParticles(values);
            Check(!renderer.forceRenderingOff && !ps.GetComponent<ParticleSystemRenderer>().forceRenderingOff,"100 restores visible renderers");
            Check(values[0].startColor.a==120 && values[1].startColor.a==200,"100 restores existing native alpha after zero");
            Near(values[0].startSize,6,"100 restores existing native size after zero"); Near(light.intensity,3,"100 restores latest native fade");
            Check(filter.sharedMesh.vertices.SequenceEqual(originalVertices),"100 restores mesh vertices");
            Near(emission.rateOverTime.constantMax,40,"100 restores density");
            object shaped=New("VisualOptions"); Set(shaped,"TintEnabled",true); Set(shaped,"Tint",new Color(0.2f,0.1f,0.05f));
            Call(state,"Apply",shaped);
            Check(filter.sharedMesh.vertices.SequenceEqual(originalVertices),"Tint leaves native mesh position and dimensions unchanged");
            Near(renderer.sharedMaterial.color.r,0.2f,"Shield tint");
            Check(root.transform.localScale==new Vector3(2,3,4) && root.transform.position==new Vector3(4,5,6) && bubble.transform.localScale==visualScale,"Gameplay transforms unchanged");
            Check(collider.radius==7 && meshCollider.radius==meshRadius && body.mass==13 && body.isKinematic && audio.volume==0.7f,"Colliders, rigidbody and sound unchanged");
            Check(unrelated.intensity==9 && RenderSettings.fog==fog && RenderSettings.fogDensity==fogDensity,"Unrelated light and weather unchanged");
        }
        finally { ((IDisposable)state).Dispose(); }
        Check(filter.sharedMesh==mesh && renderer.sharedMaterial==material,"Dispose releases mesh/material ownership");
        Near(emission.rateOverTime.constantMax,40,"Dispose restores emission");
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(ambient); UnityEngine.Object.DestroyImmediate(material);
    }

    private static void ShieldShape()
    {
        var root=new GameObject("vfx_StaffShield"); root.transform.localScale=new Vector3(2,3,4);
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name="Sphere"; sphere.transform.SetParent(root.transform); sphere.transform.localScale=Vector3.one*2.5f;
        var other=GameObject.CreatePrimitive(PrimitiveType.Cube); other.name="Other shield mesh"; other.transform.SetParent(root.transform);
        var filter=sphere.GetComponent<MeshFilter>(); Mesh original=filter.sharedMesh;
        Vector3[] vertices=original.vertices, normals=original.normals; Vector4[] tangents=original.tangents;
        Mesh otherMesh=other.GetComponent<MeshFilter>().sharedMesh; Vector3[] otherVertices=otherMesh.vertices;
        float radius=sphere.GetComponent<SphereCollider>().radius;
        object state=New("VisualState",root,Stage("Shield"));
        try {
            object options=New("VisualOptions");
            foreach(float ovality in new[]{0f,0.5f,1f,0.5f,0f}) {
                Set(options,"ShieldOvality",ovality); Call(state,"Apply",options);
                Vector3 extent=filter.sharedMesh.bounds.size, native=original.bounds.size;
                Near(extent.x,native.x*(1-ovality*0.5f),"Shield oval horizontal extent "+ovality);
                Near(extent.y,native.y,"Shield oval retains height "+ovality);
                Near(extent.z,native.z*(1-ovality*0.5f),"Shield oval depth extent "+ovality);
                Check(other.GetComponent<MeshFilter>().sharedMesh.vertices.SequenceEqual(otherVertices),"Other shield mesh retains its shape");
            }
            Check(filter.sharedMesh.normals.SequenceEqual(normals) && filter.sharedMesh.tangents.SequenceEqual(tangents),"Native normals and tangents restored after oval changes");
            Set(options,"ShieldOvality",0.5f); Call(state,"Apply",options);
            Vector3[] ovalVertices=filter.sharedMesh.vertices;
            for(int i=0;i<20;i++) Call(state,"Apply",options);
            Check(filter.sharedMesh.vertices.SequenceEqual(ovalVertices),"Oval shape does not accumulate");
            Check(original.vertices.SequenceEqual(vertices) && original.normals.SequenceEqual(normals),"Original shield mesh remains unchanged");
            Check(root.transform.localScale==new Vector3(2,3,4) && sphere.transform.localScale==Vector3.one*2.5f && sphere.GetComponent<SphereCollider>().radius==radius,"Oval leaves transforms and collider unchanged");
            Call(state,"Apply",New("VisualOptions")); Check(filter.sharedMesh.vertices.SequenceEqual(vertices),"Disabling oval restores native vertices");
        } finally { ((IDisposable)state).Dispose(); }
        Check(filter.sharedMesh==original,"Shield disposal restores original mesh resource");
        UnityEngine.Object.DestroyImmediate(root);
    }



    private static object ListWithPrefab(string name)
    {
        object list=Activator.CreateInstance(game.GetType("EffectList")); Type effect=game.GetType("EffectList+EffectData"); object data=Activator.CreateInstance(effect); Set(data,"m_prefab",new GameObject(name)); Array array=Array.CreateInstance(effect,1); array.SetValue(data,0); Set(list,"m_effectPrefabs",array); return list;
    }
    private static ParticleSystem.Particle Particle(uint seed,float size,byte alpha,float remaining) { return new ParticleSystem.Particle { randomSeed=seed,startSize=size,startColor=new Color32(120,80,40,alpha),startLifetime=60,remainingLifetime=remaining,position=new Vector3(size,1,2),velocity=Vector3.up }; }
    private static object Options(float light,float glow,float opacity,float size,float density) { object o=New("VisualOptions"); Set(o,"Light",light); Set(o,"Glow",glow); Set(o,"Opacity",opacity); Set(o,"Size",size); Set(o,"Density",density); return o; }
    private static object Stage(string name) { return Enum.Parse(mod.GetType("ImprovedMagicShield.Visuals.EffectStage"),name); }
    private static object New(string type,params object[] args) { return Activator.CreateInstance(mod.GetType("ImprovedMagicShield.Visuals."+type,true),All,null,args,null); }
    private static void Set(object target,string field,object value) { target.GetType().GetField(field,All).SetValue(target,value); }
    private static object Get(object target,string field) { return target.GetType().GetField(field,All).GetValue(target); }
    private static object Value(object settings,string field) { return Get(settings,field).GetType().GetProperty("Value").GetValue(Get(settings,field),null); }
    private static void SetValue(object settings,string field,object value) { Get(settings,field).GetType().GetProperty("Value").SetValue(Get(settings,field),value,null); }
    private static object Call(object target,string method,params object[] args) { return target.GetType().GetMethod(method,All).Invoke(target,args); }
    private static void Near(float actual,float expected,string message) { Check(Mathf.Abs(actual-expected)<0.0001f,message+" expected="+expected+" actual="+actual); }
    private static void Check(bool result,string message) { if(!result) throw new Exception(message); checks++; }
}
