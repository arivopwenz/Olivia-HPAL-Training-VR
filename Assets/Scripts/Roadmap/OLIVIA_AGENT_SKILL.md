# OLIVIA VR HPAL Simulator — Agent Skill / Knowledge Pack

> **Purpose**: This document is a complete handoff for any AI assistant (ChatGPT, Claude, etc.) continuing development on the OLIVIA VR project. It captures the full project context, architecture, level flow, technical conventions, and gotchas learned through iteration. Read this before making changes.

---

## 1. Project Identity

- **Name**: OLIVIA VR — Operasi & Pelatihan VR HPAL Nikel
- **Type**: Unity VR simulator (XR Interaction Toolkit 3.4.1, XR Hands 1.7.3)
- **Goal**: Indonesian National Competition winner ("untuk Lomba Nasional!")
- **Domain**: HPAL (High Pressure Acid Leaching) nickel processing plant operator training
- **Total Levels**: 14 levels covering full HPAL flowsheet (Crusher → Dry Stack Tailings)
- **Showcase Levels** (priority for winning): Level 7 (Autoclave X-Ray) and Level 13 (Tailing)

---

## 2. Communication & Code Conventions

### Language Rules
- **Code, comments, classes, identifiers**: ENGLISH only
- **Chat replies, HUD messages, NPC voice**: INDONESIAN (casual style)
- User language style: casual Indonesian ("gas", "kerjain", "tolong"); gets frustrated when iterations fail repeatedly

### Visual / Audio Standards
- **Slurry color**: PURPLE — use `Assets/Materials/Color Utama/Slurry_Fill.mat` (RGBA `0.42, 0.18, 0.55, 0.95`). Never orange/yellow.
- **Pipe transparency**: `Pipe_Transparent.mat` at alpha `0.06–0.08` so liquid is visible inside.
- **Audio**: PROCEDURAL only. Use `AudioClip.Create` with sample-data generation (sine + noise + envelopes). No external audio assets.
- **Industrial materials**: Use `Industrial_*.mat` family in `Assets/Materials/Color Utama/` (BlueGrey, Steel, TankGrey, etc.). Created by copying `DCS Machine.mat` template via `AssetDatabase.CopyAsset` to preserve URP shader keywords.
- **Lighting gotcha**: If everything renders pink/magenta in scene, check `RenderSettings.ambientLight`. Bug seen at `RGBA(1, 0, 0.7)`. Fix: set `ambientMode = Skybox`, neutral colors, ensure a Directional Light with ~1.2 intensity exists.

### VR Movement Standard
- **NEVER** set `xrOrigin.transform.position` directly — XR Interaction Simulator overrides camera tracking and player snaps back.
- **ALWAYS** use:
  ```csharp
  xrOrigin.MoveCameraToWorldLocation(target.position + Vector3.up * xrOrigin.CameraYOffset);
  xrOrigin.MatchOriginUpCameraForward(Vector3.up, target.forward);
  ```
- See `LevelTeleportManager.cs` and `Teleport()` helpers in level controllers.

---

## 3. World Layout (Final, post-reposition)

Coordinates are world-space positions of key machines. Layout matches `Peta dan Alur.png` reference 1:1:

| Machine | World Pos | Notes |
|---|---|---|
| `Slurry Tank` | `(73.03, 2.46, 47.70)` | Area 4 — slurry preparation |
| `Crusher Ore` | `(84.25, 9.05, 48.47)` | Area 4 — ore crusher |
| `SlurryPump_Field` | `(80.00, 0.50, 42.00)` | Pumps slurry to PreHeater |
| `PreHeater_Field_1` | `(37.60, 0.66, 48.05)` | Area 5 — steam pre-heater (with `SteamValve_Handwheel`, `TempGauge`) |
| `AcidInjection_System` | `(5.00, 0.00, 48.00)` | Area 6 — H₂SO₄ tanks, dosing pump |
| `Autoclave_Field` | `(8.00, 0.00, 68.00)` | Area 7 — 22m × 6m horizontal cylinder, focal point |
| `Pipe_PreheaterToAutoclave` | origin (children at world pos) | 4 transparent segments connecting Area 5 → Area 7 |

