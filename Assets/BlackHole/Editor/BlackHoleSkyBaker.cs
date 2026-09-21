using UnityEditor;
using UnityEngine;

namespace BlackHole.Editor
{
    public static class BlackHoleSkyBaker
    {
        const int Size=1024;
        static readonly Vector3[] Forward={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        static readonly Vector3[] Right={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left};
        static readonly Vector3[] Up={Vector3.down,Vector3.down,Vector3.forward,Vector3.back,Vector3.down,Vector3.down};
        static float Hash(int x,int y,int z)
        {
            unchecked { uint h=(uint)x*1597334677u^(uint)y*3812015801u^(uint)z*2798796415u;
                h^=h>>16; h*=2246822519u; h^=h>>13; return (h&0xffffff)/16777216f; }
        }
        static float Noise(Vector3 p)
        {
            int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);
            float u=p.x-x,v=p.y-y,w=p.z-z;
            u=u*u*(3-2*u);v=v*v*(3-2*v);w=w*w*(3-2*w);
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Hash(x,y,z),Hash(x+1,y,z),u),Mathf.Lerp(Hash(x,y+1,z),Hash(x+1,y+1,z),u),v),
                Mathf.Lerp(Mathf.Lerp(Hash(x,y,z+1),Hash(x+1,y,z+1),u),Mathf.Lerp(Hash(x,y+1,z+1),Hash(x+1,y+1,z+1),u),v),w);
        }
        static float Clouds(Vector3 p) => Noise(p)*0.55f+Noise(p*2.03f+Vector3.one*17)*0.28f+Noise(p*4.11f)*0.17f;

        [MenuItem("Black Hole/Bake Deep Sky")]
        public static void Bake()
        {
            const string path="Assets/BlackHole/Resources/BlackHoleSky.asset";
            if(!AssetDatabase.IsValidFolder("Assets/BlackHole/Resources")) AssetDatabase.CreateFolder("Assets/BlackHole","Resources");
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            bool create=!cube;
            if(create) cube=new Cubemap(Size,TextureFormat.RGBAHalf,true) {name="BlackHoleSky",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            Vector3 normal=new Vector3(0.5f,0.8f,0.12f).normalized;
            var faces=new Color[6][];
            for(int face=0;face<6;face++)
            {
                var colors=faces[face]=new Color[Size*Size];
                for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
                {
                    Vector3 d=(Forward[face]+Right[face]*((x+0.5f)/Size*2-1)+Up[face]*((y+0.5f)/Size*2-1)).normalized;
                    float latitude=Vector3.Dot(d,normal);
                    float broad=Clouds(d*3+Vector3.one*8);
                    float fine=Clouds(d*18+Vector3.one*27);
                    float band=Mathf.Exp(-Mathf.Pow((latitude+(broad-0.5f)*0.12f)/0.19f,2));
                    float haze=Mathf.Exp(-latitude*latitude/0.22f);
                    float dust=Mathf.Exp(-Mathf.Pow((latitude+(fine-0.5f)*0.1f)/0.045f,2))*(0.35f+fine*0.65f);
                    Color tint=Color.Lerp(new Color(0.045f,0.13f,0.24f),new Color(0.2f,0.06f,0.16f),broad);
                    Color c=new Color(0.002f,0.0035f,0.009f)+tint*(band*(0.18f+fine*fine*1.8f)+haze*0.06f)*(1-dust*0.9f)*4;
                    c.a=1; colors[y*Size+x]=c;
                }
            }
            var random=new System.Random(74021);
            for(int star=0;star<11000;star++)
            {
                float z=(float)random.NextDouble()*2-1,phi=(float)random.NextDouble()*Mathf.PI*2;
                Vector3 d=new Vector3(Mathf.Sqrt(1-z*z)*Mathf.Cos(phi),z,Mathf.Sqrt(1-z*z)*Mathf.Sin(phi));
                // Add a denser stellar population in the galactic plane.
                if(star>6500) d=(d-normal*Vector3.Dot(d,normal)*0.9f).normalized;
                float rare=(float)random.NextDouble();
                float sigma=rare>0.985f?0.8f:0.42f;
                float intensity=rare>0.985f?2.5f:0.22f+(float)random.NextDouble()*0.9f;
                Color tint=Color.Lerp(new Color(0.55f,0.72f,1),new Color(1,0.78f,0.48f),(float)random.NextDouble())*intensity;
                for(int face=0;face<6;face++)
                {
                    float denom=Vector3.Dot(d,Forward[face]); if(denom<=0) continue;
                    float u=Vector3.Dot(d,Right[face])/denom,v=Vector3.Dot(d,Up[face])/denom;
                    if(Mathf.Abs(u)>1.03f || Mathf.Abs(v)>1.03f) continue;
                    float px=(u+1)*Size*0.5f-0.5f,py=(v+1)*Size*0.5f-0.5f;
                    // Project to every overlapping face, including edge padding.
                    for(int y=Mathf.Max(0,Mathf.FloorToInt(py)-4);y<=Mathf.Min(Size-1,Mathf.CeilToInt(py)+4);y++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(px)-4);x<=Mathf.Min(Size-1,Mathf.CeilToInt(px)+4);x++)
                    {
                        float r2=(x-px)*(x-px)+(y-py)*(y-py);
                        faces[face][y*Size+x]+=tint*Mathf.Exp(-r2/(2*sigma*sigma));
                    }
                }
            }
            for(int face=0;face<6;face++) cube.SetPixels(faces[face],(CubemapFace)face);
            cube.Apply(true,false);
            if(create) AssetDatabase.CreateAsset(cube,path); else EditorUtility.SetDirty(cube);
            AssetDatabase.SaveAssets(); Debug.Log("DEEP_SKY_BAKED");
        }
    }
}
