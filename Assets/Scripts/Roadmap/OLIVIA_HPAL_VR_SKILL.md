# Olivia HPAL VR — AI Agent Skill Guide

**Project**: Olivia VR — HPAL (High Pressure Acid Leaching) nickel plant operator training simulator
**Engine**: Unity 6 + URP + XR Interaction Toolkit 3.4.1 + Input System 1.18
**Workspace**: `C:\Users\mp2dz\Olivia`
**Main scene**: `Assets/Scenes/Level1.unity`
**Domain**: HPAL nickel processing plant (Crusher → Slurry Tank → Pre-Heater → Autoclave → Flash Vessel → CCD → MHP → Tailing → Dry Stack → K3 Emergency)

---

## 1. Architecture Overview

### Core Singletons
- **`GameLevelManager`** (`Assets/Scripts/Simulation/GameLevelManager.cs`): central state machine for 14 levels (0-14). Holds level data, voice keyword config, parameter state (suhu, tekanan, RPM, pH, AcidRatio, FlowRate), and per-level flag bools (e.g. `_level5PreheaterReady`, `_level6OutletReportDone`, `_level7XrayActivated`). Fires events: `OnLevelStarted`, `OnLevelComplete`, `OnDCSButtonPressed`, `OnVoiceReportAccepted`, `OnLevel3PhaseChanged`, `OnLevel4PhaseChanged`, `OnDCSButtonShouldHighlight`, `OnLevelTransitionRequested`.
- **`PhaseManager`** (`Assets/Scripts/Simulation/PhaseManager.cs`): tracks 8 APD items (Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, Respirator, Earplug, WalkieTalkie). Has logic to lock grab on equipped APD (`KuncIGrabAPD`), hide displayed APD (`SembunyikanApdDiMeja`), and auto-pin respirator to chest socket (`PastikanMaskerAdaDiSocketBaju`).
- **`WalkieTalkieManager`** (`Assets/Scripts/Simulation/WalkieTalkieManager.cs`): voice recognizer + PTT. Has `_walkieTalkieInHand` reference. `TampilkanHT(true/false)` shows/hides walkie at right hand anchor.
- **`PlayerHUD`** (`Assets/Scripts/UI/PlayerHUD.cs`): right-side quest panel. `UpdateOperasionalChecklist(level)` builds `[OK]/[ ]` checklist per level. Subscribes to GLM events.

### Per-Level Controllers
Each level has its own controller in `Assets/Scripts/Simulation/`:
- `Level3OreSlurryController.cs`, `Level4SlurryPumpController.cs`, `Level5SteamValveController.cs`, `Level6AcidInjectionController.cs`, `Level7AutoclaveController.cs`, `Level8MonitoringController.cs`, `Level9FlashVesselController.cs`, `Level10CCDController.cs`, `Level11MHPController.cs`, `Level12TailingFilterController.cs`, `Level13DryStackController.cs`, `Level14EmergencyController.cs`.

Each controller listens to GLM events, runs its own phase state machine, and notifies GLM via `Notify*` methods to advance level flags.

### UI Systems
- **`UniversalTaskMarker`** (`Assets/Scripts/UI/UniversalTaskMarker.cs`): single-instance marker that shows yellow 3D arrow + outline wireframe box on the active task target. `ResolveTarget()` returns target Transform per level + sub-phase. `FindByName` includes inactive objects. `IsChildOfPlayer` filter prevents marker on player-attached objects (walkie in hand, etc).
- **`DirectionArrowIndicator`** (`Assets/Scripts/UI/DirectionArrowIndicator.cs`): legacy 3D arrow component used by `TaskArrowDirector` (DCS buttons) and `GlobalTaskArrowDirector` (non-DCS). `_panahDinonaktifkan` must be `false` for arrows to show.
- **`Level1ApdTaskHintDirector`** (`Assets/Scripts/UI/Level1ApdTaskHintDirector.cs`): legacy Level 1 hint, **disabled** in `LateUpdate()` since `UniversalTaskMarker` handles Level 1.

---

## 2. Level Mechanic Reference

