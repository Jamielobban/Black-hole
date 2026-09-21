using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlackHole.Editor
{
    [InitializeOnLoad]
    public static class BlackHolePlayValidation
    {
        const string Key="BlackHole.IsolatedPlayValidation";
        static BlackHoleExplorer explorer;
        static Camera camera;
        static RenderTexture target;
        static Texture2D pixels;
        static int lastFrame=-1,frame,stage;
        static readonly List<float> timings=new List<float>();
        static readonly System.Text.StringBuilder report=new System.Text.StringBuilder();
        static BlackHolePlayValidation() { EditorApplication.playModeStateChanged+=Changed; }
        public static void Begin()
        {
            if(!Application.isBatchMode) throw new Exception("Run this check in an isolated batch editor.");
            BlackHoleSkyBaker.Bake();
            EditorSceneManager.OpenScene("Assets/BlackHole/BlackHoleExplorer.unity");
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false)) return;
            explorer=UnityEngine.Object.FindFirstObjectByType<BlackHoleExplorer>();
            camera=explorer.GetComponent<Camera>();
            explorer.sky=Resources.Load<Cubemap>("BlackHoleSky");
            explorer.showControls=false; explorer.paused=true;
            target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);target.Create();camera.targetTexture=target;
            pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            report.AppendLine("Unity Play mode, batch editor; 1280x720 camera target, orbiting camera, 30 warm-up + 90 measured frames per preset. CPU frame interval includes editor overhead; not standalone or GPU-only FPS.");
            Directory.CreateDirectory("Artifacts");
            EditorApplication.update+=Tick;
        }
        static void Capture(string name)
        {
            var previous=RenderTexture.active;RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=previous;
            File.WriteAllBytes("Artifacts/"+name+".png",pixels.EncodeToPNG());
        }
        static void Tick()
        {
            if(!Application.isPlaying || Time.frameCount==lastFrame) return;
            lastFrame=Time.frameCount;
            try
            {
                explorer.integrationSteps=stage==0?300:stage==1?480:720;
                explorer.azimuth=Mathf.Max(0,frame-30)*0.025f;
                explorer.ApplyView();
                if(frame>30) timings.Add(Time.unscaledDeltaTime*1000);
                frame++;
                if(frame<121) return;
                string name=stage==0?"fast":stage==1?"balanced":"fine";
                timings.Sort();
                float sum=0;foreach(float t in timings)sum+=t;
                report.AppendLine(name+": mean "+(sum/timings.Count).ToString("F2")+" ms, p95 "+timings[Mathf.Min(timings.Count-1,(int)(timings.Count*0.95f))].ToString("F2")+" ms");
                Capture("sky-"+name);
                timings.Clear();frame=0;stage++;
                if(stage<3) return;
                explorer.elevation=0;explorer.ApplyView();camera.Render();Capture("sky-edge-on");
                explorer.elevation=55;explorer.ApplyView();camera.Render();Capture("sky-high-angle");
                explorer.elevation=12;explorer.azimuth=90;explorer.ApplyView();camera.Render();Capture("sky-quarter-orbit");
                explorer.azimuth=0;explorer.ApplyView();
                explorer.material.SetFloat("_SimulationTime",40);camera.Render();Capture("sky-gas-time40");
                foreach(var message in ShaderUtil.GetShaderMessages(explorer.material.shader))
                    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new Exception(message.message);
                File.WriteAllText("Artifacts/play-profile.txt",report.ToString());
                Debug.Log("SKY_PLAY_VALIDATION_OK\n"+report);
                Finish(0);
            }
            catch(Exception e) {Debug.LogException(e);Finish(1);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
            camera.targetTexture=null;target.Release();
            UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
            EditorApplication.Exit(code);
        }
    }
}
