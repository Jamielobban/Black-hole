using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackHole
{
    [RequireComponent(typeof(Camera))]
    public sealed class BlackHoleExplorer : MonoBehaviour
    {
        public Material material;
        [Range(5, 45)] public float distance = 22;
        [Range(-80, 80)] public float elevation = 12;
        public float azimuth = 0;
        [Range(5, 16)] public float diskRadius = 10;
        [Range(0.2f, 6)] public float brightness = 2.5f;
        [Range(0, 8)] public float simulationSpeed = 1;
        public bool paused;
        public bool showControls = true;
        public int integrationSteps = 480;
        public bool volumetricDisk = true;
        [Range(0.06f,0.6f)] public float diskThickness = 0.18f;
        [Range(0.2f,3f)] public float skyBrightness = 1;
        public Cubemap sky;
        Vector2 panelScroll;
        float smoothedFrameTime;
        float simulationTime;
        Camera viewCamera;
        GUIStyle title, small;

        void OnEnable() { viewCamera = GetComponent<Camera>(); if(!sky) sky=Resources.Load<Cubemap>("BlackHoleSky"); ApplyView(); }
        void Update()
        {
            smoothedFrameTime=Mathf.Lerp(smoothedFrameTime,Time.unscaledDeltaTime,0.06f);
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.hKey.wasPressedThisFrame) showControls = !showControls;
                if (keyboard.rKey.wasPressedThisFrame) { distance=22; elevation=12; azimuth=0; }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta=mouse.delta.ReadValue();
                    azimuth+=delta.x*0.2f;
                    elevation=Mathf.Clamp(elevation-delta.y*0.15f,-80,80);
                }
                if (!showControls || mouse.position.ReadValue().x > 330)
                    distance=Mathf.Clamp(distance*Mathf.Exp(-mouse.scroll.ReadValue().y*0.001f),5,45);
            }
            if (!paused) simulationTime += Time.deltaTime*simulationSpeed;
            ApplyView();
        }
        public void ApplyView()
        {
            if (!viewCamera) viewCamera=GetComponent<Camera>();
            transform.position=Quaternion.Euler(elevation,azimuth,0)*new Vector3(0,0,-distance);
            transform.LookAt(Vector3.zero);
            if (!material) return;
            material.SetVector("_Observer",transform.position);
            material.SetVector("_ViewRight",transform.right);
            material.SetVector("_ViewUp",transform.up);
            material.SetVector("_ViewForward",transform.forward);
            material.SetFloat("_TanHalfFov",Mathf.Tan(viewCamera.fieldOfView*Mathf.Deg2Rad*0.5f));
            material.SetFloat("_ViewAspect",viewCamera.aspect);
            material.SetFloat("_SimulationTime",simulationTime);
            material.SetFloat("_DiskOuter",diskRadius);
            material.SetFloat("_Exposure",brightness);
            material.SetInt("_Steps",integrationSteps);
            material.SetFloat("_VolumeEnabled",volumetricDisk?1:0);
            material.SetFloat("_DiskThickness",diskThickness);
            material.SetFloat("_VolumeQuality",integrationSteps>480?2:1);
            material.SetInt("_RaySamples",integrationSteps<=300?1:integrationSteps<=480?2:4);
            material.SetFloat("_SkyBrightness",skyBrightness);
            material.SetFloat("_HasSky",sky?1:0);
            if(sky) material.SetTexture("_SkyCube",sky);
        }
        void OnGUI()
        {
            if (!showControls) return;
            if (title == null)
            {
                title=new GUIStyle(GUI.skin.label) {fontSize=24, fontStyle=FontStyle.Bold};
                title.normal.textColor=new Color(1,0.83f,0.58f);
                small=new GUIStyle(GUI.skin.label) {fontSize=12,wordWrap=true};
                small.normal.textColor=new Color(0.65f,0.72f,0.8f);
            }
            GUILayout.BeginArea(new Rect(22,22,290,Mathf.Min(590,Screen.height-44)),GUI.skin.box);
            panelScroll=GUILayout.BeginScrollView(panelScroll);
            GUILayout.Space(12);
            GUILayout.Label("BLACK HOLE",title);
            GUILayout.Label("SCHWARZSCHILD / LIGHT LAB",small);
            GUILayout.Space(14);
            GUILayout.Label("Right-drag to orbit · Scroll to zoom\nSpace pause · R reset · H hide panel",small);
            GUILayout.Space(15);
            distance=Slider("Observer distance",distance,5,45," Rs");
            elevation=Slider("Viewing angle",elevation,-80,80,"°");
            diskRadius=Slider("Disk outer radius",diskRadius,5,16," Rs");
            brightness=Slider("Disk brightness",brightness,0.2f,6,"");
            simulationSpeed=Slider("Time speed",simulationSpeed,0,8,"×");
            skyBrightness=Slider("Sky brightness",skyBrightness,0.2f,3,"");
            volumetricDisk=GUILayout.Toggle(volumetricDisk,"Volumetric gas (off = surface fallback)");
            if(volumetricDisk) diskThickness=Slider("Gas thickness",diskThickness,0.06f,0.6f," Rs");
            GUILayout.Space(8);
            int choice=GUILayout.SelectionGrid(integrationSteps<=300?0:integrationSteps<=480?1:2,new[]{"Fast","Balanced","Fine"},3);
            integrationSteps=choice==0?300:choice==1?480:720;
            if(GUILayout.Button(paused?"Resume disk animation":"Pause disk animation")) paused=!paused;
            GUILayout.Space(10);
            GUILayout.Label("Curved light paths · Non-rotating hole\nHorizon = 1 Rs · Disk begins at 3 Rs\nProcedural gas, not a fluid simulation",small);
            if(smoothedFrameTime>0) GUILayout.Label((1/smoothedFrameTime).ToString("0")+" FPS · "+(smoothedFrameTime*1000).ToString("0.0")+" ms/frame",small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        float Slider(string label,float value,float min,float max,string unit)
        {
            GUILayout.Label(label+"   "+value.ToString("0.0")+unit);
            return GUILayout.HorizontalSlider(value,min,max);
        }
    }
}