### Level 0 — Tutorial
Onboarding controls.

### Level 1 — APD Safety
Pakai 8 APD wajib di rak `Socket_Scanner_*`. Respirator di rak (`Socket_Scanner_RespiratorMask`) — tidak langsung di dada.

### Level 2 — DCS Prep
Lihat DCS monitor → tekan tombol DCS 2 → lapor HT.

### Level 3 — Ore & Slurry
DCS 3 → lapor awal → teleport ke field → ore reaches slurry tank → slurry 75% → lapor akhir. Phase enum `Level3Phase`.

### Level 4 — Slurry Pump
DCS 4 → set flow rate 450 m³/h via panel `Btn_FlowPlus/Minus` → lapor "slurry pump aktif" → observasi pipe → slurry sampai pre-heater → lapor "cairan sudah di preheater". Phase enum `Level4Phase`.

### Level 5 — Steam Valve
DCS 5 → lapor "aktifkan pre-heater" → teleport ke field preheater → grab handwheel `RealSteamValve_Pivot_Lvl5` → putar (suhu naik 25→200°C) → suhu ≥180°C → `NotifyLevel5PreheaterReady` → lapor "katup steam terbuka". Steam particle FX + audio mendesir, gauge needle naik proporsional. `_validasiApdLapangan` default false (player tidak diblock).

**Hotkey debug**: `R` = buka valve, `F` = tutup. Grab valve dengan controller juga auto-buka perlahan (fallback rotation).

### Level 6 — Acid Injection (PALING KOMPLEKS)
**Alur 6 fase**:
1. **DCS 6** ditekan
2. **Lapor "outlet preheater dibuka"** → teleport ke field preheater handwheel
3. **Putar handwheel preheater** (auto-find `L5_Condensate_Drain_Handwheel_Hub` terdekat dengan SpawnPoint_Lvl6, lalu group 6 part `Hub + OuterRing + 4 Spokes` ke pivot baru `L6_SlurryValve_Pivot_Runtime`). Saat valve full open → cairan ungu animasi mengalir di pipa + audio `_flowAudio` + autoclave terisi via `AnimateAutoclaveFill`.
4. **Lapor "slurry masuk autoclave"** → teleport balik ke DCS
5. **DCS Acid Setup**: panel runtime `L6_DCS_AcidControlPanel_Runtime` muncul dengan 6 tombol:
   - `Btn_AcidPlus/Minus` → +/-10 kg/ton (target 350)
   - `Btn_AcidStrokePlus/Minus` → +/-5% stroke (target 70%)
   - `Btn_AcidTankSelect` → SWAP A/B
   - `Btn_AcidArm` → ARM toggle
   Saat ratio + stroke + ARM lengkap → `NotifyLevel6DcsAcidRatioReady` → auto teleport ke acid skid.
6. **Acid Skid (field)**: 2 mushroom button runtime di sebelah `Transparent_CalibrationColumn`:
   - `L6_AcidSkid_BtnLocalStart_Runtime` (hijau): tekan → pump nyala
   - `L6_AcidSkid_BtnLeakOk_Runtime` (biru): tekan setelah 8 detik leak inspection → cairan amber naik di calibration column → autoclave penuh → `NotifyLevel6AcidInjectionComplete`
7. **Lapor "acid aktif"** → Level 7

**Tidak ada acid valve** di acid skid (dihilangkan). Player hanya tekan 2 button.

**State flags GLM**: `_level6OutletReportDone`, `_level6SlurryMasukAutoclave`, `_level6SlurryReportDone`, `_level6DcsAcidReady`, `_level6AcidComplete`.

**Hotkey debug**: keyboard `+/-`, `[/]`, `T`, `A`, `G` (LOCAL START), `H` (LEAK OK).

### Level 7 — Autoclave Inspection (SHOWCASE)
**6 mekanik VR-native**:
1. **X-Ray Vision** (X) + **3 layer** (C): Slurry Flow / Heat Map / Scale Buildup
2. **Scale Mark** (M) — tag 3 spot scale buildup di kompartemen
3. **Cluster Gauge Reading** + **Logbook** (L) — baca 3 gauge analog, submit logbook
4. **Sample Port** (V toggle valve, B take sample) — auto-close valve setelah sample
5. **Safety Drill** (S 4x) — konfirmasi PSV → ESD → Quench → Exit
6. **Voice Report** "autoclave normal, suhu 250, tekanan 50, agitator 60 RPM"

