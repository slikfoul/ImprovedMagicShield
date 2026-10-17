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
        AppDomain.CurrentDomain.AssemblyResolve += (sender,args) => {
            string name = new AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] { managed, core }) {
                string path = Path.Combine(directory,name);
                if (File.Exists(path) && !name.StartsWith("UnityEngine")) return Assembly.LoadFrom(path);
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
        Check(mod.GetName().Version == new Version(0,1,0,0), "Release assembly version");
        Check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Run with graphics initialized to exercise the real client controller");
        Visuals(); ShieldShape(); ConfigurationAndController();
        Type ht=harmony.GetType("HarmonyLib.Harmony"); object patcher=Activator.CreateInstance(ht,new object[]{"ImprovedMagicShield.VisualChecks"});
        try { ht.GetMethod("PatchAll",new[]{typeof(Assembly)}).Invoke(patcher,new object[]{mod}); Check(true,"All gameplay and visual Harmony hooks bind installed game signatures"); }
        finally { ht.GetMethod("UnpatchSelf").Invoke(patcher,null); }
        string message="ImprovedMagicShield 0.1.0 Unity component checks passed: "+checks;
        Debug.Log(message); File.WriteAllText(Path.Combine(root,"artifacts","visual-checks-0.1.0.txt"),message+"\nNo Valheim gameplay or multiplayer test.\n");
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
