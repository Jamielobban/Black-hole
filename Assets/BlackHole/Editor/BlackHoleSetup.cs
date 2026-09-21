using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlackHole.Editor
{
    public static class BlackHoleSetup
    {
        const string Root="Assets/BlackHole";
        [MenuItem("Black Hole/Create or Open Explorer")]
        public static void CreateOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(Root+"/BlackHoleExplorer.unity"))
            { EditorSceneManager.OpenScene(Root+"/BlackHoleExplorer.unity"); return; }
            Build();
        }
        public static void Build()
        {
            var shader=Shader.Find("BlackHole/Schwarzschild");
            if(!shader) throw new Exception("Black-hole shader not imported.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/BlackHole.mat");
            if(!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,Root+"/BlackHole.mat"); }
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/BlackHoleRenderer.asset");
            if(!renderer)
            {
                renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,Root+"/BlackHoleRenderer.asset");
                var feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name="Black Hole Light Paths";
                feature.passMaterial=material;
                feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.fetchColorBuffer=false;
                feature.requirements=ScriptableRenderPassInput.None;
                AssetDatabase.AddObjectToAsset(feature,renderer);
                renderer.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(renderer);
            }
            // A renderer created with CreateInstance does not inherit the URP
            // post-processing resources. Copy the template's resource reference.
            var template=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var rendererSettings=new SerializedObject(renderer);
            rendererSettings.FindProperty("postProcessData").objectReferenceValue=
                new SerializedObject(template).FindProperty("postProcessData").objectReferenceValue;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            // Use the same appended renderer index for the PC and Mobile quality assets.
            int rendererIndex=-1;
            foreach(string assetPath in new[]{"Assets/Settings/PC_RPAsset.asset","Assets/Settings/Mobile_RPAsset.asset"})
            {
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                if(!pipeline) continue;
                var serialized=new SerializedObject(pipeline);
                var list=serialized.FindProperty("m_RendererDataList");
                int index=-1;
                for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer) index=i;
                if(index<0) { index=list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue=renderer; }
                if(rendererIndex>=0 && rendererIndex!=index) throw new Exception("Pipeline renderer indexes differ.");
                rendererIndex=index;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject=new GameObject("Black Hole Observer");
            cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>();
            camera.fieldOfView=50; camera.nearClipPlane=0.01f; camera.farClipPlane=200;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.allowHDR=true;
            cameraObject.AddComponent<AudioListener>();
            var data=camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(rendererIndex); data.renderPostProcessing=true;
            var explorer=cameraObject.AddComponent<BlackHoleExplorer>(); explorer.material=material; explorer.ApplyView();
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/BlackHoleVolume.asset");
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile,Root+"/BlackHoleVolume.asset");
                var bloom=profile.Add<Bloom>(true); bloom.intensity.Override(0.45f); bloom.threshold.Override(1); bloom.scatter.Override(0.65f);
                var tone=profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var vignette=profile.Add<Vignette>(true); vignette.intensity.Override(0.2f);
                foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
                EditorUtility.SetDirty(profile);
            }
            var volume=new GameObject("Bloom and Filmic Tone Mapping").AddComponent<Volume>();
            volume.isGlobal=true; volume.sharedProfile=profile;
            EditorSceneManager.SaveScene(scene,Root+"/BlackHoleExplorer.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("BLACK_HOLE_SETUP_OK");
        }
        public static void ValidateAndCapture()
        {
            ValidateOrbits();
            EditorSceneManager.OpenScene(Root+"/BlackHoleExplorer.unity");
            var explorer=UnityEngine.Object.FindFirstObjectByType<BlackHoleExplorer>();
            var camera=explorer.GetComponent<Camera>();
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);
            target.Create(); camera.targetTexture=target; explorer.ApplyView();
            camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=target;
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/black-hole-preview.png",pixels.EncodeToPNG());
            explorer.elevation=55;
            explorer.ApplyView();
            camera.Render();
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            File.WriteAllBytes("Artifacts/black-hole-high-angle.png",pixels.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            foreach(var message in ShaderUtil.GetShaderMessages(explorer.material.shader))
                if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new Exception(message.message);
            Debug.Log("BLACK_HOLE_RENDER_OK");
        }

        public static void BuildAndValidate() { Build(); ValidateAndCapture(); }

        public static void ValidateVolume()
        {
            ValidateOrbits();
            EditorSceneManager.OpenScene(Root+"/BlackHoleExplorer.unity");
            var explorer=UnityEngine.Object.FindFirstObjectByType<BlackHoleExplorer>();
            var camera=explorer.GetComponent<Camera>();
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            target.Create(); camera.targetTexture=target;
            Directory.CreateDirectory("Artifacts");
            var report=new System.Text.StringBuilder("1280x720, Balanced; milliseconds include synchronous CPU rendering and GPU readback, NOT gameplay FPS.\n");
            try
            {
                foreach(bool volume in new[]{false,true})
                foreach(float angle in new[]{12f,55f,0f})
                {
                    explorer.volumetricDisk=volume; explorer.elevation=angle;
                    explorer.diskThickness=0.18f; explorer.ApplyView();
                    string name=(volume?"volume":"surface")+"-"+angle.ToString("0");
                    // Warm the shader and post-processing before timing.
                    for(int i=0;i<2;i++) { camera.Render(); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); }
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<4;i++) { camera.Render(); RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); }
                    timer.Stop(); pixels.Apply();
                    report.AppendLine(name+": "+(timer.Elapsed.TotalMilliseconds/4).ToString("F2")+" ms");
                    File.WriteAllBytes("Artifacts/"+name+".png",pixels.EncodeToPNG());
                    int bright=0;
                    foreach(var pixel in pixels.GetPixels32()) if(pixel.r>100 && pixel.r>pixel.b*1.2f) bright++;
                    if(volume && bright<500) throw new Exception("Volume image unexpectedly empty: "+name);
                }
                foreach(float thickness in new[]{0.06f,0.6f})
                {
                    explorer.diskThickness=thickness; explorer.ApplyView();
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                    File.WriteAllBytes("Artifacts/volume-thickness-"+thickness.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png",pixels.EncodeToPNG());
                }
                foreach(var message in ShaderUtil.GetShaderMessages(explorer.material.shader))
                    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new Exception(message.message);
                File.WriteAllText("Artifacts/volume-validation.txt",report.ToString());
                Debug.Log("VOLUME_VALIDATION_OK\n"+report);
            }
            finally
            {
                RenderTexture.active=previous; camera.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // Regression check against the Schwarzschild critical impact parameter
        // b_critical = 3 sqrt(3) / 2, in horizon units. Mirrors the GPU integrator.
        static void ValidateOrbits()
        {
            foreach(float impact in new[]{0f,2.3f,2.57f,2.63f,2.9f,8f})
            {
                Vector3 p=new Vector3(0,0,-22);
                float sine=impact*Mathf.Sqrt(1-1/22f)/22;
                Vector3 v=new Vector3(sine/Mathf.Sqrt(1-1/22f),0,Mathf.Sqrt(1-sine*sine));
                float l2=Vector3.Cross(p,v).sqrMagnitude;
                bool captured=false,escaped=false;
                float maxMomentumError=0;
                for(int i=0;i<480;i++)
                {
                    float r=p.magnitude;
                    if(r<1.015f) { captured=true; break; }
                    if(r>60 && Vector3.Dot(p,v)>0) { escaped=true; break; }
                    float dt=Mathf.Clamp(r*0.025f,0.015f,0.65f);
                    Vector3 a=Acceleration(p,l2);
                    Vector3 next=p+v*dt+0.5f*a*dt*dt;
                    v+=0.5f*(a+Acceleration(next,l2))*dt;
                    p=next;
                    if(l2>0) maxMomentumError=Mathf.Max(maxMomentumError,Mathf.Abs(Vector3.Cross(p,v).sqrMagnitude/l2-1));
                }
                bool shouldCapture=impact<3*Mathf.Sqrt(3)/2;
                if(captured!=shouldCapture || escaped==shouldCapture || maxMomentumError>0.005f)
                    throw new Exception("Orbit validation failed at impact "+impact);
                Debug.Log("ORBIT_OK impact="+impact+" captured="+captured+" angular momentum error="+maxMomentumError);
            }
        }
        static Vector3 Acceleration(Vector3 p,float l2)
        {
            float r2=Mathf.Max(p.sqrMagnitude,0.1f);
            return -1.5f*l2*p/(r2*r2*Mathf.Sqrt(r2));
        }
    }
}