**Notify methods**: `NotifyLevel7XrayActivated`, `NotifyLevel7ScaleMarked`, `NotifyLevel7GaugesLogged`, `NotifyLevel7SampleTaken`, `NotifyLevel7SafetyDrillDone`. Helper `TryCompleteLevel7Inspection` set `_level7AutoclaveInspected = true` saat semua 5 lengkap.

### Level 8 — Monitoring DCS
Stabilkan parameter (suhu 250-255, tekanan 45-50, RPM 60). `ParameterAutoklaveSesuaiSOP()`.

### Level 9-13 — Pengolahan Lanjut
Flash Vessel (letdown) → CCD (separator) → MHP (presipitasi MgO) → Tailing Filter Press → Dry Stack (limestone netralisasi).

### Level 14 — K3 Emergency
Alarm → ESD button → shutdown.

---

## 3. Voice Report Format

GLM has `kataKunciVoiceAwal` (initial report) and `kataKunciVoice` (final report) per level. Validation in `HandleVoice*` methods. PlayerHUD dynamically shows `txtHintKataKunci` with expected keyword.

Level 5/6 have **multiple voice reports**:
- Level 5: "aktifkan pre-heater" (initial) → "katup steam terbuka" (final)
- Level 6: "outlet preheater dibuka" → "slurry masuk autoclave" → "acid aktif" (3 reports total)

PlayerHUD tracks intermediate reports via per-level flags (e.g. `_level5LaporanAwalDone`). `_voiceReportSelesai` only set true on FINAL report — checklist `[OK] Lapor HT akhir` uses guard `&& _voiceReportSelesai`.

---

## 4. UniversalTaskMarker Target Resolution

Per level, the marker resolver returns the next target Transform. Examples:
- **Level 5**: DCS button → `RealSteamValve_Pivot_Lvl5` → walkie talkie
- **Level 6**: DCS button → walkie → `L6_SlurryValve_Pivot_Runtime` → walkie → DCS acid buttons → acid skid mushroom buttons → walkie
- **Level 7**: DCS button → autoclave shell (X-Ray) → scale spot → logbook → sample port → PSV → walkie

**Fallback**: `FindByName` searches all transforms including inactive. **Filter**: `IsChildOfPlayer(target)` returns true if any parent's name contains "XR Origin"/"XR Rig"/"PlayerRig" — marker hides for player-attached objects.

---

## 5. Common Pitfalls Encountered

### Material rendering (URP/Lit runtime)
URP/Lit shader requires `_Surface=0`, `_ZWrite=1`, `_SrcBlend=One`, `_DstBlend=Zero`, `RenderType=Opaque`, queue 2000 untuk solid cube body. Without these, runtime-created cubes render transparent/invisible.
**Reference**: `Level6AcidInjectionController.CreateOpaqueMat()`.

### TextMeshPro 3D vs UGUI
TMP 3D (`TextMeshPro` component) requires font asset assigned. Without it, text empty. Workaround:
1. Create `TextMeshProUGUI` (UGUI variant) under hidden Canvas with `CanvasGroup.alpha=0`
2. Add `TextMesh` (legacy 3D) as visible
3. Use `L6PanelTextSyncer` MonoBehaviour to copy `TMP.text` → `TextMesh.text` each frame
**Reference**: `Level6AcidInjectionController.CreatePanelDisplay()`.

### XRGrabInteractable on Mesh-Only GameObject
Imported GLB/FBX meshes (e.g. `L5_Condensate_Drain_Handwheel_Hub`) only have MeshFilter+Renderer. To make grabable:
1. `GetComponent<Collider>() ?? AddComponent<SphereCollider>()` (set radius)
2. `GetComponent<Rigidbody>() ?? AddComponent<Rigidbody>()` (set isKinematic=true, useGravity=false)
3. `GetComponent<XRGrabInteractable>() ?? AddComponent<XRGrabInteractable>()`
4. Remove conflicting `XRSimpleInteractable` jika ada
**Reference**: `Level6AcidInjectionController.EnsureInteractable()`.

