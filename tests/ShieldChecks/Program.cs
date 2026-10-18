using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ImprovedMagicShield;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;
using OpCodes = System.Reflection.Emit.OpCodes;

internal static class Program
{
    private static int checks;
    private static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Pass installed assembly_valheim.dll and built ImprovedMagicShield.dll.");
        ShieldRefresh.VerifyFields();
        RefreshChecks();
        ManagerChecks();
        DisplayChecks();
        HudChecks();
        HudLifecycleChecks();
        HudVisibilityChecks();
        ActivityDisplayChecks();
        HealthOutlineChecks();
        InstalledIlChecks(args[0], args[1]);
        Console.WriteLine("Passed " + checks + " automated checks: production refresh helpers, rejection state, disabled mode, labels/ZDO test doubles, installed-game IL and emitted replacement block. This is not live Unity or multiplayer testing.");
    }

    private static void HealthOutlineChecks()
    {
        foreach (float capacity in new[] { 200f, 300f, 365f, 700f })
        {
            Check(DisplayPolicy.ProtectionFraction(capacity, capacity) == 1f, "Full shield fraction depends on caster strength.");
            Check(DisplayPolicy.ProtectionFraction(capacity * 0.5f, capacity) == 0.5f, "Half shield fraction differs by capacity.");
        }
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            Check(DisplayPolicy.ProtectionFraction(100f, invalid) == 0f, "Invalid or absent capacity is rendered as a full shield.");
            Check(DisplayPolicy.ProtectionFraction(invalid, 100f) == 0f, "Invalid remaining protection is rendered.");
        }
        var local = new Player(); Player.m_localPlayer = local;
        var player = new Player(); var skeleton = new Character();
        skeleton.gameObject.name = "Skeleton_Friendly(Clone)";
        var unshielded = new Character();
        local.Manager.Effect = new SE_Shield(700, 0);
        player.Manager.Effect = new SE_Shield(700, 327);
        skeleton.Manager.Effect = new SE_Shield(200, 190);
        Character.All.Clear(); Character.All.AddRange(new Character[] { local, player, skeleton, unshielded });
        EnemyHud.instance = new EnemyHud(); Hud.instance = new Hud(); Camera.main = new Camera();
        var headers = new Dictionary<Character, GameObject>();
        var healths = new Dictionary<Character, RectTransform>();
        foreach (Character character in Character.All)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            var name = new GameObject("Name", typeof(RectTransform)); name.transform.SetParent(header.transform, false);
            ((RectTransform)name.transform).TestBounds = new Bounds { min = new Vector3(10, 20, 0), max = new Vector3(110, 44, 0) };
            name.AddComponent<TMPro.TMP_Text>();
            var health = (RectTransform)new GameObject("Health", typeof(RectTransform)).transform;
            health.SetParent(header.transform, false); health.sizeDelta = new Vector2(100, 10);
            health.TestBounds = new Bounds { min = new Vector3(10, 5, 0), max = new Vector3(110, 15, 0) };
            health.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 1, 0);
            headers[character] = header; healths[character] = health; EnemyHud.instance.Set(character, header);
        }
        var display = new ShieldLabels(); Time.unscaledTime += 1; ZNet.instance = new ZNet();
        void Render(bool numbers, bool outline)
        {
            Time.unscaledTime += 1; Time.frameCount++; GUI.Texts.Clear();
            display.Tick(true, numbers, outline); display.PrepareFrame(Camera.main, numbers, 80, -20, outline); display.Draw(80, -20);
        }
        GameObject Rim(Character character) { return healths[character].Find("ImprovedMagicShield.HealthOutline")?.gameObject; }
        float Fraction(GameObject outline) { return ((RectTransform)outline.transform.Find("Progress")).anchorMax.x; }
        Render(false, false);
        Check(Rim(player) == null && Rim(skeleton) == null, "Disabled outline created UI.");
        Check(player.View.Data.GetFloat("Slikfoul.ImprovedMagicShield.Capacity".GetStableHashCode(), 0f) == 700f,
            "Hidden displays stop publishing the applied shield capacity.");
        Render(false, true);
        Check(Rim(player)?.activeInHierarchy == true && Rim(skeleton)?.activeInHierarchy == true, "Outline without numbers misses players or summons.");
        Check(Rim(local) == null && Rim(unshielded) == null && GUI.Texts.Count == 0, "Outline reveals own/absent shield or enables numbers.");
        var original = Rim(player);
        Check(Math.Abs(Fraction(original) - 373f / 700f) < 0.0001f, "Player outline does not show the actual applied shield fraction.");
        Check(Math.Abs(Fraction(Rim(skeleton)) - 0.05f) < 0.0001f, "Skeleton outline assumes a 700-protection maximum.");
        player.Manager.Effect = new SE_Shield(300, 150); Render(false, true);
        Check(Fraction(original) == 0.5f, "Half of a 300-protection shield does not cover half the bar.");
        player.Manager.Effect = new SE_Shield(200, 0); Render(false, true);
        Check(Fraction(original) == 1f, "A full 200-protection shield does not cover the full bar.");
        int capacityKey = "Slikfoul.ImprovedMagicShield.Capacity".GetStableHashCode();
        int protectionKey = "Slikfoul.ImprovedMagicShield.Protection".GetStableHashCode();
        int publisherKey = "Slikfoul.ImprovedMagicShield.Publisher".GetStableHashCode();
        player.View.Owner = false;
        player.Manager.Effect = new SE_Shield(99999, 0);
        player.View.Data.Set(protectionKey, 100f); Render(false, true);
        Check(Fraction(original) == 0.5f, "Remote outline reads a local effect instead of owner capacity.");
        player.View.Data.Set(capacityKey, 0f); Render(false, true);
        Check(!original.activeSelf, "An older owner without capacity gets a made-up outline percentage.");
        player.View.Data.Set(capacityKey, 200f); Render(false, true);
        Check(original.activeSelf, "Fresh remote capacity does not restore its outline.");
        int capacitySampleKey = "Slikfoul.ImprovedMagicShield.CapacitySampleTime".GetStableHashCode();
        long capacitySample = player.View.Data.GetLong(capacitySampleKey, -1L);
        player.View.Data.Set(capacitySampleKey, capacitySample - 1L); Render(false, true);
        Check(!original.activeSelf, "An older peer refresh reuses a previous owner's capacity.");
        player.View.Data.Set(capacitySampleKey, capacitySample);
        player.View.Data.Set(publisherKey, -1L);
        display.PrepareFrame(Camera.main, false, 80, -20, true);
        Check(!original.activeSelf, "Outline accepts a previous owner's snapshot.");
        player.View.Data.Set(publisherKey, player.View.Data.GetOwner());
        ZNet.instance.Now += 3;
        display.PrepareFrame(Camera.main, false, 80, -20, true);
        Check(!original.activeSelf, "Outline displays a stale remote snapshot.");
        ZNet.instance.Now -= 3; player.View.Owner = true; player.Manager.Effect = new SE_Shield(200, 0);
        Render(false, true);
        var edges = original.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        Check(edges.Length == 2 && edges.All(edge => !edge.raycastTarget), "Outline rim/glow must not intercept input.");
        foreach (var edge in edges)
        {
            Check(edge.color.b > edge.color.g && edge.color.a <= 0.85f, "Outline is not translucent purple.");
            Check(edge.type == UnityEngine.UI.Image.Type.Sliced && !edge.fillCenter, "Outline paints over the health center.");
            Check(edge.sprite.texture.GetPixel(0, 0).a == 0f && edge.sprite.texture.GetPixel(32, 32).a == 0f, "Outline corners or center are opaque.");
        }
        var sharedRim = edges.Single(edge => edge.gameObject.name == "Rim").sprite;
        Check(ReferenceEquals(sharedRim, Rim(skeleton).GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(edge => edge.gameObject.name == "Rim").sprite), "Each character creates redundant rounded sprites.");
        Check(((RectTransform)original.transform).anchorMax.x == 1f
            && ((RectTransform)edges.Single(edge => edge.gameObject.name == "Rim").transform).sizeDelta.x == 106f,
            "A partial shield moves a vertical end cap through the native health instead of clipping the full contour.");
        Check(healths[player].sizeDelta.x == 100 && healths[player].sizeDelta.y == 10
            && healths[player].gameObject.GetComponent<UnityEngine.UI.Image>().color.g == 1, "Outline changed native health color or size.");
        player.Manager.Effect.Time += 20; skeleton.Manager.Effect.Time += 20; ZNet.instance.Now += 20;
        Render(false, true);
        Check(Rim(player) == original && original.activeInHierarchy, "Quiet shield lost active outline or cloned it again.");
        healths[player].gameObject.SetActive(false); Render(false, true);
        Check(!original.activeSelf && Rim(skeleton).activeSelf, "Hidden native health must hide only its outline.");
        healths[player].gameObject.SetActive(true); Render(false, true);
        Check(original.activeSelf, "Outline does not recover with the native health.");
        Render(true, false);
        Check(!original.activeSelf, "Number setting re-enabled a disabled outline.");
        Render(false, true); player.Manager.Effect = null; Render(false, true);
        Check(original.Destroyed, "Expired shield retained an outline.");
        headers[skeleton].SetActive(false); Render(false, true);
        Check(!Rim(skeleton).activeSelf, "Hidden header retained visible outline.");
        headers[skeleton].SetActive(true); Render(false, true); Hud.instance.Visible = false;
        display.PrepareFrame(Camera.main, false, 80, -20, true);
        Check(!Rim(skeleton).activeSelf, "Hidden game HUD retained shield outline.");
        Hud.instance.Visible = true; Render(false, true); var last = Rim(skeleton);
        display.Clear(); Check(last.Destroyed, "Outline not disposed on global shutdown.");
        Check(sharedRim.AssetDestroyed && sharedRim.texture.AssetDestroyed, "Shared outline resources remain after the last display is disposed.");
        EnemyHud.instance = null; Character.All.Clear();
    }

    private static void RefreshChecks()
    {
        foreach (var example in new[] { (500f,450f,100f,true), (500f,200f,100f,false), (500f,200f,500f,true), (500f,400f,100f,true) })
        {
            var shield = new SE_Shield(example.Item1,example.Item2) { m_absorbDamage=example.Item3, m_absorbDamagePerSkillLevel=0 };
            var originalSource = new Character(); shield.Source=originalSource;
            var hit = new HitData { m_itemLevel=3,m_skillLevel=57 };
            var effect = ShieldRefresh.RefreshFromHit(shield,hit);
            Check((effect != null)==example.Item4,"Example acceptance differs.");
            if (example.Item4)
            {
                Check(shield.Total==example.Item3 && shield.Absorbed==0 && shield.Time==0 && shield.m_ttl==180,"Accepted cast did not fully replace protection/time.");
                Check(shield.LastItem==3 && shield.LastSkill==57,"Incoming caster parameters were lost.");
            }
            else
                Check(shield.Total==500 && shield.Absorbed==200 && shield.Time==25 && shield.m_ttl==60
                    && shield.LastItem==0 && shield.Source==originalSource && shield.ResetCalls==0 && shield.LevelCalls==0,"Rejected shield changed.");
        }
        foreach (int world in new[] {-1,0,1,3})
        foreach (float skill in new[] {0f,1f,42.5f,100f})
        {
            Game.m_worldLevel=world;
            var shield = new SE_Shield(10000,9999);
            float expected=100+4*skill+(world>0 ? 100*world : 0);
            Check(ShieldRefresh.FullProtection(shield,skill)==expected,"World/skill formula differs.");
            Check(ShieldRefresh.TryRefresh(shield,2,skill) && shield.Total==expected && shield.m_ttl==120 && ShieldRefresh.Remaining(shield)==expected,"World/skill refresh differs.");
        }
        Game.m_worldLevel=0;
        var unchangedTtl = new SE_Shield(100,99) { m_ttlPerItemLevel=0,m_ttl=95 };
        ShieldRefresh.TryRefresh(unchangedTtl,4,2);
        Check(unchangedTtl.m_ttl==95,"Non-scaled vanilla TTL changed.");
        Plugin.IsEnabled=false;
        var disabled = new SE_Shield(500,450);
        Check(ShieldRefresh.RefreshFromHit(disabled,new HitData { m_itemLevel=2 })==disabled,"Disabled mode lost vanilla effect.");
        Check(disabled.Absorbed==450 && disabled.Total==100 && disabled.Time==0,"Disabled mode did not retain vanilla absorbed damage.");
        Plugin.IsEnabled=true;
        var other = new StatusEffect();
        Check(ShieldRefresh.RefreshFromHit(other,new HitData { m_itemLevel=4,m_skillLevel=88 })==other
            && other.Time==0 && other.LastItem==4 && other.LastSkill==88,"Non-shield behavior changed.");
    }

    private static void ManagerChecks()
    {
        foreach (bool objectOverload in new[] { false,true })
        foreach (bool accept in new[] { false,true })
        {
            var shield = new SE_Shield(500,accept ? 450 : 200);
            var manager = new SEMan { Effect=shield };
            StatusEffect result = shield;
            bool run = objectOverload
                ? ManagerObjectShieldHook.Prefix(manager,new StatusEffect(),true,3,0,7,ref result)
                : ManagerHashShieldHook.Prefix(manager,123,true,3,0,7,ref result);
            Check(!run && result==null,"Existing manager effect return contract differs.");
            Check(manager.Effect==shield,"Manager removed or replaced the effect instance.");
            Check(accept ? shield.Absorbed==0 && shield.Total==100 && shield.Time==0 && shield.m_hitVariant==7
                : shield.Absorbed==200 && shield.Total==500 && shield.Time==25 && shield.m_hitVariant==-1,"Manager refresh state differs.");
        }
        foreach (StatusEffect effect in new StatusEffect[] {null,new StatusEffect(),new SE_Shield(500,200)})
        {
            var manager = new SEMan { Effect=effect }; StatusEffect result=null;
            Check(ManagerHashShieldHook.Prefix(manager,123,false,1,0,2,ref result),"resetTime=false did not use vanilla.");
            if (!(effect is SE_Shield)) Check(ManagerHashShieldHook.Prefix(manager,123,true,1,0,2,ref result),"Fresh/non-shield application did not use vanilla.");
            Plugin.IsEnabled=false;
            Check(ManagerHashShieldHook.Prefix(manager,123,true,1,0,2,ref result),"Disabled manager hook did not use vanilla.");
            Plugin.IsEnabled=true;
        }
    }

    private static void DisplayChecks()
    {
        Check(DisplayPolicy.IsFresh(50,100,101,10,10),"Fresh snapshot rejected.");
        Check(!DisplayPolicy.IsFresh(50,100,102.01,10,10),"Expired snapshot accepted.");
        Check(!DisplayPolicy.IsFresh(50,100,101,10,11),"Previous owner snapshot accepted.");
        Check(!DisplayPolicy.IsFresh(float.NaN,100,100,10,10) && !DisplayPolicy.IsFresh(float.PositiveInfinity,100,100,10,10),"Invalid snapshot accepted.");
        Check(DisplayPolicy.WholeProtection(99.9f)==99 && DisplayPolicy.WholeProtection(-3)==0,"Label rounding differs.");
        var local=new Player(); Player.m_localPlayer=local; Hud.instance=new Hud();
        var character=new Player(); var shield=new SE_Shield(500,450.1f);
        character.Manager.Effect=shield; local.Manager.Effect=new SE_Shield(500,200);
        Character.All.Clear(); Character.All.Add(character); Character.All.Add(local);
        Camera.main=new Camera(); var camera=Camera.main;
        var labels=new ShieldLabels(); Time.unscaledTime=1; ZNet.instance.Now=100;
        void Render(bool show=true,int scale=100,int offset=0)
        {
            Time.frameCount++; GUI.Texts.Clear(); Event.current.type=EventType.Repaint;
            labels.PrepareFrame(camera,show,scale,offset); labels.Draw(scale,offset);
        }
        void Tick() { Time.unscaledTime+=1; labels.Tick(true,true); }
        labels.Tick(true,false); Render(false);
        Check(GUI.Texts.Count==0,"Hidden label drawn.");
        Check(local.View.Data.Writes==6 && character.View.Data.Writes==6,"Hidden displays stopped owner publication of protection/capacity/activity.");
        Tick(); Render();
        Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="49" && GUI.LastFontSize==20,"Other label/local exclusion differs.");
        Check(local.View.Data.GetFloat("Slikfoul.ImprovedMagicShield.Protection".GetStableHashCode(),-1)==300,"Excluded local player stopped publishing.");
        var localBar=new ShieldHud();
        foreach(bool showBar in new[]{true,false})
        {
            localBar.Tick(showBar); Render();
            Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="49","Own overhead label depends on the lower-bar toggle.");
        }
        localBar.Destroy();
        shield.Hit(30); Render(true,200);
        Check(GUI.Texts.Last()=="19" && GUI.LastFontSize==40,"Damage/scale did not update before tick.");
        Render(); float x=GUI.LastRect.x,y=GUI.LastRect.y;
        character.RenderedTop=new Vector3(5,4,0); character.PhysicsCenter=new Vector3(-100,100,0); Render();
        Check(GUI.LastRect.x==x+5 && GUI.LastRect.y==y-2 && character.PhysicsReads==0,"Label did not follow rendered anchor independently of physics and tick.");
        camera.transform.position=new Vector3(2,1,0); Render();
        Check(GUI.LastRect.x==x+3 && GUI.LastRect.y==y-1,"Projection missed current camera pose.");
        x=GUI.LastRect.x; int projections=camera.Projections;
        character.RenderedTop=new Vector3(12,4,0); camera.transform.position=new Vector3(9,1,0);
        GUI.Texts.Clear(); Event.current.type=EventType.Layout; labels.Draw(100);
        Check(GUI.Texts.Count==0 && camera.Projections==projections,"Layout rendered or reprojected label.");
        Event.current.type=EventType.Repaint; labels.Draw(100);
        Check(GUI.LastRect.x==x && camera.Projections==projections,"GUI changed the prepared pose.");
        Time.frameCount++; GUI.Texts.Clear(); labels.Draw(100);
        Check(GUI.Texts.Count==0,"Previous-frame position reused.");
        labels.PrepareFrame(new Camera(),true); labels.Draw(100);
        Check(GUI.Texts.Count==0,"Secondary camera supplied frame.");
        Render(); x=GUI.LastRect.x; labels.PrepareFrame(new Camera(),true); GUI.Texts.Clear(); labels.Draw(100);
        Check(GUI.Texts.Count==2 && GUI.LastRect.x==x,"Secondary camera replaced main-camera frame.");
        camera.pixelWidth=Screen.width/2; camera.pixelHeight=Screen.height/2; Render();
        Check(Math.Abs(GUI.LastRect.x+GUI.LastRect.width/2-206)<0.01f,"Projection did not scale to screen size.");
        camera.pixelWidth=Screen.width; camera.pixelHeight=Screen.height;
        // The visible native name/health block, rather than a guessed world offset,
        // owns label placement. Scaling the label must never grow back over it.
        EnemyHud.instance=new EnemyHud();
        var nativeRoot=new GameObject("Native character header",typeof(RectTransform));
        var nativeCanvas=nativeRoot.AddComponent<Canvas>();
        var nativeName=(RectTransform)new GameObject("Name",typeof(RectTransform)).transform;
        var nativeHealth=(RectTransform)new GameObject("Health",typeof(RectTransform)).transform;
        nativeName.SetParent(nativeRoot.transform,false); nativeHealth.SetParent(nativeRoot.transform,false);
        nativeName.TestBounds=new Bounds { min=new Vector3(80,850,0),max=new Vector3(120,880,0) };
        nativeHealth.TestBounds=new Bounds { min=new Vector3(70,840,0),max=new Vector3(130,850,0) };
        EnemyHud.instance.Set(character,nativeRoot); Render();
        Check(GUI.LastRect.y==156 && GUI.LastRect.x+GUI.LastRect.width/2==100,"Player protection does not sit above the native name.");
        var creature=new Character(); creature.Manager.Effect=shield;
        Character.All.Add(creature); EnemyHud.instance.Set(creature,nativeRoot); Tick(); Render();
        Check(GUI.LastRect.y==244,"Other creature protection no longer sits below the native name and health.");
        Character.All.Remove(creature); Tick();
        Render(true,100,20); Check(GUI.LastRect.y==136,"Positive label offset does not move up.");
        Render(true,100,-20); Check(GUI.LastRect.y==176,"Negative label offset does not move down.");
        nativeCanvas.scaleFactor=2; Render(true,300);
        Check(GUI.LastRect.y==72 && GUI.LastFontSize==60,"Large player label or canvas scale grows over the native name.");
        Render(true,100,20); Check(GUI.LastRect.y==112,"Manual label offset does not follow native canvas scaling.");
        nativeHealth.gameObject.SetActive(false); Render();
        Check(GUI.LastRect.y==152,"Hidden health bar changes above-name player placement.");
        nativeHealth.gameObject.SetActive(true); nativeName.gameObject.SetActive(false); Render();
        Check(GUI.LastRect.y>800,"Hidden player name does not use the rendered-character fallback.");
        nativeHealth.TestBounds=new Bounds { min=new Vector3(170,810,0),max=new Vector3(230,820,0) }; Render();
        Check(GUI.LastRect.y>800,"Moving health without a visible player name changes its fallback anchor.");
        nativeRoot.SetActive(false); Render();
        Check(GUI.LastRect.y>800 && GUI.Texts.Count==2,"Missing native header failed to use rendered-character fallback.");
        nativeRoot.SetActive(true); character.Boss=true; Render();
        Check(GUI.LastRect.y>800,"A global boss header incorrectly anchors a world label."); character.Boss=false;
        var replacedRoot=new GameObject("Recreated character header",typeof(RectTransform));
        var replacedName=(RectTransform)new GameObject("Name",typeof(RectTransform)).transform;
        replacedName.SetParent(replacedRoot.transform,false);
        replacedName.TestBounds=new Bounds { min=new Vector3(280,700,0),max=new Vector3(320,720,0) };
        EnemyHud.instance.Set(character,replacedRoot); Render();
        Check(GUI.LastRect.y==316 && GUI.LastRect.x+GUI.LastRect.width/2==300,"Recreated player header retains stale cached geometry.");
        var skeleton=new Character(); skeleton.gameObject.name="Skeleton_Friendly(Clone)"; skeleton.Manager.Effect=shield;
        Character.All.Add(skeleton); EnemyHud.instance.Set(skeleton,replacedRoot); Tick(); Render();
        Check(GUI.LastRect.y+GUI.LastRect.height==356 && GUI.LastRect.x+GUI.LastRect.width/2==300,"Summoned skeleton protection is not above its name.");
        Render(true,300); Check(GUI.LastRect.y+GUI.LastRect.height==356 && GUI.LastRect.height==120,"Large skeleton label grows back over its name.");
        Render(true,100,20); Check(GUI.LastRect.y+GUI.LastRect.height==336,"Skeleton label manual upward offset differs.");
        var nativeText=replacedName.gameObject.AddComponent<TMPro.TMP_Text>(); nativeText.fontSize=16;
        Render();
        var nativeLabel=replacedRoot.transform.Children.First(c=>c.gameObject.name=="ImprovedMagicShield.Protection").GetComponent<TMPro.TMP_Text>();
        Check(GUI.Texts.Count==0 && nativeLabel.text=="19" && nativeLabel.gameObject.activeSelf,"Native header caption was also drawn in the independent GUI pass.");
        Check(nativeLabel.transform.parent==replacedRoot.transform && nativeLabel.fontSize==20 && nativeText.fontSize==16,"Caption failed to share the header hierarchy or modified the native name.");
        float nativeY=nativeLabel.transform.localPosition.y;
        Render(true,100,20); Check(nativeLabel.transform.localPosition.y==nativeY+20,"Native caption offset differs.");
        Tick(); Render();
        Check(replacedRoot.transform.Children.Count(c=>c.gameObject.name=="ImprovedMagicShield.Protection" && !c.gameObject.Destroyed)==2,"Discovery tick recreated retained native labels.");
        // The player and skeleton deliberately share this fixture root: each has one label.
        Render(false); Check(!nativeLabel.gameObject.activeSelf,"Hidden native caption remains active.");
        Render(); Check(nativeLabel.gameObject.activeSelf,"Native caption did not recover after hiding.");
        nativeText.gameObject.Components.Remove(typeof(TMPro.TMP_Text)); Render();
        Check(!nativeLabel.gameObject.activeSelf && GUI.Texts.Count==0,"Native header transition jumped to an independent fallback label.");
        // Restore the earlier fallback fixture for the remaining network/lifecycle checks.
        var anchors=typeof(ShieldLabels).GetField("labels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(labels) as System.Collections.IEnumerable;
        foreach(var item in anchors) ((ShieldLabelAnchor)item.GetType().GetField("Anchor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(item)).Dispose();
        Character.All.Remove(skeleton); Tick();
        EnemyHud.instance=null;
        character.RenderedTop=new Vector3(100,2,0); Render(); Check(GUI.Texts.Count==0,"Distant label drawn.");
        character.RenderedTop=new Vector3(0,2,0); camera.transform.position=new Vector3();
        camera.Behind=true; Render(); Check(GUI.Texts.Count==0,"Behind-camera label drawn."); camera.Behind=false;
        character.Dead=true; Render(); Check(GUI.Texts.Count==0,"Dead label drawn."); character.Dead=false;
        character.Teleporting=true; Render(); Check(GUI.Texts.Count==0,"Teleporting label drawn."); character.Teleporting=false;
        character.View.Valid=false; Render(); Check(GUI.Texts.Count==0,"Invalid view label drawn."); character.View.Valid=true;
        character.Manager.Effect=null; Render(); Check(GUI.Texts.Count==0,"Removed effect left label before tick."); character.Manager.Effect=shield;
        foreach(int state in new[]{0,1,2,3})
        {
            local.Dead=state==0; local.Teleporting=state==1; local.Cutscene=state==2; Hud.instance.Visible=state!=3;
            Render(); Check(GUI.Texts.Count==0,"Local visibility lifecycle left labels.");
        }
        local.Dead=false; local.Teleporting=false; local.Cutscene=false; Hud.instance.Visible=true;
        Render(); Check(GUI.Texts.Count==2,"Labels did not recover after hide.");
        Player.m_localPlayer=character; Render(); Check(GUI.Texts.Count==0,"Changed local player retained own label."); Player.m_localPlayer=local;
        ZNet.instance.Now+=0.5; Tick(); character.View.Owner=false; Render();
        Check(GUI.Texts.Last()=="19","Remote snapshot not read after ownership transfer.");
        character.View.Data.Owner=11; Render(); Check(GUI.Texts.Count==0,"Old owner accepted between ticks.");
        character.View.Data.Owner=10; Render(); Check(GUI.Texts.Count==2,"Fresh remote snapshot rejected.");
        character.View.Data.Owner=11; GUI.Texts.Clear(); labels.Draw(100);
        Check(GUI.Texts.Count==0,"Owner change after projection retained label."); character.View.Data.Owner=10;
        ZNet.instance.Now+=3; Render(); Check(GUI.Texts.Count==0,"Expired snapshot drawn between ticks.");
        character.View.Owner=true; Tick(); Render(false); GUI.Texts.Clear(); labels.Draw(100);
        Check(GUI.Texts.Count==0,"Hidden labels retained prepared frame.");
        labels.Tick(false,true); Render();
        Check(GUI.Texts.Count==0 && character.Manager.Effect==shield && shield.Absorbed==480.1f,"Disabled labels changed active shield.");
        Check(character.View.Data.GetFloat("Slikfoul.ImprovedMagicShield.Protection".GetStableHashCode(),-1)==0,"Disabled owner left advertised shield.");
        Tick(); Render(); labels.Clear(); GUI.Texts.Clear(); labels.Draw(100);
        Check(GUI.Texts.Count==0,"Cleanup retained label.");
        Character.All.Clear(); Player.m_localPlayer=null; Hud.instance=null;
    }

    private static void InstalledIlChecks(string gamePath,string modPath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var mod=AssemblyDefinition.ReadAssembly(modPath);
        TypeDefinition Type(string name) => game.MainModule.Types.Single(t=>t.Name==name);
        var character=Type("Character");
        var rpc=character.Methods.Single(m=>m.Name=="RPC_Damage");
        var status=Type("StatusEffect");
        var shield=Type("SE_Shield");
        Check(shield.Fields.Any(f=>f.Name=="m_totalAbsorbDamage" && f.IsPrivate && f.FieldType.FullName=="System.Single")
            && shield.Fields.Any(f=>f.Name=="m_damage" && f.IsPrivate && f.FieldType.FullName=="System.Single"),"Installed shield fields differ.");
        Check(status.Methods.Single(m=>m.Name=="SetAttacker").Body.Instructions.All(i=>i.OpCode.Code==Code.Ret),"Vanilla source semantics changed; re-review.");
        var setLevel=shield.Methods.Single(m=>m.Name=="SetLevel");
        // Execute the installed game's numeric IL, stopping before diagnostic formatting.
        foreach (int world in new[] {-1,0,1,3})
        foreach (float skill in new[] {0f,1f,42.5f,100f})
        foreach (int item in new[] {1,4})
        {
            Game.m_worldLevel=world; var state=new SE_Shield(500,200);
            InterpretNumericSetLevel(setLevel,state,item,skill);
            Check(state.Total==ShieldRefresh.FullProtection(state,skill) && state.m_ttl==60*item && state.Absorbed==200,"Installed SetLevel IL differs from protection formula.");
        }
        Game.m_worldLevel=0;
        Check(rpc.Body.Instructions.TakeWhile(i=>!(i.Operand is MethodReference m && m.Name=="ResetTime"))
            .Any(i=>i.Operand is MethodReference m && m.Name=="IsOwner"),"Damage refresh no longer follows ownership check.");
        var manager=Type("SEMan");
        var publicHash=manager.Methods.Single(m=>m.Name=="AddStatusEffect" && m.Parameters[0].ParameterType.FullName=="System.Int32");
        var rpcAdd=manager.Methods.Single(m=>m.Name=="RPC_AddStatusEffect");
        foreach (var method in new[] {publicHash,rpcAdd})
            Check(method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Internal_AddStatusEffect")
                && method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="IsOwner"),"SEMan ownership/RPC convergence changed.");
        var modPlugin=mod.MainModule.Types.Single(t=>t.Name=="Plugin");
        Check(mod.Name.Version.ToString(3)=="1.2.2","Built DLL version differs.");
        var awake=modPlugin.Methods.Single(m=>m.Name=="Awake").Body.Instructions;
        int showIndex=awake.ToList().FindIndex(i=>Equals(i.Operand,"Show remaining protection"));
        int enabledIndex=awake.ToList().FindIndex(i=>Equals(i.Operand,"Enabled"));
        int hudIndex=awake.ToList().FindIndex(i=>Equals(i.Operand,"Show local shield bar"));
        int outlineIndex=awake.ToList().FindIndex(i=>Equals(i.Operand,"Show shield health outline"));
        Check(outlineIndex>=0 && awake[outlineIndex+1].OpCode.Code==Code.Ldc_I4_0,"Health outline must have its own off-by-default setting.");
        Check(showIndex>=0 && awake[showIndex+1].OpCode.Code==Code.Ldc_I4_0,"Label default must be off.");
        Check(enabledIndex>=0 && awake[enabledIndex+1].OpCode.Code==Code.Ldc_I4_1,"Mechanics default must be on.");
        Check(hudIndex>=0 && awake[hudIndex+1].OpCode.Code==Code.Ldc_I4_1,"Local HUD default must be on.");
        var top=character.Methods.Single(m=>m.Name=="GetTopPoint").Body.Instructions;
        Check(top.Any(i=>i.Operand is MethodReference m && m.Name=="TransformPoint")
            && !top.Any(i=>i.Operand is MethodReference m && m.Name=="get_bounds"),"Installed visual anchor changed to physics bounds.");
        var cameraLate=Type("GameCamera").Methods.Single(m=>m.Name=="LateUpdate").Body.Instructions;
        Check(cameraLate.Any(i=>i.Operand is MethodReference m && m.Name=="UpdateCamera"),"Installed camera update stage changed.");
        var renderer=mod.MainModule.Types.Single(t=>t.Name=="ShieldLabels");
        var prepare=renderer.Methods.Single(m=>m.Name=="PrepareFrame").Body.Instructions;
        var draw=renderer.Methods.Single(m=>m.Name=="Draw").Body.Instructions;
        Check(prepare.Any(i=>i.Operand is MethodReference m && m.Name=="GetTopPoint")
            && prepare.Any(i=>i.Operand is MethodReference m && m.Name=="WorldToScreenPointScaled")
            && !renderer.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name=="GetCenterPoint"),"Compiled labels do not use the rendered anchor/native screen projection.");
        Check(!draw.Any(i=>i.Operand is MethodReference m && (m.Name=="GetTopPoint" || m.Name.Contains("WorldToScreenPoint"))),"GUI projects a different pose from the camera frame.");
        var destroy=modPlugin.Methods.Single(m=>m.Name=="OnDestroy").Body.Instructions;
        Check(awake.Any(i=>i.Operand is FieldReference f && f.Name=="onPreCull")
            && destroy.Any(i=>i.Operand is FieldReference f && f.Name=="onPreCull")
            && destroy.Any(i=>i.Operand is MethodReference m && m.Name=="Remove" && m.DeclaringType.Name=="Delegate"),"Camera callback subscription/cleanup missing.");
        Check(modPlugin.Methods.Single(m=>m.Name=="PrepareLabels").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="PrepareFrame"),"Camera callback does not prepare labels.");
        var dynamic=new DynamicMethod("InstalledBlock",typeof(void),new[] {typeof(StatusEffect),typeof(HitData),typeof(Character)},typeof(Program).Module,true);
        var generator=dynamic.GetILGenerator();
        var locals=rpc.Body.Variables.ToDictionary(v=>v.Index,v=>generator.DeclareLocal(v.VariableType.FullName=="StatusEffect" ? typeof(StatusEffect) : typeof(object)));
        var opcodeMap=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(System.Reflection.Emit.OpCode))
            .Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(o=>o.Name);
        var labels=rpc.Body.Instructions.ToDictionary(i=>i,i=>generator.DefineLabel());
        var code=new List<CodeInstruction>();
        foreach (var instruction in rpc.Body.Instructions)
        {
            object operand=instruction.Operand;
            if (operand is VariableDefinition v) operand=locals[v.Index];
            if (operand is MethodReference m && m.DeclaringType.Name=="StatusEffect") operand=typeof(StatusEffect).GetMethod(m.Name);
            if (operand is FieldReference f && f.DeclaringType.Name=="HitData") operand=typeof(HitData).GetField(f.Name);
            var next=new CodeInstruction(opcodeMap[instruction.OpCode.Name],operand);
            if (rpc.Body.Instructions.Any(i=>i.Operand==instruction || i.Operand is Instruction[] targets && targets.Contains(instruction))) next.labels.Add(labels[instruction]);
            code.Add(next);
        }
        int start=code.FindIndex(i=>i.Calls(typeof(StatusEffect).GetMethod("ResetTime")))-1;
        var transformed=HitShieldHook.Transpiler(code).ToList();
        Check(transformed.Count==code.Count-4,"Unexpected patch size.");
        Check(transformed.Count(i=>i.Calls(typeof(ShieldRefresh).GetMethod("RefreshFromHit",BindingFlags.Static|BindingFlags.NonPublic)))==1,"Refresh helper was not inserted exactly once.");
        Check(!transformed.Any(i=>i.Calls(typeof(StatusEffect).GetMethod("ResetTime")))
            && transformed.Count(i=>i.Calls(typeof(StatusEffect).GetMethod("SetAttacker")))==1,"Original partial refresh/source path remains incorrectly patched.");
        Check(transformed[start].labels.SequenceEqual(code[start].labels),"Refresh entry label was lost.");
        // Execute the actual transformed block, mapped from Character's local variable.
        LocalBuilder effectLocal=(LocalBuilder)transformed[start].operand;
        generator.Emit(OpCodes.Ldarg_0); generator.Emit(OpCodes.Stloc,effectLocal);
        foreach (var instruction in transformed.Skip(start).Take(4))
        {
            if (instruction.opcode==OpCodes.Ldarg_2) generator.Emit(OpCodes.Ldarg_1);
            else if (instruction.operand is LocalBuilder local) generator.Emit(instruction.opcode,local);
            else generator.Emit(instruction.opcode,(MethodInfo)instruction.operand);
        }
        var skip=generator.DefineLabel();
        generator.Emit(OpCodes.Ldloc,effectLocal); generator.Emit(OpCodes.Brfalse,skip);
        generator.Emit(OpCodes.Ldloc,effectLocal); generator.Emit(OpCodes.Ldarg_2); generator.Emit(OpCodes.Callvirt,typeof(StatusEffect).GetMethod("SetAttacker"));
        generator.MarkLabel(skip); generator.Emit(OpCodes.Ret);
        var apply=(Action<StatusEffect,HitData,Character>)dynamic.CreateDelegate(typeof(Action<StatusEffect,HitData,Character>));
        foreach (bool accept in new[] {false,true})
        {
            var original=new Character(); var incoming=new Character(); var active=new SE_Shield(500,accept?450:200) { Source=original };
            apply(active,new HitData { m_itemLevel=3 },incoming);
            Check(active.Source==(accept?incoming:original),"Emitted block changed rejected source or lost accepted source.");
            Check(accept ? active.Absorbed==0 && active.Time==0 : active.Absorbed==200 && active.Time==25,"Emitted block changed rejected state.");
        }
        bool failed=false; try { HitShieldHook.Transpiler(Array.Empty<CodeInstruction>()).ToList(); } catch (InvalidOperationException) { failed=true; }
        Check(failed,"Incompatible hook did not fail closed.");
        var changed=code.Select(c=>new CodeInstruction(c)).ToList(); changed[start+2].labels.Add(generator.DefineLabel()); failed=false;
        try { HitShieldHook.Transpiler(changed).ToList(); } catch (InvalidOperationException) { failed=true; }
        Check(failed,"Foreign control flow was silently removed.");
    }

    private static void InterpretNumericSetLevel(MethodDefinition method,SE_Shield state,int item,float skill)
    {
        var stack=new Stack<object>(); var instructions=method.Body.Instructions;
        for (int p=0;p<instructions.Count;p++)
        {
            var i=instructions[p];
            switch (i.OpCode.Code)
            {
                case Code.Ldarg_0: stack.Push(state); break;
                case Code.Ldarg_1: stack.Push(item); break;
                case Code.Ldarg_2: stack.Push(skill); break;
                case Code.Ldc_I4_0: stack.Push(0); break;
                case Code.Ldfld:
                    var target=stack.Pop(); var field=target.GetType().GetField(((FieldReference)i.Operand).Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    stack.Push(field.GetValue(target)); break;
                case Code.Ldsfld:
                    if (((FieldReference)i.Operand).FullName!="System.Int32 Game::m_worldLevel") throw new Exception("Unexpected static field in installed SetLevel.");
                    stack.Push(Game.m_worldLevel); break;
                case Code.Stfld:
                    object value=stack.Pop(); target=stack.Pop(); field=target.GetType().GetField(((FieldReference)i.Operand).Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    field.SetValue(target,value); break;
                case Code.Conv_R4: stack.Push(Convert.ToSingle(stack.Pop())); break;
                case Code.Mul:
                    object right=stack.Pop(),left=stack.Pop();
                    if (right is int ri && left is int li) stack.Push(ri*li); else stack.Push(Convert.ToSingle(left)*Convert.ToSingle(right)); break;
                case Code.Add: stack.Push(Convert.ToSingle(stack.Pop())+Convert.ToSingle(stack.Pop())); break;
                case Code.Ble_S:
                    right=stack.Pop(); left=stack.Pop(); if (Convert.ToDouble(left)<=Convert.ToDouble(right)) p=instructions.IndexOf((Instruction)i.Operand)-1; break;
                case Code.Ldstr: return;
                default: throw new Exception("Review changed installed SetLevel IL: "+i);
            }
        }
        throw new Exception("Installed SetLevel did not reach expected diagnostic block.");
    }

    private static void HudChecks()
    {
        var shield=new SE_Shield(500,250);
        Check(HudPolicy.TryRead(shield,out float remaining,out float capacity) && remaining==250 && capacity==500,"HUD read wrong current values.");
        Check(HudPolicy.Text(remaining,capacity)=="250 / 500" && HudPolicy.Fraction(remaining,capacity)==0.5f,"HUD text/fill mismatch.");
        shield.Hit(240); ShieldRefresh.TryRefresh(shield,2,0);
        Check(HudPolicy.TryRead(shield,out remaining,out capacity) && HudPolicy.Text(remaining,capacity)=="100 / 100"
            && HudPolicy.Fraction(remaining,capacity)==1,"HUD retained previous shield's maximum.");
        shield.Hit(100);
        Check(!HudPolicy.TryRead(shield,out _,out _),"Exhausted shield HUD remains visible.");
        var expired=new SE_Shield(500,200) { Time=61 };
        Check(!HudPolicy.TryRead(expired,out _,out _) && !HudPolicy.TryRead(null,out _,out _),"Expired/absent shield HUD remains visible.");
        Check(HudPolicy.Fraction(1,0)==0 && HudPolicy.Fraction(600,500)==1,"HUD fill is not bounded.");
        Check(HudPolicy.Width(200,50)==128 && HudPolicy.Width(700,50)==448,"Full shield width does not follow the requested protection scale.");
        Check(Math.Abs(HudPolicy.Width(365,50)-233.6f)<0.01f,"Intermediate shield capacity was quantized to whole segments.");
        Check(HudPolicy.Width(100,50)==64,"Text width clamps small shield geometry.");
        foreach(float full in new[]{100f,200f,365f,700f})
        {
            float previous=float.PositiveInfinity;
            for(int segment=10;segment<=500;segment++)
            {
                float width=HudPolicy.Width(full,segment);
                Check(width<previous && Math.Abs(width-full/segment*32f)<0.001f,"Protection per segment plateaus: "+full+"/"+segment);
                previous=width;
            }
        }
    }

    private static void HudLifecycleChecks()
    {
        var hud=new Hud(); Hud.instance=hud;
        hud.m_staminaBar2Root.TestBounds=new Bounds { center=new Vector3(20,-400,0),max=new Vector3(150,-385,0) };
        hud.m_eitrBarRoot.TestBounds=new Bounds { max=new Vector3(150,-350,0) };
        hud.m_adrenalineBarRoot.TestBounds=new Bounds { max=new Vector3(150,-320,0) };
        var player=new Player(); Player.m_localPlayer=player; var shield=new SE_Shield(500,250); player.Manager.Effect=shield;
        var bar=new ShieldHud(); bar.Tick(true);
        var root=(RectTransform)hud.m_rootObject.transform.Children.Single(c=>c.gameObject.name=="ImprovedMagicShield.ResourceBar");
        Check(root.gameObject.activeSelf && root.parent==hud.m_rootObject.transform,"HUD is not attached to native canvas.");
        Check(root.anchoredPosition.x==0 && root.anchoredPosition.y==192,"HUD does not occupy its fixed bottom-center row.");
        Check(root.pivot.x==0.5f && root.pivot.y==0.5f,"HUD pivot is not stable while changing bar width.");
        var artwork=(RectTransform)root.GetChild(0);
        Check(artwork.anchoredPosition.x==0 && artwork.anchoredPosition.y==0 && artwork.localScale.x==1 && artwork.localScale.y==1,"HUD inherited an animated native resource pose.");
        Check(root.rect.height==32 && root.rect.width==336,"HUD did not retain native thickness with capacity-based width.");
        var fastField=typeof(ShieldHud).GetField("fast",BindingFlags.Instance|BindingFlags.NonPublic);
        var slowField=typeof(ShieldHud).GetField("slow",BindingFlags.Instance|BindingFlags.NonPublic);
        var fast=(GuiBar)fastField.GetValue(bar); var slow=(GuiBar)slowField.GetValue(bar);
        Check(fast.Value==0.5f && slow.Value==0.5f && fast.Width==320 && slow.Width==320,"Native GuiBar values/width do not match the shield.");
        Check(fast!=hud.m_eitrBarFast && fast.m_bar!=hud.m_eitrBarFast.m_bar && hud.m_eitrBarFast.Value==1,"Cloned bar altered native Eitr references or values.");
        var fillImage=fast.m_bar.GetComponent<UnityEngine.UI.Image>();
        Check(fillImage.sprite==hud.m_eitrBarFast.m_bar.GetComponent<UnityEngine.UI.Image>().sprite && fillImage.type==UnityEngine.UI.Image.Type.Tiled,"Native fill sprite or tiling was replaced.");
        Check(root.GetComponentsInChildren<Animator>(true).Length==0,"Native panel Animator was copied.");
        Check(root.GetComponentsInChildren<NativeResourceUpdater>(true).All(c=>!c.enabled)
            && hud.m_eitrBarRoot.GetComponentsInChildren<NativeResourceUpdater>(true).All(c=>c.enabled),"Resource updater was not isolated to the clone.");
        Check(root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget),"Shield visuals intercept native HUD input.");
        var text=typeof(ShieldHud).GetField("text",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(((TMPro.TMP_Text)text.GetValue(bar)).text=="250 / 500","Native HUD current/max text differs.");
        var clonedText=(TMPro.TMP_Text)text.GetValue(bar);
        Check(clonedText.font==hud.m_eitrText.font && clonedText.fontSharedMaterial==hud.m_eitrText.fontSharedMaterial
            && clonedText.fontSize==hud.m_eitrText.fontSize,"Native typography was replaced.");
        hud.m_staminaBar2Root.sizeDelta=new Vector2(900,32); hud.m_eitrBarRoot.sizeDelta=new Vector2(1000,32); bar.Tick(true);
        Check(fast.Width==320 && root.rect.width==336,"Other resource maxima changed the shield's overall width.");
        foreach(bool shown in new[]{false,true,false,true})
        {
            hud.m_eitrBarRoot.gameObject.SetActive(shown); hud.m_adrenalineBarRoot.gameObject.SetActive(!shown);
            hud.m_eitrBarRoot.TestBounds=new Bounds { min=new Vector3(-200,-900,0),max=new Vector3(300,50,0) };
            hud.m_staminaBar2Root.anchoredPosition=new Vector2(180,shown?320:130);
            hud.m_staminaBar2Root.TestBounds=new Bounds { center=new Vector3(170,shown?320:130,0),max=new Vector3(1200,400,0) };
            bar.Tick(true);
            Check(root.anchoredPosition.x==0 && root.anchoredPosition.y==192 && artwork.anchoredPosition.y==0,"Another resource's visibility, animation or layout moved the shield row.");
        }
        shield.Hit(1); bar.Tick(true);
        Check(fast.Width==320 && root.rect.width==336 && Math.Abs(fast.Value-0.498f)<0.0001f,"Damage resized the frame instead of changing only its fill.");
        foreach(var example in new[]{(200f,128f),(365f,233.6f),(700f,448f)})
        {
            player.Manager.Effect=new SE_Shield(example.Item1,0); bar.Tick(true);
            Check(Math.Abs(fast.Width-example.Item2)<0.01f && Math.Abs(root.rect.width-(example.Item2+16))<0.01f && fast.Value==1 && slow.Value==1,"A full applied shield did not set its own length and full fill: "+example.Item1);
        }
        player.Manager.Effect=shield; bar.Tick(true,100);
        Check(fast.Width==160 && root.rect.width==176,"Live protection-per-segment change failed.");
        bar.Tick(true);
        Check(fast.Width==320,"Default segment scale did not restore the capacity-based width.");
        foreach(int segment in new[]{72,73,100,200,500})
        {
            bar.Tick(true,segment);
            Check(Math.Abs(fast.Width-500f/segment*32f)<0.001f && Math.Abs(root.rect.width-fast.Width-16)<0.001f,"Rendered frame plateaued at "+segment);
            Check(clonedText.rectTransform.rect.width>=clonedText.GetPreferredValues("500 / 500").x && clonedText.textWrappingMode==TMPro.TextWrappingModes.NoWrap && !clonedText.enableAutoSizing,"Short bar clips or shrinks its caption.");
        }
        bar.Tick(false); Check(!root.gameObject.activeSelf && player.Manager.Effect==shield,"HUD toggle changed shield state.");
        bar.Tick(true); Check(root.gameObject.activeSelf,"HUD did not reappear after enabling.");
        shield.Hit(240); ShieldRefresh.TryRefresh(shield,2,0); bar.Tick(true);
        Check(((TMPro.TMP_Text)text.GetValue(bar)).text=="100 / 100" && fast.Value==1 && slow.Value==1 && fast.Width==64 && clonedText.rectTransform.rect.width>fast.Width,"Native HUD retained old maximum/length or clamped small-shield geometry to caption size.");
        player.Dead=true; bar.Tick(true); Check(!root.gameObject.activeSelf,"HUD remains visible after death.");
        player.Dead=false; player.Teleporting=true; bar.Tick(true); Check(!root.gameObject.activeSelf,"HUD remains visible during teleport.");
        player.Teleporting=false; hud.Visible=false; bar.Tick(true); Check(!root.gameObject.activeSelf,"HUD ignores vanilla visibility.");
        hud.Visible=true; player.Manager.Effect=null; bar.Tick(true); Check(!root.gameObject.activeSelf,"HUD remains visible without a shield.");
        player.Manager.Effect=shield; bar.Tick(true); Check(root.gameObject.activeSelf,"HUD failed to recover after a new shield.");
        Hud.instance=new Hud(); bar.Tick(true); Check(root.gameObject.Destroyed,"HUD recreation retained old elements.");
        Player.m_localPlayer=null; bar.Tick(true);
        var secondRoot=Hud.instance.m_rootObject.transform.Children.Single(c=>c.gameObject.name=="ImprovedMagicShield.ResourceBar");
        Check(!secondRoot.gameObject.activeSelf,"Menu/no-player state retains shield HUD.");
        bar.Destroy(); Check(secondRoot.gameObject.Destroyed,"Plugin destruction retains shield HUD.");
        Hud.instance=null;
    }

    private static void HudVisibilityChecks()
    {
        var hud=new Hud(); Hud.instance=hud;
        var player=new Player(); Player.m_localPlayer=player;
        var shield=new SE_Shield(500,0) { Time=0, m_ttl=600, m_absorbDamage=500,m_absorbDamagePerSkillLevel=0 };
        player.Manager.Effect=shield;
        var bar=new ShieldHud(); bar.Tick(true);
        var root=(RectTransform)hud.m_rootObject.transform.Children.Single(c=>c.gameObject.name=="ImprovedMagicShield.ResourceBar");
        var alpha=root.GetComponent<CanvasGroup>();
        Check(root.gameObject.activeSelf && alpha.alpha==1 && !alpha.interactable && !alpha.blocksRaycasts,"Fresh full shield is not visible and input-transparent.");
        shield.Time=5; bar.Tick(true); Check(alpha.alpha==1 && root.gameObject.activeSelf,"Full shield hides before five seconds.");
        shield.Time=5.25f; bar.Tick(true); Check(alpha.alpha==0.5f && root.gameObject.activeSelf,"Full shield does not fade smoothly after five seconds.");
        shield.Time=5.5f; bar.Tick(true); Check(alpha.alpha==0 && !root.gameObject.activeSelf,"Full shield remains visible after its fade.");
        player.Blocking=true;
        shield.Time=6; bar.Tick(true); Check(alpha.alpha==0 && !root.gameObject.activeSelf,"Right-click blocking alone reveals a hidden bar.");
        shield.Time=7; bar.Tick(true); Check(alpha.alpha==0 && !root.gameObject.activeSelf,"Holding right-click prolongs the bar's display period.");
        player.Blocking=false;
        // Reject a weaker full cast without reviving a hidden bar or resetting its age.
        shield.m_absorbDamage=100; shield.Time=8;
        Check(!ShieldRefresh.TryRefresh(shield,1,0),"Weaker full cast unexpectedly accepted.");
        bar.Tick(true); Check(alpha.alpha==0 && !root.gameObject.activeSelf && shield.Time==8,"Rejected cast revived the hidden full shield bar.");
        shield.Hit(1); bar.Tick(true); Check(alpha.alpha==1 && root.gameObject.activeSelf,"Damage did not immediately reveal a hidden full shield.");
        shield.Time=13; bar.Tick(true); Check(alpha.alpha==1 && root.gameObject.activeSelf,"Damaged shield hides before its quiet period ends.");
        shield.Time=13.25f; bar.Tick(true); Check(alpha.alpha==0.5f,"Damaged shield does not fade outside combat.");
        shield.Time=13.5f; bar.Tick(true); Check(!root.gameObject.activeSelf,"Damaged shield remains visible outside combat.");
        player.Targeted=player.Attacking=player.DrawingBow=player.Blocking=true;
        shield.Time+=1; bar.Tick(true);
        Check(!root.gameObject.activeSelf,"Input state or generic sensed-target state reveals a quiet shield bar.");
        ShieldThreatHook.Postfix(player,false); shield.Time+=1; bar.Tick(true);
        Check(!root.gameObject.activeSelf,"Non-alerted enemy detection reveals the shield bar.");
        ShieldThreatHook.Postfix(new Player(),true); shield.Time+=1; bar.Tick(true);
        Check(!root.gameObject.activeSelf,"Another player's threat notification reveals the local shield bar.");
        for(int report=0;report<3;report++)
        {
            ShieldThreatHook.Postfix(player,true); shield.Time+=2; bar.Tick(true);
            Check(root.gameObject.activeSelf && alpha.alpha==1,"An alerted enemy targeting the local player does not reveal/maintain the shield bar.");
        }
        shield.Time+=5.25f; bar.Tick(true);
        Check(alpha.alpha==0.5f && root.gameObject.activeSelf,"The last real enemy notification does not start the quiet-period fade.");
        shield.Time+=0.25f; bar.Tick(true);
        Check(!root.gameObject.activeSelf,"Holding attack/block/bow inputs prevents hiding after enemy targeting stops.");
        Plugin.IsEnabled=false; ShieldThreatHook.Postfix(player,true); Plugin.IsEnabled=true;
        shield.Time+=1; bar.Tick(true);
        Check(!root.gameObject.activeSelf,"Disabled mod records enemy notifications.");
        player.Targeted=player.Attacking=player.DrawingBow=player.Blocking=false;
        shield.Time+=1; shield.Hit(1); bar.Tick(true);
        Check(root.gameObject.activeSelf && alpha.alpha==1,"A second damage event did not reveal the shield.");
        shield.m_absorbDamage=500; Check(ShieldRefresh.TryRefresh(shield,1,0),"Restoring cast rejected.");
        bar.Tick(true); Check(alpha.alpha==1 && root.gameObject.activeSelf && shield.Time==0,"Accepted cast did not start a fresh full-shield display period.");
        shield.Time=6; bar.Tick(true); Check(!root.gameObject.activeSelf,"Refreshed full shield does not hide again.");
        Check(ShieldRefresh.TryRefresh(shield,1,0),"Equal full-to-full cast rejected.");
        bar.Tick(true); Check(root.gameObject.activeSelf && alpha.alpha==1,"Full-to-full recast of the same effect did not reveal the bar.");
        player.Manager.Effect=null; bar.Tick(true); Check(!root.gameObject.activeSelf,"Removing the shield leaves its fade visible.");
        player.Manager.Effect=new SE_Shield(200,0) { Time=0 }; bar.Tick(true);
        Check(root.gameObject.activeSelf && alpha.alpha==1,"A new shield inherits the old hidden state.");
        bar.Tick(false); Check(!root.gameObject.activeSelf,"Display toggle does not override an active fade.");
        bar.Destroy(); Hud.instance=null; Player.m_localPlayer=null;
    }

    private static void ActivityDisplayChecks()
    {
        ShieldActivity.Clear();
        var viewer=new Player(); viewer.Manager.Effect=new SE_Shield(400,0) { Time=0,m_ttl=1000 };
        var recipient=new Player(); var shield=new SE_Shield(500,200) { Time=0,m_ttl=1000,m_absorbDamage=500,m_absorbDamagePerSkillLevel=0 }; recipient.Manager.Effect=shield;
        var skeleton=new Character(); skeleton.gameObject.name="Skeleton_Friendly(Clone)";
        skeleton.AI=new BaseAI(); var skeletonShield=new SE_Shield(700,0) { Time=0,m_ttl=1000 }; skeleton.Manager.Effect=skeletonShield;
        Character.All.Clear(); Character.All.Add(recipient); Character.All.Add(skeleton); Character.All.Add(viewer);
        Hud.instance=new Hud(); EnemyHud.instance=null; Camera.main=new Camera(); ZNet.instance=new ZNet();
        var ownerLabels=new ShieldLabels(); var viewerLabels=new ShieldLabels();
        void Publish(float age,float recipientAge=-1)
        {
            shield.Time=recipientAge<0?age:recipientAge; skeletonShield.Time=age; ZNet.instance.Now=1000+age;
            Player.m_localPlayer=recipient; recipient.View.Owner=skeleton.View.Owner=true; viewer.View.Owner=false;
            Time.unscaledTime+=1; ownerLabels.Tick(true,false);
            Player.m_localPlayer=viewer; recipient.View.Owner=skeleton.View.Owner=false; viewer.View.Owner=true;
            Time.unscaledTime+=1; viewerLabels.Tick(true,true);
        }
        void Render()
        {
            Time.frameCount++; GUI.Texts.Clear(); GUI.color=Color.white; Event.current.type=EventType.Repaint;
            viewerLabels.PrepareFrame(Camera.main,true); viewerLabels.Draw(100);
        }
        Publish(0); Render();
        Check(GUI.Texts.Count==4 && GUI.Texts.Contains("300") && GUI.Texts.Contains("700"),"Fresh remote player/skeleton shields do not appear.");
        Publish(10); Render(); Check(GUI.Texts.Count==0,"Quiet remote player or skeleton remains visible.");
        ShieldThreatHook.Postfix(viewer,true); Publish(11); Render();
        Check(GUI.Texts.Count==0,"The viewer entering combat reveals someone else's shield.");
        var ownBar=new ShieldHud(); ownBar.Tick(true);
        var ownRoot=Hud.instance.m_rootObject.transform.Children.Single(c=>c.gameObject.name=="ImprovedMagicShield.ResourceBar");
        Check(ownRoot.gameObject.activeSelf && ownRoot.GetComponent<CanvasGroup>().alpha==1,"The viewer's own bar does not share its own activity.");
        ownBar.Destroy(); Render(); Check(GUI.Texts.Count==0,"Destroying the local bar resets other characters' activity.");
        Player.m_localPlayer=recipient; ShieldThreatHook.Postfix(recipient,true); Publish(12); Render();
        Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="300","Recipient combat fails to reveal only that player's remote label.");
        skeleton.AI.Target=true; Publish(13); Render();
        Check(GUI.Texts.Count==2 && !GUI.Texts.Contains("700"),"Non-alerted skeleton target reveals its label.");
        skeleton.AI.Alerted=true; Publish(14); Render();
        Check(GUI.Texts.Count==4 && GUI.Texts.Contains("700"),"Skeleton's own combat does not reveal its shield.");
        skeleton.AI.Alerted=skeleton.AI.Target=false; Publish(19.25f); Render();
        Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="700" && Math.Abs(GUI.LastColor.a-0.5f)<0.002f,"Remote overhead text does not share the five-second/half-second fade.");
        Publish(19.5f); Render(); Check(GUI.Texts.Count==0,"Remote overhead label does not finish fading.");
        shield.Hit(10); Publish(20); Render();
        Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="290","Remote shield damage does not reveal the current remaining protection.");
        Check(ShieldRefresh.TryRefresh(shield,1,0),"Remote recast fixture rejected.");
        Publish(21,0); Render(); Check(GUI.Texts.Count==2 && GUI.Texts.Last()=="500","Remote accepted recast does not reveal full protection.");
        recipient.View.Data.Set("Slikfoul.ImprovedMagicShield.ActivityTime".GetStableHashCode(),-1L); Render();
        Check(GUI.Texts.Count==0,"Missing owner activity is replaced by the viewer's combat state.");
        Check(DisplayPolicy.SnapshotAlpha(-0.001,100)==0 && DisplayPolicy.SnapshotAlpha(103,100)==0
            && DisplayPolicy.SnapshotAlpha(100,105.25)==0.5f,"Missing/future activity or fractional fade differs.");
        ownerLabels.Clear(); viewerLabels.Clear(); Character.All.Clear(); Player.m_localPlayer=null; Hud.instance=null;
    }
}