### Spawn Points (all under `SpawnPoint/SpawnPoint_Level/`)
- `SpawnPoint_DCS`: `(-2.12, 8.36, 16.28)` — control room, used by most level transitions
- `SpawnPoint_Lvl3` / `SpawnPoint_Lvl3 - Slurry Tank`: Area 4 observation
- `SpawnPoint_Lvl4_Pump` / `SpawnPoint_Lvl4_Preheater`: Area 4–5 observation
- `SpawnPoint_Lvl6`: `(18, 2, 56)` — between PreHeater and Autoclave looking at Autoclave
- `SpawnPoint_Lvl7`: `(8, 10, 72.8)` — on Autoclave inspection platform

---

## 4. Level Flow (Full Pipeline)

### Level 1 — Onboarding (DCS Tutorial)
- Player learns to interact with DCS panels in control room.
- Ends with WT (Walkie Talkie) report; transition fades to Level 2.

### Level 2 — APD (Personal Protective Equipment)
- Locker room (`LockerHubController.cs`). Player wears helmet, gloves, etc.
- DCS monitor must be turned ON manually for Level 2 (configured via `DcsMonitorActivator`).

### Level 3 — Slurry Tank Mask & Fill (chest-grab respirator)
- **Mechanic**: Mask spawns at chest socket (via `TorsoChestAnchor.cs`), glows yellow, player grabs and equips.
- `TorsoChestAnchor` follows camera position+yaw but NOT pitch (so mask visible when looking down).
- Slurry tank fills 0% → 50% with `Level3OreSlurryController.cs` (18 second duration).
- `SlurryFXController` adds procedural splash audio + bubble particles.
- `DirectionArrowIndicator` shows 3D pulsing arrow toward tank.
- `LevelTransitionChoicePanel.cs` appears after final WT report: "Lanjut" or "Lihat Proses".
- `GameLevelManager` event: `OnLevel3LaporanAkhirDiterima`, flag `_tundaTransisiLevel3`, method `LanjutkanTransisiLevel3()`.

### Level 4 — Slurry Pump / DCS Flow Rate
- **DCS phase**: Player adjusts flow rate 1–450 m³/h via `+/-` buttons on DCS canvas (`FlowRateControlPanel.cs`).
- Quest auto-completes at 450 ± 10 (NO confirmation button — user explicitly rejected that approach).
- **Phase enum** (`Level4Phase` in `GameLevelManager`):
  ```
  Idle, MenungguTombolDcs, AturFlowRate, ObservasiPump,
  ObservasiPreheater, MenungguLaporanFlow, MenungguLaporanAkhir,
  KembaliKeDcs, Selesai
  ```
- **Field phase**: Player teleports to pump area, watches:
  - Pipe liquid fill animation using **purple `Slurry_Fill.mat`** (loaded from existing tank renderer at runtime).
  - `PipeFlowAnimator.cs` — wobble physics: UV scroll, scale pulse, position goyang, emission pulse.
  - Slurry tank Y-scale lerps from full (1.84) → end (0.5) parallel with pipe fill.
- **Audio**: Permanent `PumpMotor_Audio` GameObject in scene with procedural rumble (75 Hz + 220 Hz + filtered noise, looped, 2D `spatialBlend=0`).
- Flow ends → WT report → fade → DCS for Level 5.

### Level 5 — Steam Valve & Pre-Heater
- **Mechanic**: `Level5SteamValveController.cs` — VR hand-turn valve.
- `SteamValve_Handwheel` at `(31.5, 3.6, 46.5)` near PreHeater outlet — red wheel with 4 spokes + center knob, `XRGrabInteractable`.
- 4 full rotations (1440°) = 100% open = 200°C target.
- `TempGauge` analog needle rotates from 45° → -135° as temperature rises (25°C → 200°C).
- `Steam_FX` particle system (white smoke rising) scales with valve open %.
- Procedural steam hiss audio (white noise, high-pass filtered).
- Quest completes at suhu ≥ 180°C → WT report "katup steam terbuka, suhu naik".
- Keyboard test: hold `R` to simulate valve rotation.

### Level 6 — PreHeater→Autoclave Liquid Flow + Acid Injection
- **CRITICAL CORRECTION** (made mid-development):
  - Level 6 is **NOT** "acid injection only".
  - Phase A (Field): Liquid flows PreHeater outlet → through pipe segments → into Autoclave inlet.
  - Phase B (DCS): Acid system injects H₂SO₄ into Autoclave SEPARATELY.
  - Acid system does NOT receive slurry from PreHeater. It's an independent dosing line.