### Group multi-part mesh into single rotation pivot
Blender import sering split handwheel jadi Hub + OuterRing + 4 Spokes sebagai siblings. Untuk rotate seluruh stir:
1. Cari hub terdekat (anchor)
2. Filter siblings dengan distance < 1m dari hub
3. Buat empty pivot di posisi hub
4. `SetParent(pivot, worldPositionStays: true)` semua part
5. Rotate pivot
**Reference**: `Level6AcidInjectionController.FindNearestSlurryHandwheel()`.

### Auto-find by name false positive
`FindTransformContains("Dosing_Handwheel")` matches `MGO_Dosing_Handwheel` (Level 11) AND `Dosing_Handwheel` (Level 6). Use full unique names atau distance-based filtering. Lesson learned di Level 6 acid valve.

### `_panahDinonaktifkan` default true
`DirectionArrowIndicator` ships with `_panahDinonaktifkan = true` (arrows disabled). Set to false for arrows to render. UniversalTaskMarker doesn't depend on this — has own visuals.

### Voice report accept loops teleport
`OnVoiceReportAccepted` fires for EVERY accepted report. Without guards, level controller akan teleport berulang-ulang. Always guard with phase/flag (`!_questTercapai && !_fieldSudahDibuka`).

### Walkie talkie grab disabled
`WalkieTalkieManager.TampilkanHT(true)` disables `XRGrabInteractable` to prevent "snap to socket". `TampilkanHT(false)` MUST re-enable grab + colliders + GameObject active.

### Respirator masker pinned to chest
`PhaseManager.OnLevelStarted` pins respirator to `Socket_Respirator_Baju` di chest setiap level start. **Special case**: Level 1 player belum pakai → pin ke `Socket_Scanner_RespiratorMask` (rak APD) instead. Check `OnLevelStarted` Level 1 branch.

---

## 6. Debug ContextMenu Skip Methods

`GameLevelManager` has `[ContextMenu]` debug methods (right-click component in Inspector):

- DEBUG: Selesaikan Level Ini
- DEBUG: Trigger Emergency
- DEBUG: Pindah ke Level Berikutnya
- DEBUG: Skip ke Level 3 (Auto-equip APD dasar)
- DEBUG: Skip ke Level 4 (Flow Rate)
- **DEBUG: Skip ke Level 5 (Steam Valve)** — auto APD + DCS 5 + lapor awal
- **DEBUG: Skip ke Level 6 (Acid Injection)** — auto APD + DCS 6 + lapor outlet
- **DEBUG: Skip ke Level 6 - Acid Skid (Field)** — sub-skip langsung ke acid skid
- **DEBUG: Skip ke Level 7 (Autoclave Inspection)** — auto APD + DCS 7 + parameter
- **DEBUG: Auto-Complete Level 7 (semua flag)** — set 5 inspeksi flag

Use `AutoEquipApdLengkap()` + `MulaiLevel()` + `TryOnDCSTombolDitekan()` + `OnVoiceReportAccepted?.Invoke()` + `TeleportPlayerKeSpawnPoint()` patterns when adding new debug skips.

---

## 7. Roadmap Documents (priority reading)

Located in `Assets/Scripts/Roadmap/`:
- **`Olivia_Blueprint_Final.md`** — 14-level master plan
- **`HPAL_DeepResearch.md`** — HPAL chemistry & process
- **`HPAL_Mekanisme_Mesin_DeepResearch.md`** — per-machine mechanism (autoclave, flash vessel, CCD, MHP, neutralization)
- **`OLIVIA_AGENT_SKILL.md`** — original AI agent skill doc (older version)
- **`PROJECT_CONTEXT.md`** — voice keywords, parameter targets per level
- **`GAMEPLAY_Level5_SteamValve.md`**, **`GAMEPLAY_Level6_AcidInjection.md`**, **`GAMEPLAY_Level7_Autoclave.md`** — per-level mechanic specs
- **`AUDIT_BUG_DAN_REFACTOR.md`** — known bugs + refactor priorities

