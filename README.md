# Black Hole Light Lab

A real-time **black hole rendering experiment built in Unity URP**, focused on gravitational lensing, procedural accretion-disk rendering, volumetric gas, and procedural deep-space backgrounds.

The effect is implemented primarily in HLSL and traces light around a simplified Schwarzschild black hole in real time.

![Black Hole](Images/black-hole-edge-on.jpg)

---

## Visual Showcase

### Edge-On View

A near-equatorial view showing the accretion disk crossing the black hole and the rear section being gravitationally lensed around it.

![Black Hole Edge-On](Images/black-hole-edge-on.jpg)

### High-Angle View

A higher observer angle showing how the apparent shape of the accretion disk changes with viewpoint.

![Black Hole High-Angle](Images/black-hole-high-angle.jpg)

---

## Features

- Real-time gravitational lensing
- Schwarzschild-based ray integration
- Event-horizon absorption
- Procedural accretion disk
- Volumetric gas rendering
- Approximate Doppler and gravitational-redshift effects
- Procedural HDR deep-space background
- Multiple quality presets
- Sub-pixel supersampling
- Interactive orbit and zoom controls
- Unity editor tools for setup and sky generation

---

## Rendering

Each screen-space ray is numerically integrated around the black hole.

During the process, the renderer checks whether the ray:

1. Crosses the event horizon
2. Intersects the accretion disk
3. Escapes back into space

Disk intersections contribute emission and absorption, while escaping rays sample the procedural background sky.

```text
Camera Ray
    │
    ▼
Gravitational Integration
    │
    ├── Event Horizon → Absorbed
    │
    ├── Accretion Disk → Emission / Absorption
    │
    ▼
Escaping Ray
    │
    ▼
Procedural Sky
    │
    ▼
Final Image
```

---

## Accretion Disk

The disk is generated procedurally rather than using a traditional textured mesh.

Its appearance is influenced by:

- Disk density
- Radial emission
- Procedural gas noise
- Observer angle
- Gas thickness
- Animation speed
- Doppler-style brightness variation
- Gravitational-redshift approximation

The project supports both surface-based and volumetric disk rendering.

---

## Quality Presets

| Preset | Integration Steps | Rays / Pixel |
| --- | ---: | ---: |
| Fast | 300 | 1 |
| Balanced | 480 | 2 |
| Fine | 720 | 4 |

Higher quality settings improve thin disk details and photon-ring stability at additional GPU cost.

---

## Controls

| Input | Action |
| --- | --- |
| Right Mouse Drag | Orbit |
| Mouse Wheel | Zoom |
| Space | Pause / resume |
| R | Reset camera |
| H | Hide / show UI |

---

## Project Structure

```text
Assets/BlackHole/
│
├── BlackHoleExplorer.unity
├── BlackHoleRenderer.asset
├── BlackHoleVolume.asset
│
├── Editor/
│   ├── BlackHolePlayValidation.cs
│   ├── BlackHoleSetup.cs
│   └── BlackHoleSkyBaker.cs
│
├── Resources/
│   └── BlackHoleSky.asset
│
├── Scripts/
│   └── BlackHoleExplorer.cs
│
└── Shaders/
    └── BlackHole.shader
```

---

## Requirements

- Unity 6000.3.23f1
- Universal Render Pipeline
- Desktop GPU recommended
- Git LFS

The large generated HDR sky asset is stored through Git LFS.

---

## Quick Start

Clone the repository:

```bash
git clone https://github.com/Jamielobban/Black-hole.git
```

Open:

```text
Assets/BlackHole/BlackHoleExplorer.unity
```

and enter Play Mode.

---

## Limitations

This is a real-time graphics experiment rather than a complete astrophysical simulation.

It does not currently simulate Kerr spin, fluid dynamics, magnetic fields, or full relativistic radiative transfer.

---

## Author

Created by **Jamie Lobban**.
