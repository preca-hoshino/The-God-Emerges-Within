using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Via.Application;
using Via.UI;
using Via.Shared;
using Via.Story.Domain;
using Via.Minigame.Runtime;
using Via.Minigame.Presentation;

[InitializeOnLoad]
public static class TutorialInputValidation
{
    [Serializable] public class Report
    {
        public string result,unityVersion;public bool beforeFix,stuckCaptureReproduced,completionModalUnpauses,formalInputRestored,exitRestores,pauseRestores,disableReleases,cancelReleases,detachReleases,normalPullCommits,normalCutCommits;public int completedCycles;public List<string> resolutions=new List<string>();
    }
    static Report report=new Report();static UIDocument doc;static GameController game;static MinigameController mini;static NeuronToolController tools;static CanvasPointerInput input;static NeuronCanvasElement canvas;static RenderTexture target;static double began,readyAt;static int phase,minFrame,cycle,sizeIndex;static Vector2 heldPosition;static string cancelCase;static int cancelIndex;
    static readonly Vector2Int[] sizes={new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)};
    static string Output=>Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName,"UIExports","TutorialInput_20261019");
    static TutorialInputValidation(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("TutorialInput.Active",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
    public static void RunBefore()=>Run(true);
    public static void RunAfter()=>Run(false);
    static void Run(bool before)
    {
        Directory.CreateDirectory(Output);File.Delete(Path.Combine(Output,before?"before-failure.txt":"failure.txt"));File.Delete(Path.Combine(Output,before?"before-report.json":"report.json"));
        EditorSceneManager.OpenScene("Assets/Via/Scenes/GameFramework.unity",OpenSceneMode.Single);var d=UnityEngine.Object.FindFirstObjectByType<UIDocument>();AssetDatabase.DeleteAsset("Assets/TutorialInputTarget.renderTexture");var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);AssetDatabase.CreateAsset(rt,"Assets/TutorialInputTarget.renderTexture");d.panelSettings.targetTexture=rt;AssetDatabase.SaveAssets();SessionState.SetBool("TutorialInput.Before",before);SessionState.SetBool("TutorialInput.Active",true);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-began>140)throw new TimeoutException("Tutorial phase "+phase+" cycle="+cycle);
            if(doc==null)
            {
                var d=UnityEngine.Object.FindFirstObjectByType<UIDocument>();if(d==null||!d.GetComponent<LoadingIntroController>().IsCompleted)return;
                doc=d;game=UnityEngine.Object.FindFirstObjectByType<GameController>();mini=UnityEngine.Object.FindFirstObjectByType<MinigameController>();tools=(NeuronToolController)Get(d.GetComponent<GameUIController>(),"tools");input=(CanvasPointerInput)Get(d.GetComponent<GameUIController>(),"pointer");canvas=(NeuronCanvasElement)Get(doc.GetComponent<GameUIController>(),"canvas");target=d.panelSettings.targetTexture;report.beforeFix=SessionState.GetBool("TutorialInput.Before",false);report.unityVersion=UnityEngine.Application.unityVersion;
                game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(1,.08);return;
            }
            if(Time.frameCount<minFrame||EditorApplication.timeSinceStartup<readyAt)return;
            if(phase==1)
            {
                Require(mini.IsTutorial&&mini.CanEdit&&tools.SelectedTool==NeuronToolController.Tool.Reshape,"Real Level_04 reshape tutorial active");Require(!Shown("modal-layer"),"No modal before completion");
                heldPosition=World(new Vector2(2,0));Require(Within(Pick(heldPosition),canvas),"Tutorial corner physically reachable");Down(canvas,heldPosition);Require(mini.ToolUses(Via.Minigame.Configuration.MinigameTool.Reshape)==1,"Press performs actual reshape");Require(doc.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId)==canvas,"Canvas captures held mouse");
                if(cycle%2==1&&!report.beforeFix)Up(Routed(heldPosition),heldPosition);Next(2,.1);return;
            }
            if(phase==2)
            {
                Require(!mini.IsTutorial&&Shown("notice-panel")&&Shown("modal-layer"),"Tutorial completed and completion dialog opened");Require(game.Flow.IsPaused&&mini.IsPaused&&Time.timeScale==0,"Completion modal pauses game");
                bool captured=doc.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId)==canvas;
                if(report.beforeFix)
                {
                    Require(captured,"Reproduce capture left behind by completion");Up(Routed(heldPosition),heldPosition);Require(doc.rootVisualElement.panel.GetCapturingElement(PointerId.mousePointerId)==canvas,"Release is swallowed after pressed was cleared");report.stuckCaptureReproduced=true;Capture("before_stuck_completion.png");report.result="reproduced";Save();Finish(0);return;
                }
                Require(!captured,"Completion must release held mouse");if(cycle%2==0)Up(Routed(heldPosition),heldPosition);MouseClick("notice-close");Next(3,.1);return;
            }
            if(phase==3)
            {
                Require(!Shown("modal-layer")&&!game.Flow.IsPaused&&!mini.IsPaused&&Time.timeScale>0,"Mouse click closes completion modal and resumes");report.completionModalUnpauses=true;Require(canvas.InputEnabled&&!input.IsBlocked&&mini.CanEdit&&!mini.IsGuided&&!tools.IsDragging,"Formal input restored");Require(doc.rootVisualElement.panel.GetCapturingElement(0)==null,"No stale capture after modal");Require(mini.Network.Nodes.Count==1&&mini.Network.Edges.Count==0&&game.Flow.History.Records.Count==0,"Reset only tutorial state, no formal record");
                var p=World(new Vector2(1.8f,0));int before=mini.AttractorPlacedCount;Down(Routed(p),p);Up(Routed(p),p);Require(mini.AttractorPlacedCount==before+1,"Actual pointer click places formal attractor");report.formalInputRestored=true;report.completedCycles++;
                if(cycle==0)Capture("after_formal_input_restored.png");
                if(++cycle<4){game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(1,.08);return;}
                if(++sizeIndex<sizes.Length){cycle=0;SetTarget(sizes[sizeIndex]);game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(1,.15);return;}
                report.resolutions.Add("1920x1080");report.resolutions.Add("1280x720");report.resolutions.Add("1024x768");report.resolutions.Add("2560x1080");sizeIndex=0;SetTarget(sizes[0]);cancelIndex=0;StartCancelCase();return;
            }
            if(phase==10)
            {
                Require(mini.IsTutorial&&mini.CanEdit,"Cancellation fixture tutorial");Nav("tool-pull");heldPosition=World(new Vector2(1,0));Down(canvas,heldPosition);Require(tools.IsDragging&&canvas.HasPointerCapture(0),"Active pull capture fixture");
                cancelCase=new[]{"pause","exit","disable","cancel","detach"}[cancelIndex];
                if(cancelCase=="pause"){Nav("pause-open");Require(!canvas.HasPointerCapture(0)&&!tools.IsDragging,"Pause cancels and releases active drag");Next(14,.1);return;}
                if(cancelCase=="exit"){Nav("teaching-exit");Require(!canvas.HasPointerCapture(0)&&!tools.IsDragging,"Tutorial exit confirmation cancels and releases drag");Next(14,.1);return;}
                if(cancelCase=="disable"){canvas.InputEnabled=false;Require(!canvas.HasPointerCapture(0)&&!tools.IsDragging,"Disabling input releases capture");report.disableReleases=true;canvas.InputEnabled=true;Next(11,.05);return;}
                if(cancelCase=="cancel"){var sys=new Event{type=EventType.MouseDown,button=0,mousePosition=heldPosition};using(var evt=PointerCancelEvent.GetPooled(sys)){evt.target=canvas;canvas.SendEvent(evt);}Require(!canvas.HasPointerCapture(0)&&!tools.IsDragging,"Pointer cancellation releases capture");report.cancelReleases=true;Next(11,.05);return;}
                if(cancelCase=="detach"){var parent=canvas.parent;parent.Remove(canvas);Require(doc.rootVisualElement.panel.GetCapturingElement(0)!=canvas&&!tools.IsDragging,"Detached canvas releases capture");parent.Add(canvas);report.detachReleases=true;Next(11,.08);return;}
            }
            if(phase==14)
            {
                // The newly opened modal needs a real layout pass before pointer picking.
                if(cancelCase=="pause"){MouseClick("pause-resume");report.pauseRestores=true;}
                else MouseClick("confirm-yes");
                Next(11,.08);return;
            }
            if(phase==11)
            {
                Require(!game.Flow.IsPaused&&!mini.IsPaused&&Time.timeScale>0&&canvas.InputEnabled&&!input.IsBlocked&&!canvas.HasPointerCapture(0),"Input usable after "+cancelCase);if(cancelCase=="exit"){Require(!mini.IsTutorial&&mini.Network.Edges.Count==0,"Exit restores formal solve");report.exitRestores=true;}
                if(++cancelIndex<5){StartCancelCase();return;}
                game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(12,.1);return;
            }
            if(phase==12)
            {
                Nav("tool-pull");var a=World(new Vector2(1,0));var b=World(new Vector2(1,-.6f));Down(canvas,a);Drag(canvas,b);Up(Routed(b),b);
                Require(mini.ToolUses(Via.Minigame.Configuration.MinigameTool.Pull)==1&&!tools.IsDragging&&!canvas.HasPointerCapture(0),"Normal pointer-up commits pull before cleanup");report.normalPullCommits=true;
                game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(13,.1);return;
            }
            if(phase==13)
            {
                Nav("tool-cut");var a=World(new Vector2(1.5f,-1));var b=World(new Vector2(1.5f,1));Down(canvas,a);Drag(canvas,b);Up(Routed(b),b);
                Require(mini.ToolUses(Via.Minigame.Configuration.MinigameTool.Cut)==1&&mini.Network.HasCutEndpoints&&!tools.IsDragging&&!canvas.HasPointerCapture(0),"Normal pointer-up commits cut before cleanup");report.normalCutCommits=true;
                report.result="passed";Save();Finish(0);
            }
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Output,SessionState.GetBool("TutorialInput.Before",false)?"before-failure.txt":"failure.txt"),"phase="+phase+" cycle="+cycle+"\n"+e);Debug.LogException(e);Finish(1);}
    }
    static void StartCancelCase(){game.Flow.Restore("F438",Array.Empty<ChoiceRecord>());Next(10,.1);}
    static bool Within(VisualElement e,VisualElement ancestor){while(e!=null){if(e==ancestor)return true;e=e.parent;}return false;}
    static Vector2 World(Vector2 p){float scale=canvas.contentRect.height/12f;return canvas.LocalToWorld(canvas.contentRect.center+new Vector2(p.x,-p.y)*scale);}
    static VisualElement Pick(Vector2 p)=>doc.rootVisualElement.panel.Pick(p);
    static VisualElement Routed(Vector2 p)=>doc.rootVisualElement.panel.GetCapturingElement(0) as VisualElement??Pick(p);
    static void Down(VisualElement target,Vector2 p){var sys=new Event{type=EventType.MouseDown,button=0,mousePosition=p,clickCount=1};using(var evt=PointerDownEvent.GetPooled(sys)){evt.target=target;target.SendEvent(evt);}}
    static void Drag(VisualElement target,Vector2 p){var sys=new Event{type=EventType.MouseDrag,button=0,mousePosition=p};using(var evt=PointerMoveEvent.GetPooled(sys)){evt.target=target;target.SendEvent(evt);}}
    static void Up(VisualElement target,Vector2 p){var sys=new Event{type=EventType.MouseUp,button=0,mousePosition=p,clickCount=1};using(var evt=PointerUpEvent.GetPooled(sys)){evt.target=target;target.SendEvent(evt);}}
    static void MouseClick(string n){var e=doc.rootVisualElement.Q<Button>(n);Require(e.enabledInHierarchy,"Pointer button enabled "+n);var p=e.worldBound.center;Require(Within(Pick(p),e),"Pointer button physically reachable "+n);Down(Routed(p),p);Up(Routed(p),p);}
    static void Nav(string n){var b=doc.rootVisualElement.Q<Button>(n);b.Focus();using(var evt=NavigationSubmitEvent.GetPooled())b.SendEvent(evt);}
    static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static bool Shown(string n)=>doc.rootVisualElement.Q(n).style.display.value!=DisplayStyle.None;
    static void Next(int p,double delay){phase=p;minFrame=Time.frameCount+3;readyAt=EditorApplication.timeSinceStartup+delay;}
    static void Require(bool v,string why){if(!v)throw new InvalidOperationException(why);}
    static void Save()=>File.WriteAllText(Path.Combine(Output,report.beforeFix?"before-report.json":"report.json"),JsonUtility.ToJson(report,true));
    static void Capture(string n){var old=RenderTexture.active;RenderTexture.active=target;var im=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);im.ReadPixels(new Rect(0,0,target.width,target.height),0,0);im.Apply();File.WriteAllBytes(Path.Combine(Output,n),im.EncodeToPNG());UnityEngine.Object.Destroy(im);RenderTexture.active=old;}
    static void SetTarget(Vector2Int size){var old=target;target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);target.Create();doc.panelSettings.targetTexture=target;if(old!=null&&!AssetDatabase.Contains(old)){old.Release();UnityEngine.Object.Destroy(old);}}
    static void Finish(int code){SessionState.SetBool("TutorialInput.Active",false);EditorApplication.update-=Tick;Time.timeScale=1;AudioListener.pause=false;EditorApplication.Exit(code);}
}