---

## 8. Future Roadmap (Levels 8-14)

### Level 8 — Monitoring DCS (next priority)
- Reactor parameter UI: temperature, pressure, RPM, pH, flow rate readouts
- Player adjusts setpoints via DCS to keep autoclave dalam SOP range
- Trigger: parameter stable for X seconds → `NotifyLevel8MonitoringStable`
- Reuse `Level6AcidInjectionController` panel pattern (URP/Lit material fix + TextMesh sync) for setpoint UI

### Level 9 — Flash Vessel / Letdown
- Open letdown valve manually (handwheel, similar to Level 5)
- Steam vapor particle effect (already exists via `Heat Recovery Steam FX`)
- Visualize 3 flash vessels turun tekanan bertahap
- Voice: "tekanan turun, flash vessel aktif"

### Level 10 — CCD (Counter-Current Decantation)
- Activate CCD separator from DCS
- Watch slurry split: overflow (PLS, kuning-coklat) vs underflow (tailing, abu)
- Add `Flocculant_Dosing_System` mechanic (pump + pH check)
- Voice: "CCD aktif, pemisahan berjalan"

### Level 11 — MHP Presipitasi
- DCS 11 → field MHP plant
- Grab `MgO_Sack` prefab → tuangkan ke `MHP_NeutralizationTank`
- pH naik ke 5.5, color change green
- Grab sample bottle dari `Sample_Port_Handwheel`
- Voice: "MHP presipitasi berhasil"

### Level 12 — Tailing Discharge / Filter Press
- Open `Letdown_Discharge_Valve` ke neutralization tank
- Limestone slurry dosing — pH naik 1→7.5
- Filter press cycle: moisture < 25%
- Cake build-up visualization
- Voice: "tailing netral, filter press OK"

### Level 13 — Dry Stack Tailing (SHOWCASE)
- B3 zone signage + APD pemantauan
- Grab `Limestone_Bag`, scatter di tank → pH naik 7→8.5 (target compliance)
- Filter press final → tailing cake
- Stack cake di `Dry_Stack_Storage_Area`
- Voice: "dry stack aman, pH 8.5"

### Level 14 — K3 Emergency Shutdown
- Alarm gas detector + sirine sound
- White/yellow smoke FX (kebocoran H2SO4 atau steam)
- 45-second countdown timer
- Player MUST: lapor HT "Emergency! Evakuasi!" → tutup isolation valve manual → tekan ESD button merah
- Failure: pipa "explode" tailing fluid spew (penalty cinematic)
- Voice: "emergency, evakuasi, sistem aman"

---

## 9. Skill: Building New VR Mechanics

When asked to add new mechanic, follow this checklist:

### 9.1 Mechanic Design
1. Look up real HPAL mechanism in `HPAL_Mekanisme_Mesin_DeepResearch.md`
2. Identify VR-native angle: spasial reach, dual-tool usage, voice + button, timed inspection, hidden visualization (X-Ray), gestural pour/grab
3. Avoid: pure click-button menus that work better on desktop
4. Bonus: muscle memory transferable to Level 14 emergency

### 9.2 Code Structure
1. Add fields/flags to `GameLevelManager` (private bools + public properties)
2. Add `Notify*` methods + `TryComplete*` aggregator
3. Per-level controller listens to `OnLevelStarted`, runs phase state machine
4. UI: integrate with `PlayerHUD.UpdateOperasionalChecklist` + `UniversalTaskMarker.Resolve*Target`
5. Voice: register keyword in `LevelData` + handle in `HandleVoice*`
6. Add `[ContextMenu]` debug skip