- **Controller**: `Level6AcidInjectionController.cs`
- **Phases**:
  1. Player observes slurry flow PreHeater → Autoclave (12s animated fill across pipe segments via `PipeFlowAnimator`).
  2. Slurry arrives at Autoclave inlet → HUD prompts return to DCS.
  3. Fade → teleport to DCS.
  4. Player presses `Btn_AcidPlus` / `Btn_AcidMinus` on DCS to set acid ratio to 350 kg/ton.
  5. pH drops 5.0 → 1.0; beaker hologram changes color green→yellow→orange→red (`UpdateBeakerColor()`).
  6. Quest completes at ratio 340–360 kg/ton AND pH ≤ 1.1 → WT report.
  7. Fade → Level 7.
- **Scene Objects**:
  - `Pipe_PreheaterToAutoclave` (4 segments, transparent material, route Down → AlongX → AlongZ → Connect)
  - `AcidInjection_System` (2 vertical H₂SO₄ tanks, walkway, dosing pump, yellow acid line to Autoclave)
- **Audio**: Procedural flow sound + acid pump sound (volume scales with ratio).

### Level 7 — Autoclave Monitoring + X-Ray Vision (SHOWCASE)
- **THE killer feature for the competition.**
- **Controller**: `Level7AutoclaveController.cs`
- **Flow**:
  1. Player teleports to inspection platform.
  2. HUD: "Inspect the Autoclave. Press X to activate X-Ray vision."
  3. Player presses X (or VR button) → `ToggleXRay()`:
     - Shell + EndCaps swap to transparent blue ghost material (alpha 0.12).
     - Inner fluid cylinder (purple slurry) becomes visible.
     - Agitator shaft visible spinning at 60 RPM on Y-axis.
  4. Player inspects 3 gauges (call `InspectGauge("pressure"/"temperature"/"rpm")`):
     - Pressure: 50 atm (target 45–50)
     - Temperature: 252°C (target 250–255)
     - RPM: 60 (target 60)
  5. After 3 gauges → quest complete → WT report.
  6. Fade → DCS for Level 8.
- **Auto-find references** from `Mesin Utama/Autoclave_Field` hierarchy (Shell, EndCap_Left/Right, AgitatorShaft).
- **Inner fluid**: cylinder at shell center, scale 0.85, with sine wobble on Y + slow rotation 15°/s for swirl effect.
- **Audio**: Reactor hum (50 Hz + 100 Hz sine) + agitator whir (180 Hz + blade-pass amplitude modulation).
- **X-Ray material**: URP/Lit transparent, blue tint `(0.3, 0.7, 1, 0.12)`, blue emission `(0.2, 0.5, 0.9) × 0.8`.

### Level 8+ (Future)
- Level 8: Flash Vessel / Letdown
- Level 9: CCD (Counter-Current Decantation) solid-liquid separation
- Level 10: Neutralization / Purification
- Level 11: MHP Product
- Level 12: Tailings Neutralization & Filter Press
- Level 13: Dry Stack Tailing (SHOWCASE — second priority for winning)
- Level 14: Emergency assembly / scenario

---

## 5. Architecture & Key Systems

### `GameLevelManager` (Singleton)
- Holds `CurrentLevel` enum, level data, target SOPs.
- Events:
  - `OnLevelStarted(GameLevel)` — main hook for level controllers
  - `OnLevel3LaporanAkhirDiterima` — Level 3 specific
- Setters: `SetSuhu(float)`, `SetTekanan(float)`, `SetRPM(float)`, `SetAcidRatio(float)`, `SetPH(float)`, etc.
- Method `MulaiLevel(GameLevel.LevelX)` for skip/test.

### `LevelTeleportManager.Teleport(target)`
- Wrapper that uses `XROrigin.MoveCameraToWorldLocation` + `MatchOriginUpCameraForward`.
- Disables `CharacterController` during teleport (re-enables after).

### `PlayerHUD`
- `ShowNotifPublic(string)` — main HUD message API.
- `PlayManualFade(duration)` — fade transition for level changes.

### `PipeFlowAnimator`
- MonoBehaviour added to liquid-fill cylinder GameObjects.
- `UpdateBaseScale()` / `UpdateBasePosition()` — called by controllers to animate fill, then PFA adds wobble physics.

