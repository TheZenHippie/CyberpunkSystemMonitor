# CyberpunkSystemMonitor — Project Handoff & Architecture Memory

> **Date:** September 15, 2026  
> **Repository:** `D:\Other Coding Projects\CyberpunkSystemMonitor`  
> **Author & Company:** TheZenHippie  
> **Target Framework:** .NET 8.0 Windows (`net8.0-windows`), C# 12, WPF  
> **Status:** v1.0.0 Full Release Complete & Verified  

---

## 📌 Executive Summary

**`CyberpunkSystemMonitor`** is a high-performance, data-dense telemetry HUD desktop widget crafted in C# and WPF (.NET 8). It joins the **Cyberpunk Desktop Widget Family** (`CyberClockWidget`, `CyberpunkTerminalWidget`, `CyberpunkSlideshowWidget`, and `CyberPunkNoteWidget`).

It provides real-time system monitoring across CPU, individual core matrices, RAM & commit charge, GPU & dedicated VRAM, storage drives with pulsing real-time I/O diodes, network download/upload speedometers, and a live sorted top processes table — all wrapped in retro cyberpunk CRT scanlines, chromatic glitch, analog TV static snow, and continuous RGB spectrum cycling.

---

## 🧱 Architecture & Design DNA

### 1. Vector LED Matrix Engine (`Controls/LedMatrixStatElement.cs` & `LedFont.cs`)
- **Procedural 5x7 & 3x5 Glyph Matrix:** Vector dot-matrix font renderer supporting digits, letters, punctuation, arrows, and custom symbols.
- **Physical Diode Chassis Look:** Renders unlit background diode rings across all matrix positions, replicating physical retro LED message boards.
- **3-Tier Optical Diode Rendering:**
  1. *Outer Neon Bloom Halo:* Soft glowing halo matching active HUD colors.
  2. *High-Intensity Diode Core:* Solid vector LED emitter.
  3. *Specular Dome Highlight:* Convex glass diode top-left specular highlight.
- **Zero-Allocation Rendering:** Vector brushes and pens are frozen (`Freeze()`) for peak performance.

### 2. Segmented Cyber Meter Engine (`Controls/CyberBarMeter.cs`)
- Segmented multi-block level meter with customizable segment counts, spacing, and multi-threshold color gradients (e.g. Cyan $\rightarrow$ Amber $\rightarrow$ Red for high load).
- Dynamic warning & critical color transitions based on utilization thresholds.

### 3. Pulsing Real-Time Disk Activity Diode (`Controls/RoundLedIndicator.cs`)
- Pulsing circular LED diodes indicating active disk **READ** (Neon Green/Cyan) and **WRITE** (Pink/Amber) events with live transfer rate metrics (KB/s and MB/s).

### 4. Native Telemetry Pipeline (`Services/SystemMetricsService.cs`)
- **Low-Overhead Native Telemetry:** Uses native `GlobalMemoryStatusEx` Win32 API via `kernel32.dll` for sub-millisecond RAM calculation.
- **Hardware Detection:** Instantaneous Registry queries for CPU hardware naming, WMI fallback for GPU detection, and Windows Performance Counters for active I/O throughput.
- **Single-Pass Process Enumeration:** Queries system processes in a single pass, computing CPU load, memory consumption, thread counts, and handle counts with minimal GC overhead.
- **Asynchronous Non-Blocking Workers:** All metrics collection executes asynchronously inside background workers (`Task.Run`), keeping the WPF UI thread at silky 60+ FPS.

### 5. Cyberpunk Shell & CRT Visual Effects Suite
- **HUD Accent Colors:** Presets for *Electric Cyan*, *Matrix Green*, *Solar Amber*, *Synthwave Magenta*, *Night City Violet*, *Glitch Red*, *Phosphor White*, or custom Hex.
- **RGB Spectrum Cycling:** Smooth 360° HSV hue rotator with speed slider.
- **CRT Effects Suite:**
  - *Vintage CRT Scanlines:* Horizontal raster with density slider.
  - *CRT Glitch & Jitter:* Retro chromatic sync jitter with intensity control.
  - *CRT TV Snow Static:* Procedural multi-frame analog TV static noise with opacity control.
  - *Cyberpunk Rainbow Border:* Animated rotating RGB spectrum border with thickness slider.
- **Session Persistence:** Window position, size, opacity, tint, colors, and polling intervals automatically saved to `%APPDATA%\CyberpunkSystemMonitor\settings.json`.
- **Zero-Footprint Cleanup:** Disposes all performance counters and worker threads, draining GC finalizers via `App.CleanProcessExit()` to guarantee zero lingering processes in Windows Task Manager.

---

## 📦 Release Packages & Assets

### Distribution Packages (`release/`)
1. **`CyberpunkSystemMonitor-v1.0.0-win-x64-FrameworkDependent.zip`** (3.7 MB)  
   Lightweight single-file binary for systems with .NET 8 Desktop Runtime installed. Contains executable, PDB, README, LICENSE, and `icon.png`.
2. **`CyberpunkSystemMonitor-v1.0.0-win-x64-Standalone.zip`** (66.57 MB)  
   Standalone batteries-included package with .NET 8 runtime bundled. Requires no preinstalled dependencies.
3. **`SHA256SUMS.txt`**  
   Cryptographic SHA-256 integrity verification hashes.

### Local Staging Directories (`publish/`)
- `publish/framework-dependent/`
- `publish/standalone/`

### Automated CI/CD Workflow
- `.github/workflows/release.yml`: Automatically builds, packages, and creates GitHub Releases upon pushing version tags (e.g. `v1.0.0`) or via manual `workflow_dispatch`.

---

## 🔄 How to Resume in a Future Session

When starting a future session, simply tell Antigravity:
> *"I want to continue work on CyberpunkSystemMonitor in D:\Other Coding Projects\CyberpunkSystemMonitor. Read PROJECT_HANDOFF.md."*