### 9.3 Runtime Object Creation Patterns
- **Mushroom button**: `CreateMushroomButton(name, pos, color, label)` di Level6 — sphere cap + cylinder stem + label panel + XRSimpleInteractable
- **Flat button**: `CreateFlatButton(name, pos, color, label)` — cube + BoxCollider oversized + label TextMesh
- **Panel display**: `CreatePanelDisplay()` — TextMesh visible + hidden TMP_Text proxy + `L6PanelTextSyncer`
- **Pivot group**: `FindNearestX()` → create empty pivot → reparent siblings dengan `worldPositionStays=true`
- **Lamp pair**: `CreateLampPanel(name, pos, ref red, ref green)` — kotak panel + 2 sphere lamp emissive

### 9.4 Material Helpers
- `CreateOpaqueMat(name, color, emission)` — URP/Lit opaque dengan property setup lengkap
- `CreateTransparentMat(name, color, emission)` — URP/Lit transparent + alphablend keyword

### 9.5 Auto-Find Pattern
```csharp
if (_targetField == null)
{
    GameObject go = GameObject.Find("ExactName");
    if (go == null)
    {
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            if (g.name == "ExactName" && g.scene.IsValid()) { go = g; break; }
    }
    if (go != null) _targetField = go.transform;
}
```

### 9.6 Spawn Point Convention
- `SpawnPoint_DCS` — control room
- `SpawnPoint_Lvl{N}` — primary field spawn for level N
- `SpawnPoint_Lvl{N}_Sub` — sub-area within level
- Player rig auto-finds via name `XR Origin (XR Rig)` or `Player` tag

---

## 10. Unity MCP Workflow Tips

When working with Unity MCP server:
1. **Compile check**: `mcp_unityMCP_refresh_unity` then `mcp_unityMCP_read_console` (filter type=error). Always retry if Unity session "not ready".
2. **Runtime inspection**: `mcp_unityMCP_execute_code` with reflection to read private fields. Use `_phase`, `_glm.Level6X`, etc.
3. **Scene modification**: `mcp_unityMCP_manage_gameobject` action=create/modify with explicit position/rotation/scale.
4. **Save scene**: `mcp_unityMCP_manage_scene` action=save AFTER any scene-level change (otherwise lost on play stop).
5. **Screenshot for verification**: `mcp_unityMCP_manage_camera` action=screenshot, view_position + view_target supplied. Player eye height ~9.5, DCS area at z=17-18.

---

## 11. Common XR Coordinate System

- **DCS area**: x=-2.77 to -1.46, y=8.36 (floor) to 9.5 (eye), z=16.28 (spawn) to 18.05 (button)
- **Level 5 PreHeater field**: x ~16-18, y ~2-3, z ~55-66
- **Level 6 Acid Skid**: spawn (-15, 2.5, 42), `Transparent_CalibrationColumn` at (-17.47, 2.76, 42.02)
- **Level 7 Autoclave field**: see `Mesin Utama/Autoclave_Field`
- **Walkie Talkie chest dock**: child of `Socket_WalkieTalkie` on player rig
- **Respirator chest socket**: child of `Socket_Respirator_Baju` (right chest)

---

## 12. Quick Reference Files

When user asks about behavior in level X:
1. Read `Assets/Scripts/Simulation/LevelXController.cs`
2. Read GLM section for level X (search `Level{X}` in GameLevelManager.cs)
3. Read `PlayerHUD.UpdateOperasionalChecklist` for level X case
4. Read `UniversalTaskMarker.ResolveLevel{X}Target` (or `ResolveTarget` switch)
5. Read `GAMEPLAY_Level{X}_*.md` if exists in Roadmap

When user reports bug:
1. Get console errors via `mcp_unityMCP_read_console`
2. Read controller code for the affected level
3. Check `OnLevelStarted` reset logic + phase state machine
4. Test runtime via `mcp_unityMCP_execute_code` reflection on private fields

When user asks for new feature:
1. Check existing patterns (auto-find, runtime creation, voice integration)
2. Add per-level flag in GLM + Notify method
3. Wire HUD checklist + UniversalTaskMarker target
4. Add debug skip method
5. Compile + test play mode

---

**Last updated**: 2026-05-28 (Level 6 acid skid, calibration column animation, DCS panel runtime UI, Level 7 inspection mechanics, Level 5/6/7 debug skip menus added).
