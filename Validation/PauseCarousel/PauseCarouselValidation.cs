using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Via.Application;
using Via.Persistence;
using Via.Story.Domain;
using Via.UI;

// Copy into an isolated project's Assets/Editor, then execute PauseCarouselValidation.Run.
[InitializeOnLoad]
public static class PauseCarouselValidation
{
    [Serializable] public class Report
    {
        public string result, unityVersion;
        public bool twoLensesWhenSettled, oneActionWhenSettled, transitionActionsDisabled,
            transitionObjectiveSlotsBounded, boundedTransitionProgress, noTransitionTail,
            fiveActions, rapidClicks, immediateRedirection, save,
            settingsReturn, titleCancel, quitCancel, resume, dimmedGameBackdrop, gameFrozen, textStyle;
        public int observedFrames;
        public List<string> resolutions = new List<string>();
    }
    static readonly string[] Names = { "pause-resume", "pause-save", "pause-settings", "pause-title", "pause-quit" };
    static readonly Vector2Int[] Sizes = { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1440) };
    static Report report = new Report();
    static UIDocument doc;
    static GameController game;
    static MicroscopeMenuController carousel;
    static RenderTexture target;
    static int phase, sizeIndex, minFrame, expectedIndex;
    static double began, readyAt, frozenTime;
    static bool watching;
    static double TurnWait => (float)Get(carousel,"turnDuration");
    static string Output => Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "UIExports", "PauseCarousel_20261023");
    static PauseCarouselValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PauseCarousel.Active",false)) return;
            began = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.Delete(Path.Combine(Output,"failure.txt"));
        File.Delete(Path.Combine(Output,"report.json"));
        EditorSceneManager.OpenScene("Assets/Via/Scenes/GameFramework.unity",OpenSceneMode.Single);
        var d = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
        AssetDatabase.DeleteAsset("Assets/PauseCarouselTarget.renderTexture");
        var rt = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
        AssetDatabase.CreateAsset(rt,"Assets/PauseCarouselTarget.renderTexture");
        d.panelSettings.targetTexture = rt;
        AssetDatabase.SaveAssets();
        SessionState.SetBool("PauseCarousel.Active",true);
        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        try
        {
            double timeout = carousel == null ? 240d : Math.Max(120d, TurnWait * 30d + 60d);
            if (EditorApplication.timeSinceStartup - began > timeout) throw new TimeoutException("Phase " + phase);
            if (doc == null)
            {
                var d = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
                if (d == null || !d.GetComponent<LoadingIntroController>().IsCompleted) return;
                doc = d; game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                carousel = d.GetComponent<MicroscopeMenuController>(); target = d.panelSettings.targetTexture;
                Require(carousel.enabled && (bool)Get(carousel,"initialized"),"Carousel startup");
                CheckTransitionTiming();
                // Saving exercises the real callback without overwriting a player's checkpoint.
                Set(game,"store",new CheckpointStore(Path.Combine(Output,"test-checkpoint.json")));
                game.Flow.Restore("F007",Array.Empty<ChoiceRecord>());
                report.unityVersion = UnityEngine.Application.unityVersion;
                Next(1,.3); return;
            }
            if (watching)
            {
                Require(game.Flow.IsPaused && Time.timeScale == 0 && Time.timeAsDouble == frozenTime,"Game clock stays frozen throughout carousel/settings/confirmations");
                report.gameFrozen = true;
                if (Shown("pause-panel") && Shown("modal-layer"))
                {
                    int lensCount = Enumerable.Range(0,6).Count(i => Shown("pause-lens-"+i));
                    if ((bool)Get(carousel,"turning"))
                    {
                        Require((float)Get(carousel,"elapsed") < (float)TurnWait,
                            "The transition has no disabled tail after its configured duration");
                        Require(lensCount >= 2 && lensCount <= 3,"Two or three objective slots during the handoff");
                        Require(Names.Count(n => Shown(n)) == 2,"Only the outgoing and incoming actions are displayed during the handoff");
                        foreach (string name in Names.Where(Shown))
                            Require(!doc.rootVisualElement.Q<Button>(name).enabledInHierarchy,"Transition action cannot execute: " + name);
                        report.transitionActionsDisabled = report.transitionObjectiveSlotsBounded = true;
                    }
                    else CheckAction((int)Get(carousel,"activeIndex"));
                    report.observedFrames++;
                }
            }
            if (EditorApplication.timeSinceStartup < readyAt || Time.frameCount < minFrame) return;
            if (phase == 1) { Capture("gameplay_"+Size()+".png"); Click("pause-open"); Next(2,.15); return; }
            if (phase == 2)
            {
                watching = true; frozenTime = Time.timeAsDouble;
                CheckAction(0); CheckLayout();
                Capture("pause_0_"+Size()+".png");
                Color backdrop = doc.rootVisualElement.Q("modal-layer").resolvedStyle.backgroundColor;
                Require(Mathf.Approximately(backdrop.a,.5f) && backdrop.r <= .001f && backdrop.g <= .001f && backdrop.b <= .001f,"Pause backdrop dims the game with 50 percent black");
                report.dimmedGameBackdrop = true;
                if (sizeIndex > 0) { expectedIndex = 1; PointerClick("pause-turret"); Next(20,TurnWait); return; }
                PointerClick("pause-turret"); Require((bool)Get(carousel,"turning"),"Real pointer turns the turret"); Next(3,TurnWait); return;
            }
            if (phase == 3)
            {
                CheckAction(1); Capture("pause_1_save.png"); PointerClick("pause-save");
                var checkpoint = new CheckpointStore(Path.Combine(Output,"test-checkpoint.json")).Load();
                Require(checkpoint.nodeId == "F007" && game.StatusMessage.StartsWith("已保存"),"Current checkpoint is saved by visible action"); report.save = true;
                PointerClick("pause-turret"); Next(4,TurnWait); return;
            }
            if (phase == 4) { CheckAction(2); Capture("pause_2_settings.png"); PointerClick("pause-settings"); Next(5,.15); return; }
            if (phase == 5) { Require(Shown("settings-panel") && game.Flow.IsPaused,"Settings opens while paused"); Click("settings-close"); Next(6,.15); return; }
            if (phase == 6) { CheckAction(2); report.settingsReturn = true; PointerClick("pause-turret"); Next(7,TurnWait); return; }
            if (phase == 7) { CheckAction(3); Capture("pause_3_title.png"); PointerClick("pause-title"); Next(8,.15); return; }
            if (phase == 8) { Require(Shown("confirm-panel"),"Title confirmation opens"); Click("confirm-no"); Next(9,.15); return; }
            if (phase == 9) { CheckAction(3); report.titleCancel = true; PointerClick("pause-turret"); Next(10,TurnWait); return; }
            if (phase == 10) { CheckAction(4); Capture("pause_4_quit.png"); report.fiveActions = true; PointerClick("pause-quit"); Next(11,.15); return; }
            if (phase == 11) { Require(Shown("confirm-panel"),"Quit confirmation opens"); Click("confirm-no"); Next(12,.15); return; }
            if (phase == 12) { CheckAction(4); report.quitCancel = true; RedirectThreeSteps(); Next(13,TurnWait); return; }
            if (phase == 13) { CheckAction(2); report.rapidClicks = true; RedirectThreeSteps(); Next(14,TurnWait); return; }
            if (phase == 14) { CheckAction(0); watching = false; PointerClick("pause-resume"); Next(15,.2); return; }
            if (phase == 15)
            {
                Require(!Shown("modal-layer") && !game.Flow.IsPaused && Time.timeScale == 1 && Time.timeAsDouble > frozenTime,"Continue restores game clock and closes overlay"); report.resume = true;
                report.resolutions.Add(Size());
                if (++sizeIndex < Sizes.Length) { SetTarget(Sizes[sizeIndex]); Next(1,.3); return; }
                report.result = "passed"; File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true)); Finish(0); return;
            }
            if (phase == 20)
            {
                CheckAction(expectedIndex); CheckLayout();
                if (expectedIndex < 4) { expectedIndex++; PointerClick("pause-turret"); Next(20,TurnWait); return; }
                PointerClick("pause-turret"); Next(14,TurnWait);
            }
        }
        catch(Exception e) { File.WriteAllText(Path.Combine(Output,"failure.txt"),"phase="+phase+"\n"+e); Debug.LogException(e); Finish(1); }
    }
    static void CheckAction(int index)
    {
        Require((int)Get(carousel,"activeIndex") == index && !(bool)Get(carousel,"turning"),"Settled on action " + index);
        Require(Shown(Names[index]) && doc.rootVisualElement.Q<Button>(Names[index]).enabledInHierarchy,"Only current action is interactive " + Names[index]);
        foreach (string name in Names.Where(n => n != Names[index])) Require(!Shown(name) && !doc.rootVisualElement.Q<Button>(name).enabledInHierarchy,"Inactive action cannot execute: " + name);
        Require(Names.Count(Shown) == 1,"Exactly one action is displayed when settled");
        Require(Enumerable.Range(0,6).Count(i => Shown("pause-lens-"+i)) == 2,"Exactly two objective slots are displayed when settled");
        report.twoLensesWhenSettled = report.oneActionWhenSettled = true;
        if ((float)Get(carousel,"elapsed") >= (float)TurnWait) report.noTransitionTail = true;
    }
    static void CheckTransitionTiming()
    {
        float duration = (float)TurnWait;
        Require(Mathf.Approximately(duration,.25f),"The scene configures a quarter-second transition");
        var evaluate = carousel.GetType().GetMethod("EvaluateProgress",BindingFlags.NonPublic|BindingFlags.Instance);
        Require(evaluate != null,"Transition progress evaluation is available");
        float previous = 0f;
        foreach (float fraction in new[] { 0f,.25f,.5f,.75f,1f,1.5f,2f })
        {
            float progress = (float)evaluate.Invoke(carousel,new object[] { duration * fraction });
            Require(progress >= 0f && progress <= 1f && progress >= previous,
                "Transition progress stays bounded and never reverses");
            if (fraction >= 1f) Require(Mathf.Approximately(progress,1f),"Progress reaches the final pose at the duration without a return segment");
            previous = progress;
        }
        report.boundedTransitionProgress = true;
    }
    static void RedirectThreeSteps()
    {
        int start = (int)Get(carousel,"activeIndex");
        for (int i = 0; i < 3; i++)
        {
            PointerClick("pause-turret");
            Require((bool)Get(carousel,"turning"),"Rapid pointer click starts a transition immediately");
            Require((int)Get(carousel,"incomingIndex") == (start + i + 1) % Names.Length,
                "Each rapid click immediately redirects to the next action instead of queueing");
            Require(Mathf.Approximately((float)Get(carousel,"elapsed"),0f),"Each rapid click restarts the transition clock immediately");
        }
        report.immediateRedirection = true;
    }
    static void CheckLayout()
    {
        var root = doc.rootVisualElement; var button = root.Q<Button>(Names[(int)Get(carousel,"activeIndex")]);
        var style = button.resolvedStyle;
        Require(style.backgroundColor.a == 0 && style.borderLeftWidth == 0 && style.borderTopWidth == 0,"Action has no colored frame");
        var expectedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Via/Art/Fonts/FONT_Main_Progress.ttf");
        Require(expectedFont != null && (style.unityFont == expectedFont || style.unityFontDefinition.font == expectedFont)
            && Mathf.Approximately(style.fontSize,38f),"Action uses FONT_Main_Progress at 38px"); report.textStyle = true;
        Require(root.worldBound.Contains(button.worldBound.min) && root.worldBound.Contains(button.worldBound.max-Vector2.one),"Action fits viewport " + Size());
        var p = new Vector2(root.worldBound.xMax-20,20); var picked = root.panel.Pick(p);
        while(picked != null && picked.name != "modal-layer") picked = picked.parent;
        Require(picked != null,"Dimmed empty region still blocks the game");
    }
    static void PointerClick(string name)
    {
        var root = doc.rootVisualElement; var b = root.Q<Button>(name); Require(b.enabledInHierarchy,"Clickable " + name);
        Vector2 p = b.worldBound.center; var picked = root.panel.Pick(p);
        Require(picked == b || b.Contains(picked),"Pointer reaches " + name + "; picked=" + picked?.name);
        var down = new Event { type = EventType.MouseDown,button=0,mousePosition=p,clickCount=1 };
        using(var evt = PointerDownEvent.GetPooled(down)) { evt.target = picked; picked.SendEvent(evt); }
        var up = new Event { type = EventType.MouseUp,button=0,mousePosition=p,clickCount=1 };
        var receiver = root.panel.GetCapturingElement(0) as VisualElement ?? root.panel.Pick(p);
        using(var evt = PointerUpEvent.GetPooled(up)) { evt.target = receiver; receiver.SendEvent(evt); }
    }
    static Color32[] Capture(string name)
    {
        var old = RenderTexture.active; RenderTexture.active = target; var im = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        im.ReadPixels(new Rect(0,0,target.width,target.height),0,0); im.Apply(); var pixels = im.GetPixels32(); File.WriteAllBytes(Path.Combine(Output,name),im.EncodeToPNG()); UnityEngine.Object.Destroy(im); RenderTexture.active=old; return pixels;
    }
    static void SetTarget(Vector2Int size) { var old=target; target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32); target.Create(); doc.panelSettings.targetTexture=target; if(old!=null&&!AssetDatabase.Contains(old)){old.Release();UnityEngine.Object.Destroy(old);} }
    static string Size() => Sizes[sizeIndex].x+"x"+Sizes[sizeIndex].y;
    static object Get(object o,string name) => o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Set(object o,string name,object value) => o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    static bool Shown(string name) => doc.rootVisualElement.Q(name).style.display.value != DisplayStyle.None;
    static void Next(int next,double wait) { phase=next; readyAt=EditorApplication.timeSinceStartup+wait; minFrame=Time.frameCount+3; }
    static void Click(string name) { var b=doc.rootVisualElement.Q<Button>(name); b.Focus(); using(var evt=NavigationSubmitEvent.GetPooled()) b.SendEvent(evt); }
    static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    static void Finish(int code) { SessionState.SetBool("PauseCarousel.Active",false); EditorApplication.update-=Tick; Time.timeScale=1; AudioListener.pause=false; EditorApplication.Exit(code); }
}