### Level Controllers Pattern
Every level controller follows this structure:
```csharp
public class LevelXController : MonoBehaviour {
    private void Awake() { _hud = FindObjectOfType<PlayerHUD>(); AutoFindReferences(); }
    private void OnEnable() { GameLevelManager.OnLevelStarted += OnLevelStarted; }
    private void OnDisable() { GameLevelManager.OnLevelStarted -= OnLevelStarted; }
    private void OnLevelStarted(GameLevel level) {
        if (level == GameLevel.LevelX) { /* activate */ }
        else { /* deactivate */ }
    }
    private void AutoFindReferences() { /* GameObject.Find for null refs */ }
}
```

---

## 6. Build / Test Workflow

```
After script changes:
  mcp_unityMCP_refresh_unity (compile=request, scope=scripts, wait_for_ready=true)

Check for errors:
  mcp_unityMCP_read_console (types=["error","warning"])

Save scene (NOT during play mode):
  mcp_unityMCP_manage_scene (action=save)

Test in play mode:
  Use GameLevelManager.Instance.MulaiLevel(GameLevel.LevelX) for skip
```

### Code Execution Quirks
- `mcp_unityMCP_execute_code` runs in restricted method context.
- **No** top-level `using` statements — use `UnityEngine.GameObject.Find()` fully qualified.
- `Object` is ambiguous → use `UnityEngine.Object.DestroyImmediate()`.
- Some commands trigger safety blocks (e.g. `AssetDatabase.DeleteAsset`) — pass `safety_checks=false` if intentional.

---

## 7. Common Pitfalls / Lessons Learned

1. **Material instances lose URP keywords**: Creating `new Material(Shader.Find("URP/Lit"))` at runtime loses required keywords → renders as default white/wrong. Fix: `AssetDatabase.CopyAsset` from a working `.mat` template, then override color/properties.

2. **Pink/magenta everything**: Check `RenderSettings.ambientLight`. The scene had a bug `RGBA(1, 0, 0.7)`. Set to neutral or `AmbientMode.Skybox`.

3. **VR teleport snapping back**: Always use `XROrigin.MoveCameraToWorldLocation`, never `transform.position =`.

4. **DCS monitor activation**: Level 2 = manual ON, Level 3+ = auto ON. Configured in `DcsMonitorActivator.cs`.

5. **Confirmation buttons rejected**: User wanted Level 4 to auto-complete at 450 m³/h, NOT show a "HIDUPKAN PUMP" button.

6. **Failed loop recognition**: If an approach fails twice, step back and diagnose root cause. Don't tweak incrementally — find the actual issue (e.g. ambient light, not material instance).

7. **Sub/superscript glyphs**: TextMeshPro `LiberationSans SDF` doesn't have `₂` `₄` (Unicode subscripts). Use plain `H2SO4` instead of `H₂SO₄`.

8. **Map sprite is rotated 180°**: `Peta dan Alur` has `rot=(90, 180, 0)` on a SpriteRenderer. Text appears upside-down in Scene view from default top-down camera.

---

## 8. File Map (Critical Files)

```
Assets/Scripts/
├── Simulation/
│   ├── GameLevelManager.cs                    # Central state + events
│   ├── PhaseManager.cs
│   ├── Level3OreSlurryController.cs           # Slurry tank fill
│   ├── Level4SlurryPumpController.cs          # DCS flow rate + field obs
│   ├── Level5SteamValveController.cs          # Steam valve hand-turn
│   ├── Level6AcidInjectionController.cs       # Liquid flow + acid dosing
│   ├── Level7AutoclaveController.cs           # X-Ray vision + gauges
│   ├── PipeFlowAnimator.cs                    # Liquid wobble physics
│   └── SlurryFXController.cs                  # Splash audio + bubbles
├── System/
│   ├── TorsoChestAnchor.cs                    # Mask chest socket
│   ├── LevelTeleportManager.cs                # XR Origin teleport
│   ├── DcsMonitorActivator.cs                 # Per-level monitor logic
│   ├── LockerHubController.cs                 # Level 2 APD
│   └── LoadingScreenManager.cs
├── UI/
│   ├── DirectionArrowIndicator.cs
│   ├── LevelTransitionChoicePanel.cs
│   ├── FlowRateControlPanel.cs
│   └── PlayerHUD.cs
└── Roadmap/                                   # Reference docs (this file lives here)
    ├── BreakdownSistem.md
    ├── HPAL_DeepResearch.md
    ├── Olivia_Blueprint_Final.md
    ├── PROJECT_CONTEXT.md
    ├── Peta dan Alur.png                      # 3D map reference image
    └── OLIVIA_AGENT_SKILL.md                  # ← THIS FILE

Assets/Materials/Color Utama/
├── Slurry_Fill.mat                            # PURPLE slurry — universal liquid color
├── Pipe_Transparent.mat                       # alpha 0.06–0.08 for visible liquid
├── Mask_Respirator.mat                        # Level 3 chest mask
├── Fix_Yellow.mat                             # Industrial yellow pipes
├── DCS Machine.mat                            # URP/Lit template (copy this)
└── Industrial_*.mat                           # Generated from DCS template
```

