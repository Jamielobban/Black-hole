# Black Hole Light Lab

Unity 6000.3.23f1 / Universal Render Pipeline 17.3.0.

Open `Assets/BlackHole/BlackHoleExplorer.unity` and press Play. If the scene has
not been generated, use **Black Hole > Create or Open Explorer** in Unity.
The original SampleScene is unchanged. The lab has its own renderer, appended
to both template pipeline assets without changing their default renderer.

## Controls

- Right mouse drag: orbit; mouse wheel outside the control panel: zoom.
- Space: pause/resume procedural gas animation.
- R: reset the camera; H: hide/show controls.
- Sliders: observer distance, viewing angle, outer disk radius, brightness, speed.
- Fast/Balanced/Fine: 300/480/720 integration steps per pixel.
- The same presets trace 1/2/4 fixed subpixel rays to reduce ring and disk aliasing.
- Volumetric gas: enabled by default; disable for the previous surface shader.
- Gas thickness: vertical density scale height at 6 Rs (0.06 to 0.6 Rs).
  Fine quality also doubles the requested volume sampling density.
- Sky brightness: controls the galactic backdrop independently of gas brightness.
- The scrollable panel reports a smoothed frame rate and frame interval.

## What is simulated

The fullscreen HLSL shader integrates Schwarzschild null orbits in a fixed,
non-rotating spacetime, in units where the horizon radius Rs is 1. The spatial
orbit equation is `x'' = -1.5 L² x / r⁵`; initial conditions transform the
stationary observer's local viewing direction into affine orbit data with E=1.
A velocity-Verlet integrator advances rays with smaller steps near the hole.
Rays crossing the horizon are absorbed; escaping rays sample a procedural sky.
The disk starts at 3 Rs (the Schwarzschild innermost stable circular orbit).

This is an educational real-time optical model, not a validated scientific
solver. The disk's noise, density, transparency, emission spectrum and rotation
animation are procedural. Doppler beaming and gravitational redshift are
approximate brightness effects; there is no fluid dynamics, magnetic field,
black-hole spin, travel-time delay, or relativistic moving camera. Disk crossings
use a cubic Hermite orbit segment and a refined plane intersection. Finite step counts and a 60 Rs escape
boundary introduce numerical error, especially near the photon ring. Unresolved
rays stay dark. A baked HDR cubemap supplies the galactic clouds, dust lanes,
and clustered stars. Its mip level follows the escaping-ray footprint, evaluated
after ray integration. Fixed subpixel rays reduce photon-ring aliasing without
temporal jitter or accumulated history. Extreme lensing and very thin features
can still shimmer; this is spatial filtering, not temporal antialiasing.

The sky is generated deterministically with **Black Hole > Bake Deep Sky** and
stored in `Assets/BlackHole/Resources/BlackHoleSky.asset`. It uses direction-space
cloud noise and stars projected onto each overlapping cube face. No external
textures or real astronomical catalogue are used. A simple procedural starfield
is kept as a fallback if the baked asset is missing. Filtering can soften highly
magnified stars; this is not a physically accurate stellar beam-tracing model.

The disk uses smooth, periodic 3D noise stretched along orbital directions,
without regular sine-wave rings. A thin-disk-inspired radial emission profile
fades at the inner boundary and cools outward. Density and viewing angle set
opacity instead of applying the same transparency at every crossing.

The default disk is now a thin volume with a Gaussian vertical density profile,
gently increasing thickness outward, and noise varying through its height.
Curved orbit segments are subdivided near the gas; midpoint samples accumulate
emission and Beer-Lambert absorption front to back. This works even for rays
lying exactly in the disk plane. Density is vertically normalized so changing
thickness does not simply increase face-on opacity. Rendering stops after the
remaining transmittance drops below 0.001. The original surface-intersection
path remains available through the Volumetric gas toggle.

This is volumetric rendering of procedural gas, not fluid simulation or full
relativistic radiative transfer. Path lengths use coordinate-space distances,
scattering is omitted, the vertical profile is truncated at three scale heights,
and sampling is capped at 12 samples per orbital segment. Fine lensed structures
and very thin gas can still alias. Volume rendering costs more than the surface
fallback, particularly at grazing angles.

The fullscreen pass renders a self-contained space view, not arbitrary Unity
scene geometry. It intentionally replaces the camera image before bloom and
ACES tone mapping. Other scene objects are not lensed or composited. Keep the
dedicated camera for this scene; the material currently holds one observer.

## Main files

- `Assets/BlackHole/Shaders/BlackHole.shader`: light integration, sky, disk.
- `Assets/BlackHole/Scripts/BlackHoleExplorer.cs`: observer and live controls.
- `Assets/BlackHole/Editor/BlackHoleSetup.cs`: scene/material/renderer creation.
- `Assets/BlackHole/Editor/BlackHoleSkyBaker.cs`: deterministic 1024-pixel HDR cube faces and mipmaps.
- `Assets/BlackHole/Editor/BlackHolePlayValidation.cs`: isolated batch Play-mode motion/profile checks.

No hardware ray-tracing support or VFX Graph package is required. GPU cost scales
with resolution and integration steps; begin at 1280×720 with Balanced quality.
This is a desktop prototype; mobile and XR have not been validated.

## Verification

Compiled and rendered with Unity 6000.3.23f1. Preview captures at 12 and 55
degrees are in `Artifacts`. Six CPU reference integration checks verify
capture/escape either side of the theoretical critical impact parameter
`3 sqrt(3) / 2`; relative angular-momentum-squared error stayed below 0.001%.
These checks exercise the same integration equations as the shader, but are
not an independent scientific accuracy certification. Interactive input and
frame rate have not been measured in a foreground Play-mode session.

The earlier volume-only validation rendered both paths at 0, 12 and 55 degrees, plus volume scale
heights of 0.06 and 0.6 Rs edge-on. On the local RTX 3060 Ti, four synchronous
720p render/readback samples per view averaged 20.64–23.18 ms for volume and
23.64–28.12 ms for surface. This small offline sample includes CPU submission
and readback, is affected by caching/background load, and is not a GPU-only
benchmark or a gameplay FPS claim. Captures and timings are in
`Artifacts/volume-comparison`; rerun `BlackHole.Editor.BlackHoleSetup.ValidateVolume`
in an isolated batch editor to reproduce the checks without touching an open scene.

The sky/detail/antialiasing update was also tested in actual Play mode inside
an isolated batch editor, with an orbiting camera and a 1280x720 render target.
Each preset had 30 warm-up frames and 90 measured frames. On the RTX 3060 Ti:

| Preset | Rays/pixel | Mean frame interval | 95th percentile |
| --- | --- | --- | --- |
| Fast | 1 | 4.22 ms | 5.30 ms |
| Balanced (default) | 2 | 8.44 ms | 9.42 ms |
| Fine | 4 | 19.16 ms | 20.17 ms |

These intervals include editor overhead and are not GPU-only timings or a
standalone performance guarantee. There is no automatic quality switching.
Captures cover multiple orbit angles, edge-on viewing and another gas-animation
time; see `Artifacts/sky-comparison`. Spatial filtering reduces aliasing but
does not eliminate shimmer. A Unity Search startup indexing exception appeared
in the isolated editor log; the Play-mode test completed with exit code 0 and
no reported shader compilation errors. Manual foreground input remains untested.

References: [Eric Bruneton's black-hole renderer](https://ebruneton.github.io/black_hole_shader/)
and [NASA's visualization](https://www.nasa.gov/universe/nasa-visualization-shows-a-black-holes-warped-world/).
