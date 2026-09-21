Shader "BlackHole/Schwarzschild"
{
    Properties
    {
        _DiskOuter ("Disk outer radius (horizon units)", Float) = 10
        _Exposure ("Disk brightness", Float) = 2.5
        _Steps ("Integration steps", Int) = 480
        _VolumeEnabled ("Volumetric disk", Float) = 1
        _DiskThickness ("Gas scale height at 6 Rs", Float) = 0.18
        _VolumeQuality ("Volume sampling quality", Float) = 1
        _SkyCube ("Deep sky", Cube) = "black" {}
        _SkyBrightness ("Sky brightness", Float) = 1
        _RaySamples ("Subpixel rays", Int) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _DiskOuter, _Exposure, _SimulationTime;
            float _VolumeEnabled, _DiskThickness, _VolumeQuality;
            int _Steps;
            int _RaySamples;
            float _SkyBrightness, _HasSky;
            TEXTURECUBE(_SkyCube); SAMPLER(sampler_SkyCube);
            float4 _Observer, _ViewRight, _ViewUp, _ViewForward;
            float _TanHalfFov, _ViewAspect;

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                o.uv = GetFullScreenTriangleTexCoord(id);
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            float3 Sky(float3 d)
            {
                float2 uv=float2(atan2(d.z,d.x)/TWO_PI+0.5, asin(clamp(d.y,-1,1))/PI+0.5);
                float3 color=float3(0.0015,0.002,0.004);
                // Two scales of procedural stars; not an astrophysical sky catalogue.
                for(int layer=0;layer<2;layer++)
                {
                    float2 grid=uv*float2(1100,550)*(1+layer*0.61);
                    float2 cell=floor(grid);
                    float seed=Hash(cell+layer*71);
                    float2 offset=float2(Hash(cell+19),Hash(cell+47));
                    float dist=length(frac(grid)-offset);
                    float star=exp(-dist*dist/(0.003+0.006*seed));
                    color+=step(0.982,seed)*star*lerp(float3(0.55,0.7,1),float3(1,0.85,0.65),Hash(cell+2))*2;
                }
                float band=exp(-pow((d.y+0.25*d.x)/0.14,2));
                color+=float3(0.018,0.022,0.038)*band*Noise(uv*float2(60,30));
                return color;
            }
            float3 Acceleration(float3 p,float angularMomentumSquared)
            {
                float r2=max(dot(p,p),0.1);
                // Schwarzschild null orbit in its orbital plane, Rs=1 and E=1.
                // x'' = -(3/2) L^2 x / r^5, with an affine integration parameter.
                return -1.5*angularMomentumSquared*p/(r2*r2*sqrt(r2));
            }
            float GasHash(float3 cell)
            {
                // Integer hashing avoids the precision bands of sin-based hashes.
                uint3 n=asuint(int3(cell));
                uint h=n.x*1597334677u ^ n.y*3812015801u ^ n.z*2798796415u;
                h^=h>>16; h*=2246822519u; h^=h>>13;
                return (h & 0x00ffffffu)/16777216.0;
            }
            float GasNoise(float3 p)
            {
                float3 cell=floor(p), f=frac(p);
                f=f*f*f*(f*(f*6-15)+10);
                return lerp(
                    lerp(lerp(GasHash(cell),GasHash(cell+float3(1,0,0)),f.x),
                         lerp(GasHash(cell+float3(0,1,0)),GasHash(cell+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(GasHash(cell+float3(0,0,1)),GasHash(cell+float3(1,0,1)),f.x),
                         lerp(GasHash(cell+float3(0,1,1)),GasHash(cell+1),f.x),f.y),f.z);
            }
            float4 Disk(float3 p,float3 rayDirection,float pathLength)
            {
                float r=length(p.xz);
                float phase=atan2(p.z,p.x);
                float omega=rsqrt(2*r*r*r);
                float a=phase-_SimulationTime*omega;
                // Periodic cylindrical coordinates: long irregular structures
                // along the orbit, with no angular seam or repeating sine rings.
                float2 orbit=float2(cos(a),sin(a));
                float broad=GasNoise(float3(orbit*2.2,r*0.7));
                float warpedRadius=r+0.45*(broad-0.5);
                float3 flow=float3(orbit*3.5,warpedRadius*2.6);
                if(_VolumeEnabled>0.5) flow+=float3(p.y*1.7,-p.y*2.1,p.y*0.8);
                float turbulence=0.45*GasNoise(flow)+0.28*GasNoise(flow*2.03+17.1)
                                +0.19*GasNoise(flow*4.07+31.7)
                                +0.08*GasNoise(flow*7.9+float3(4,9,2));
                float density=lerp(0.35,1.5,smoothstep(0.18,0.83,turbulence));
                float filament=GasNoise(float3(orbit*13,warpedRadius*8)+p.y*2);
                density*=lerp(0.75,1.3,smoothstep(0.2,0.8,filament));
                float edge=smoothstep(3,3.35,r)*(1-smoothstep(_DiskOuter-1.8,_DiskOuter,r));
                // Zero-torque thin-disk-inspired radial emission: the inner
                // boundary fades in and the outer disk cools. Artistic units.
                float flux=pow(3/r,3)*max(1-sqrt(3/r),0)/0.05665;
                float heat=pow(saturate(flux),0.45);
                float3 temperature=lerp(float3(1,0.12,0.018),float3(1,0.72,0.38),heat);
                float3 tangent=normalize(float3(-p.z,0,p.x));
                float speed=rsqrt(max(2*(r-1),1));
                float doppler=1/(rsqrt(1-speed*speed)*(1-speed*dot(tangent,-rayDirection)));
                float redshift=sqrt(max(1-1/r,0));
                // Approximate bolometric beaming; gas density/temperature are artistic.
                float boost=clamp(pow(doppler*redshift,3),0.12,6);
                float alpha=1-exp(-1.6*density*edge/max(abs(rayDirection.y),0.25));
                if(_VolumeEnabled>0.5)
                {
                    float height=max(_DiskThickness,0.03)*(0.65+0.35*r/6);
                    float z=p.y/height;
                    float vertical=exp(-0.5*z*z)*(1-smoothstep(2.5,3.0,abs(z)));
                    // Normalized vertical density keeps face-on optical depth
                    // roughly stable as thickness changes. Beer-Lambert transfer.
                    float extinction=1.6*density*edge*vertical/(2.506628*height);
                    alpha=1-exp(-extinction*pathLength);
                }
                float3 emission=temperature*pow(flux,0.85)*density*boost*_Exposure*1.65;
                return float4(emission,alpha);
            }
            float3 OrbitSegment(float3 p,float3 v,float3 next,float3 nextV,float dt,float t)
            {
                // Cubic Hermite reconstruction removes the visible boundaries
                // caused by linearly intersecting different integration steps.
                float t2=t*t, t3=t2*t;
                return (2*t3-3*t2+1)*p+(t3-2*t2+t)*dt*v
                     +(-2*t3+3*t2)*next+(t3-t2)*dt*nextV;
            }
            float3 Trace(float2 uv,out float3 skyDirection,out float skyTransmission)
            {
                float2 q=(uv*2-1)*float2(_ViewAspect,1)*_TanHalfFov;
                float3 direction=normalize(_ViewForward.xyz+q.x*_ViewRight.xyz+q.y*_ViewUp.xyz);
                skyDirection=direction; skyTransmission=0;
                float3 p=_Observer.xyz;
                float radius=length(p);
                float3 radial=p/radius;
                float cosine=dot(direction,radial);
                // Convert the stationary observer's local direction into E=1 orbit data.
                float3 v=cosine*radial+(direction-cosine*radial)/sqrt(1-1/radius);
                float3 momentum=cross(p,v);
                float l2=dot(momentum,momentum);
                float3 color=0;
                float transmission=1;
                bool escaped=false;
                [loop] for(int i=0;i<_Steps;i++)
                {
                    float r=length(p);
                    if(r<1.015) return color;
                    if(r>60 && dot(p,v)>0) { escaped=true; break; }
                    float dt=clamp(r*0.025,0.015,0.65);
                    float3 acc=Acceleration(p,l2);
                    float3 next=p+v*dt+0.5*acc*dt*dt;
                    float3 nextV=v+0.5*(acc+Acceleration(next,l2))*dt;
                    if(_VolumeEnabled>0.5)
                    {
                        float maxHeight=max(_DiskThickness,0.03)*(0.65+0.35*_DiskOuter/6)*3;
                        // Conservative segment bounds also include rays lying
                        // exactly in the disk plane (the old surface misses these).
                        float reach=length(next-p)+length(acc)*dt*dt;
                        if(abs(p.y)<maxHeight+reach && length(p.xz)<_DiskOuter+reach
                           && length(p.xz)>3-reach)
                        {
                            int samples=(int)clamp(ceil(reach*_VolumeQuality/
                                        max(_DiskThickness*0.8,0.025)),2,12);
                            float3 previous=p;
                            [loop] for(int j=0;j<samples;j++)
                            {
                                float t=(j+0.5)/samples;
                                float3 samplePosition=OrbitSegment(p,v,next,nextV,dt,t);
                                float3 end=OrbitSegment(p,v,next,nextV,dt,(j+1.0)/samples);
                                float diskR=length(samplePosition.xz);
                                if(diskR>3 && diskR<_DiskOuter && abs(samplePosition.y)<maxHeight)
                                {
                                    float4 gas=Disk(samplePosition,normalize(lerp(v,nextV,t)),length(end-previous));
                                    color+=transmission*gas.rgb*gas.a;
                                    transmission*=1-gas.a;
                                }
                                previous=end;
                            }
                        }
                        if(transmission<0.001) return color;
                    }
                    else if(p.y*next.y<0)
                    {
                        float lo=0, hi=1;
                        [unroll] for(int refinement=0;refinement<10;refinement++)
                        {
                            float mid=(lo+hi)*0.5;
                            float y=OrbitSegment(p,v,next,nextV,dt,mid).y;
                            if((y>0)==(p.y>0)) lo=mid; else hi=mid;
                        }
                        float t=(lo+hi)*0.5;
                        float3 hit=OrbitSegment(p,v,next,nextV,dt,t);
                        float diskR=length(hit.xz);
                        if(diskR>3 && diskR<_DiskOuter)
                        {
                            float4 disk=Disk(hit,normalize(lerp(v,nextV,t)),0);
                            color+=transmission*disk.rgb*disk.a;
                            transmission*=1-disk.a;
                        }
                    }
                    p=next; v=nextV;
                }
                // Unresolved near-critical rays remain dark instead of leaking the sky.
                if(escaped) { skyDirection=normalize(v); skyTransmission=transmission; }
                return color;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float3 color=0;
                int count=clamp(_RaySamples,1,4);
                [loop] for(int sampleIndex=0;sampleIndex<count;sampleIndex++)
                {
                    // Fixed rotated-grid samples: no animated jitter or history ghosting.
                    float2 offset=0;
                    if(count==2) offset=sampleIndex==0?float2(-0.25,-0.25):float2(0.25,0.25);
                    if(count>2) offset=sampleIndex==0?float2(-0.375,-0.125):
                        sampleIndex==1?float2(0.125,-0.375):sampleIndex==2?float2(-0.125,0.375):float2(0.375,0.125);
                    float3 escapeDirection; float transmission;
                    float3 rayColor=Trace(input.uv+offset/_ScaledScreenParams.xy,escapeDirection,transmission);
                    // Derivatives are taken AFTER divergent ray integration, in
                    // uniform control flow. Mips filter the lensed sky footprint.
                    float footprint=max(length(ddx(escapeDirection)),length(ddy(escapeDirection)));
                    float lod=clamp(log2(max(1,footprint*512)),0,9);
                    float3 sky=SAMPLE_TEXTURECUBE_LOD(_SkyCube,sampler_SkyCube,escapeDirection,lod).rgb;
                    if(_HasSky<0.5) sky=Sky(escapeDirection);
                    color+=rayColor+transmission*sky*_SkyBrightness;
                }
                return float4(color/count,1);
            }
            ENDHLSL
        }
    }
}