---

## 9. Logic Verification Checklist (Level 5–7 chain)

**Level 5 → Level 6 transition**
- [ ] Level 5 completes when `_temperatureCurrent >= 180`
- [ ] Player reports via WT → `GameLevelManager` advances to Level 6
- [ ] `OnLevelStarted(Level6)` fires → Level6Controller activates
- [ ] Player teleports to `SpawnPoint_Lvl6` field observation point

**Level 6 internal flow**
- [ ] Phase A: `SequenceSlurryFlowToAutoclave` coroutine runs
- [ ] `AnimateLiquidFillPipes` iterates 4 pipe segments with PipeFlowAnimator wobble
- [ ] Each segment fills sequentially (`durationPerSegment = 12s / 4`)
- [ ] On last segment complete: `_slurryArrivedAtAutoclave = true`
- [ ] Fade + teleport to `SpawnPoint_DCS`
- [ ] Phase B: `_btnAcidPlus` `_btnAcidMinus` accept clicks (gated by `_slurryArrivedAtAutoclave`)
- [ ] Each click changes `_acidRatioCurrent` by `_acidStepPerClick` (default 10)
- [ ] pH lerps from 5.0 → 1.0 as ratio approaches target 350
- [ ] `UpdateBeakerColor` smoothly interpolates pH7→pH4→pH2→pH1 colors
- [ ] `CheckAcidQuest`: passes when `|ratio - 350| ≤ 10` AND `pH ≤ 1.1`

**Level 6 → Level 7 transition**
- [ ] WT report after acid quest complete → `GameLevelManager` advances to Level 7
- [ ] `OnLevelStarted(Level7)` fires
- [ ] Level7Controller resets X-Ray flag, gauge counters, restores shell material
- [ ] Player teleports to `SpawnPoint_Lvl7` (inspection platform)

**Level 7 internal flow**
- [ ] Reactor + agitator audio loop starts
- [ ] Agitator shaft rotates 60 RPM on X-axis (`degPerSec = 360`)
- [ ] X key (or VR button) → `ToggleXRay()` swaps materials, shows inner fluid
- [ ] Inner fluid wobbles via sine on Y-scale + 15°/s rotation
- [ ] `InspectGauge("pressure"/"temperature"/"rpm")` increments `_gaugesInspected`
- [ ] After 3 unique gauges → `_questComplete = true`
- [ ] WT report → fade → Level 8

**Known integration points**
- `GameLevelManager.SetSuhu/SetTekanan/SetRPM` are called by Level 7 on activation to seed values for HUD/DCS displays.
- `_xrayMaterial` is auto-created at Awake if null (transparent blue ghost).
- `_innerFluid` is auto-created if null, parented to Autoclave_Field, uses Slurry_Fill.mat.

---

## 10. MCP Tool Group Configuration (Optimized)

**Enabled** (essential for Unity dev): `core` (25 tools)
**Disabled** (rarely needed, declutter): `animation`, `docs`, `probuilder`, `profiling`, `scripting_ext`, `testing`, `ui`, `vfx`

> Note: `scripting_ext` includes `execute_code` which IS frequently used for runtime scene manipulation. Re-enable if heavy scripting work is needed.

To toggle: use `mcp_unityMCP_manage_tools` with `action=activate`/`deactivate`, `group=<name>`.

---

## 11. Quick Reference — User Preferences

- **Tone**: Speak Indonesian casually in chat. Never lecture. Be concise.
- **Action bias**: Default to action. Don't over-explain. If multi-step, do all steps autonomously.
- **Code language**: English only.
- **No tests unless asked.**
- **Verify before claiming done**: After scene edits, screenshot or read positions. After script edits, refresh + read console.
- **Scene saves**: ONLY save when not in play mode.

---

*Last updated: After Level 7 reposition + Area 6/7 layout matching to `Peta dan Alur.png`. World coordinates verified.*
