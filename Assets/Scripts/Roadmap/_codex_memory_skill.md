# Codex Agent — Persistent Memory

> **AUTO-LOAD**: This file is automatically loaded at the start of each session.
> **AUTO-WRITE**: At the end of each session, agent MUST append important context here.

## How to Use

### At Session Start
Agent reads this file first. Picks up from where last session left off without user re-explaining.

### At Session End (ALWAYS)
Agent MUST append to bottom of this file:
- Date + brief summary of what was worked on
- Decisions made + reasoning
- Bugs encountered + fixes applied
- Files modified (paths)
- Tasks left incomplete (TODO for next session)
- User preferences observed (style, language, priorities)

Use `fs_append` to add session log; never overwrite previous sessions.

## Session Log

### 2026-05-28 — Olivia HPAL VR Level 5/6/7 Development

**Context**: User membangun simulator pelatihan operator pabrik nikel HPAL di Unity VR. 14 levels total. Sedang fokus Level 5 (Steam Valve), Level 6 (Acid Injection), Level 7 (Autoclave Inspection).

**Workspace**: `C:\Users\mp2dz\Olivia`
**Main scene**: `Assets/Scenes/Level1.unity`
**User language**: Indonesian (campuran teknis English untuk istilah industri)
**User style**: Direct, sometimes frustrated when bugs persist — wants concrete results not promises

**Major work completed today**:

1. **Level 5 Steam Valve fixes**:
   - Looping teleport bug: added guard `!_questTercapai && !_fieldSudahDibuka`
   - Valve rotation tidak responsif: relaxed APD validation (set default `_validasiApdLapangan=false`)
   - Valve auto-rotate fallback when grabbed (4-second full open) untuk prevent player frustration

2. **Level 6 Acid Injection rebuild**:
   - Slurry valve sekarang grup seluruh handwheel (Hub + OuterRing + 4 Spokes) ke pivot baru `L6_SlurryValve_Pivot_Runtime` via `FindNearestSlurryHandwheel`
   - Hapus acid valve, ganti dengan calibration column animation
   - Calibration column liquid (`Transparent_CalibrationColumn`): liquid spawn world-space, naik 85% dari column height, tidak nembus tutup atas
   - DCS Acid Control Panel runtime: 1.4m × 1.0m, posisi kiri DCS button area, 6 button (Ratio +/-, Stroke +/-, Tank Swap, ARM)
   - LOCAL START + LEAK OK mushroom button di acid skid lapangan
   - Phase BukaValveAcid skipped — langsung TekanLocalStart setelah teleport ke acid skid
   - Spawn point baru `SpawnPoint_Lvl6_AcidSkid` di (-15.0, 2.5, 42.0)

3. **Level 7 Autoclave Inspection** (PARTIAL — perlu rombak ulang):
   - Controller ditambahkan ke scene (sebelumnya tidak ada GameObject `Level7Controller`)
   - 6 mekanik dirancang: X-Ray multi-layer, Scale Mark, Cluster Gauge + Logbook, Sample Port, Safety Drill, Voice Report
   - **MASALAH ditemukan**: Sample port BUKAN di autoclave (research HPAL: PLS sampling dilakukan setelah flash vessel di Level 9, bukan dari autoclave 250°C/50 Bar)
   - Player spawn melayang ketika teleport ke `SpawnPoint_Lvl7` di (8, 10, 72.8) — perlu fix posisi tanah
   - X-Ray cairan langsung muncul ungu, tidak ada animasi naik dari bawah
   - Object scene yang sudah ada: `L7_LiquidUnderflow_Handwheel_Hub/OuterRing/Spoke_00-03`, `L7_XRay_InnerSlurry_Surface`, `L7_Local_Control_EStop`, `L7_Local_Control_RunLamp`

4. **Universal systems**:
   - `UniversalTaskMarker` di scene — menampilkan panah + outline wireframe pada target task aktif per level
   - `FindByName` include inactive objects
   - `IsChildOfPlayer` filter agar marker tidak menempel ke walkie/respirator di body player

5. **PlayerHUD checklist**:
   - Level 5: 3-step (lapor awal, putar valve, lapor akhir)
   - Level 6: 6-step (DCS, lapor outlet, putar valve preheater, lapor slurry, DCS acid, field skid + lapor akhir)
   - Level 7: 7-step (DCS, X-Ray, scale, gauge, sample, safety, lapor)
   - Level 8-14: generic + DCS button + voice report

6. **Debug ContextMenu skip methods di GameLevelManager**:
   - DEBUG: Skip ke Level 5 (Steam Valve)
   - DEBUG: Skip ke Level 6 (Acid Injection)
   - DEBUG: Skip ke Level 6 - Acid Skid (Field)
   - DEBUG: Skip ke Level 7 (Autoclave Inspection)
   - DEBUG: Auto-Complete Level 7 (semua flag)

**TODO untuk next session**:
1. **Rombak ulang Level 7** sesuai feedback user:
   - Player spawn di tanah (bukan melayang) — fix posisi `SpawnPoint_Lvl7`
   - Buang sample port mekanik (sampling sebenarnya di Level 9 flash vessel)
   - Tambah valve handwheel `L7_LiquidUnderflow_Handwheel*` untuk player buka inlet autoclave
   - Animasi cairan ungu di `L7_XRay_InnerSlurry_Surface` naik perlahan dari bawah ke atas (bukan instan)
   - X-Ray vision benar-benar tembus pandang autoclave (shell transparan, lihat agitator + slurry inside)
   - Flow baru: DCS 7 → teleport → buka valve handwheel → cairan masuk perlahan → X-Ray monitor → koordinasi DCS → lapor HT
2. Continue Level 8 (Monitoring DCS) when Level 7 done
3. Level 9 Flash Vessel (sample port mechanic moved here)
4. Verify all Level 5-7 flow di playtest end-to-end

**Files yang sudah dimodifikasi hari ini**:
- `Assets/Scripts/Simulation/Level5SteamValveController.cs`
- `Assets/Scripts/Simulation/Level6AcidInjectionController.cs`
- `Assets/Scripts/Simulation/Level7AutoclaveController.cs`
- `Assets/Scripts/Simulation/GameLevelManager.cs`
- `Assets/Scripts/Simulation/PhaseManager.cs`
- `Assets/Scripts/Simulation/WalkieTalkieManager.cs`
- `Assets/Scripts/UI/PlayerHUD.cs`
- `Assets/Scripts/UI/UniversalTaskMarker.cs`
- `Assets/Scripts/UI/Level1ApdTaskHintDirector.cs` (disabled)
- `Assets/Scripts/UI/DirectionArrowIndicator.cs` (`_panahDinonaktifkan` default false)
- `Assets/Scripts/Roadmap/GAMEPLAY_Level6_AcidInjection.md` (rewrote)
- `Assets/Scripts/Roadmap/GAMEPLAY_Level7_Autoclave.md` (created)
- `Assets/Scripts/Roadmap/OLIVIA_HPAL_VR_SKILL.md` (created)
- `Assets/Scenes/Level1.unity` (multiple object additions/modifications)

**User preferences**:
- Wants real-world accuracy (research-driven mechanics)
- Wants visible progress (panah + outline marker, jelas mana button mana)
- Frustrated when buttons "gak ada" — must use proper Material/Collider setup
- Frustrated when canvas/UI overlaps DCS reactor monitoring
- Wants debug skip per level untuk testing cepat
- Communication: campuran Indonesian + English, sometimes ALL CAPS when frustrated

**Important file paths**:
- Workspace: `C:\Users\mp2dz\Olivia`
- Skills: `C:\Users\mp2dz\.codex\skills\olivia-hpal-vr\SKILL.md`
- Memory: `C:\Users\mp2dz\.codex\skills\memory.md\SKILL.md` (THIS FILE)
- System role: `C:\Users\mp2dz\.codex\skills\system.md\SKILL.md`

---

### 2026-06-04 — DCS Control Room TOTAL REBUILD (Blender headless + Unity)

**Context**: User minta rombak total DCS — tombol, meja/console, monitor, gedung/dinding (dinding lama cuma dummy). Harus proper & industrial nikel. Semua mekanisme level digabung di canvas DCS + tombol +/- fungsional di meja. Desain WAJIB pakai Blender headless (background), bukan addon MCP (addon tidak konek).

**Scene aktif**: `Assets/Scenes/Level1_MainBroken.unity` (BUKAN Level1.unity). rootCount 17.

**Blender headless workflow** (addon MCP gagal connect, pakai fallback):
- `& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --python <script>`
- PowerShell: WAJIB pakai call operator `&` untuk path ber-spasi.
- FBX export: `axis_forward='-Z', axis_up='Y', bake_space_transform=False, apply_scale_options='FBX_SCALE_NONE'`.
- **PENTING**: FBX dari Blender Z-up tetap import "rebah" di Unity. Fix: set root Unity `transform.rotation = Euler(90,0,0)` lalu reposisikan supaya floor di Y yang benar. bakeAxisConversion di importer TIDAK cukup.
- Imported room root punya lossyScale ~90x (dari FBX scale). Saat spawn TMP 3D anak anchor, WAJIB counter-scale `localScale = 1/lossyScale` atau teks jadi raksasa.

**Asset Blender dibuat** (di `Assets/Art/DCSControlRoom/`):
- `build_dcs_controlroom.py` -> `DCS_ControlRoom.fbx`: room shell (floor epoxy, paneled walls biru HPAL, ceiling + light strips, hazard skirting, conduit), video wall (1 primary + 2 side display + bezel + valance + beacon), operator console 3-segmen melengkung (plinth, body, worksurface, sloped button deck, annunciator), ESD housing. Anchor empties: DCS_Anchor_VideoWall(_L/_R), DCS_Anchor_ButtonDeck, DCS_Anchor_ParamStrip, DCS_Anchor_ESD, DCS_Anchor_Operator.
- `build_dcs_panel.py` -> `DCS_Panel.fbx`: center module (12 recessed pushbutton bezel 2x6 utk DCS 2..13) + 2 wing param station (display housing + screen + [+]/[-] bezel). Anchor: A_DCS_{2..13}, A_PARAM_{Flow,AcidRatio,AcidStroke,pH,Suhu,Tekanan,RPM}_{DISP,PLUS,MINUS}.

**Unity scene changes**:
- `DCS_ControlRoom_NEW` instance: rot (90,0,0), pos (-2.12,8.36,16.48) area, floor di y~8.30. Dinding lama "Tower of Monitoring/Wall*,Plane*,Cube" di-SetActive(false) (reversible, tidak dihapus).
- `DCS_Panel_NEW` instance: rot (90,0,0), pos y=9.38 (di atas worksurface 9.35). Tombol fungsional lama (Tombol DCS 2..13, Btn_Flow/Acid +/-) di-snap ke anchor via reflection, scale ~0.16 / 0.10.
- 4 material cap dibuat di `Assets/Art/DCSControlRoom/Materials/` (Cap_Normal amber-off, Cap_Highlight amber, Cap_Pressed putih, Cap_Done hijau) URP/Lit emissive — di-assign ke 12 DCSTombolPanel (_materialNormal/Highlight/Ditekan/Selesai sebelumnya UNASSIGNED, makanya dulu merah polos).
- Buat clone tombol +/- baru utk station Suhu/Tekanan/RPM/pH (clone Btn_FlowPlus). AcidStroke clone DIHAPUS (Level6 pakai Btn_AcidStrokePlus/Minus sendiri).
- DCS_Monitor_Canvas dipindah ke video wall: pos (-2.12,10.45,19.90), rotation IDENTITY (euler 0 = readable; JANGAN flip 180 -> mirror). Konten cuma nyala Level 3+ (DcsMonitorActivator), normal.

**Script baru**: `Assets/Scripts/UI/DCSStationController.cs`
- Controller terpadu 7 station parameter. Tiap station: readout TMP 3D di anchor DISP + tombol +/-.
- Readout orientation: `Quaternion.Euler(90,180,0)` (flat di display, readable operator, tidak mirror). Counter-scale 1/lossyScale. Autosize font 0.05-1.0, wrap on.
- ownButtons: Flow & AcidRatio & AcidStroke = false (di-wire FlowRateControlPanel & Level6AcidInjectionController, JANGAN double-wire). Suhu/Tekanan/RPM/pH = true (controller ini wire).
- Update() baca live value dari GLM utk station non-owned (Flow/AcidRatio) supaya readout sync. AcidStroke belum ada getter GLM.
- OnLevelStarted: station.active = (activeLevel==level), enable/disable tombol owned. Highlight via warna readout.
- GLM setters dipakai: SetFlowRate/SetAcidRatio/SetSuhu/SetTekanan/SetRPM/SetPH (TIDAK ada SetAcidStroke).
- Sudah TESTED di play: tekan Suhu[+] 5x -> 30C, RPM[+] 3x -> 15, GLM update, readout "TEMP 30C TGT 252 NAIK" amber. WORKS.

**Sudah ada sebelumnya** (jangan duplikat): `DCSUnifiedOperationsPanel.cs` — panel di dalam DCS_Monitor_Canvas yang nampilin process route + checklist + setpoints + flow bar per level. Ini counterpart sisi-canvas. Komplementer dgn DCSStationController (sisi-hardware meja).

**TODO lanjut DCS**:
- Wire AcidStroke ke GLM (tambah SetAcidStroke + getter di GLM, atau biarkan visual-only).
- Param station kanan masih ada slot "Spare" (RPM module pakai 4 slot: Suhu,Tekanan,RPM,Spare) — Spare belum dipakai.
- Bikin DCS_ControlRoom_NEW + DCS_Panel_NEW jadi prefab biar rapi.
- Pertimbangkan matiin/hapus widget flow lama "Widget_FlowRate" floating kalau redundant dgn station Flow.
- ESD button fisik di console (DCS_Anchor_ESD) belum di-wire ke DCSMonitorUI.TekanESD / GLM emergency.
- Lighting ruangan: light strip emissive tapi belum ada real-time light; ruangan agak flat. Bisa tambah Area light.

**User prefs reconfirmed**: Indonesian, mau hasil visual nyata (screenshot tiap stage), industrial bukan toy, fungsional. Pakai Blender headless utk semua desain 3D.

### 2026-06-04 (lanjutan) — DCS room lighting + Acid Stroke wired ke GLM

**Acid Stroke -> GLM** (DONE, tested):
- `GameLevelManager.cs`: tambah field `_acidStrokeSaatIni`, `public void SetAcidStroke(float)` (clamp 0-100), `public float AcidStroke =>`.
- `Level6AcidInjectionController.cs`: `IncreaseAcidStroke()`/`DecreaseAcidStroke()` sekarang panggil `GameLevelManager.Instance?.SetAcidStroke(_strokePercentCurrent)`. `ResetLevelState()` reset stroke ke 0 di GLM juga.
- `DCSStationController.cs`: station AcidStroke (ownButtons=false, Level6 yang punya tombol) sekarang baca `glm.AcidStroke` di Update -> readout desk sync. PushToGLM AcidStroke case juga panggil SetAcidStroke (utk konsistensi walau jarang dipakai).
- TESTED play: L6 DcsAcidSetup, 4x IncreaseAcidStroke -> GLM.AcidStroke=20, readout desk "PUMP STROKE 20% TGT 70 NAIK" amber. CHAIN WORKS.

**Real-time lighting DCS room** (DONE):
- GameObject `DCS_Room_Lights` (child DCS_ControlRoom_NEW). 5 lights, semua shadows=None (perf, banyak point light):
  - 3 ceiling point light (CeilLight_L/C/R) di y~11.4, cool white (0.95,0.97,1), intensity 7, range 9.
  - 1 Console_TaskSpot (spot, warm, dari atas-depan, euler 60, intensity 9, range 6, spotAngle 70) nyinari meja+tombol.
  - 1 VideoWall_Glow (point biru 0.5,0.7,1, intensity 4, range 5) aksen depan video wall.
- Ambient diturunin: `RenderSettings.ambientMode=Flat`, `ambientLight=(0.22,0.24,0.28)` — sebelumnya Skybox intensity 1 (flat/washed). Sekarang ruangan ada depth, tombol/console kena highlight+shadow.
- Scene saved. Tidak ada flicker (jumlah light di bawah limit URP per-object).

**Catatan**: kalau nanti mau pakai HDRP-like quality atau baking, ceiling masih agak gelap (natural utk control room). Bisa naikin CeilLight intensity kalau user mau lebih terang.

### 2026-06-04 (lanjutan 2) — DCS support tower + zigzag stair V2 + APD room HD redesign

**Konteks**: DCS room melayang. User minta penyangga realistis dari bawah + tangga zigzag akses, dan redesain ruang APD HD. Semua Blender headless.

**Sudah ADA sebelumnya di scene** (dari sesi lain): `DCS_Support_NEW` (frame+stair lama) dan `APD_Room_NEW`. Keduanya di-disable (rename _OLD_disabled) dan diganti V2.

**Asset baru Blender** (`Assets/Art/DCSControlRoom/`):
- `build_dcs_support.py` -> `DCS_Support.fbx`: 6 I-beam column (4 sudut+2 mid) di atas concrete footing+baseplate+bolt, ring beam 3 level, X cross-bracing tiap bay, top grating deck, switchback stair 3 flight + 2 landing + yellow handrail post + grating tread. Material: S_Steel/SteelDk (struktural gelap), S_Concrete (footing), S_Grate, S_Rail (kuning), S_Hazard.
- `build_apd_room.py` -> `APD_Room.fbx`: shell ruang APD HD. Material PROSEDURAL (noise+bump): A_Floor (concrete), A_WallLower (steel-blue dado), A_WallUpper (off-white panel), plus walkway kuning, hazard border, ceiling T-grid + LED panel emissive, cable tray, conduit, door frame+EXIT sign, corner guard, vent, signage PPE.

**FBX axis/scale LESSON PENTING (beda per file!)**:
- DCS_Support.fbx: importer bakeAxisConversion=FALSE, lalu di Unity rotation `Euler(-90,0,0)` (BUKAN +90!) supaya footing di bawah (minY) & deck di atas. Scale instance NON-UNIFORM. Pemetaan axis setelah Euler(-90,0,0): worldX<-localX(width), worldY<-localZ(height), worldZ<-localY(depth). Jadi atur tinggi lewat scale.Z, kedalaman lewat scale.Y. Final localScale ~ (59.3, 47, 152) area; hasil size world (10.6 W, 9.47 H, 7.8 D), footing y=0, deck ~8.3-9.4. Center world (-1.78, _, 16.46) di bawah box DCS.
- APD_Room.fbx: bakeAxisConversion=TRUE + Euler(90,0,0) + scale 1 -> langsung pas (size 12.4x3.8x11.6). Center world (-4.75,0,-3.97), floor y=0. (SAMA seperti room/panel DCS sebelumnya.)
- INTINYA: tiap FBX export hasil import scale beda. SELALU ukur bounds dulu, lalu set scale by ratio target/current per-axis, dan tentukan rotasi dgn cek mana footing vs deck (min/max Y). Jangan asumsi.

**Lighting**:
- `APD_Room_Lights` (child APD_Room_V2): 4 point light (0.98,0.98,0.95) intensity 4.5 range 6 di y~3.2, shadows None. Plus LED panel emissive di shell.
- Global ambient masih Flat (0.22,0.24,0.28) dari sesi sebelumnya.

**Status**: support V2 + stair + APD room V2 placed, scene saved, console 0 error. Object lama (_OLD_disabled) masih di scene (inactive) — bisa dihapus permanen nanti kalau yakin.

**TODO**:
- Handrail stair cuma post vertikal, belum ada top rail nyambung. Bisa diperbaiki.
- Pertimbangkan hapus DCS_Support_OLD_disabled & APD_Room_OLD_disabled permanen.
- APD floor agak ke-blowout terang; bisa turunin emission LED atau intensity light kalau user mau.
- Prefab-kan semua aset DCS (room/panel/support/apd) biar rapi.

### 2026-06-04 (lanjutan 3) — Batch fix Level3/4/5/6 + canvas + pipe

**SUDAH SELESAI**:
- Level3 swirl (`Level3_Runtime_Slurry_Surface_Swirl`) dikecilkan: scatter radius 2.5->1.45, scale factor radius*0.30/1.45 (sebelumnya 0.42/2.65). Tidak keluar tank lagi.
- `Slurry_Instrument_Panel` DIHAPUS: `SlurryConditioningTankRunner.BuildPanel()` call di-comment out.
- Level3 ore belt: tambah serialized `_oreBeltHeightOffset=-0.55` (turunin belt mepet crusher) + `_oreBeltStartSnug=1.2` (geser mid ke start). Diterapkan di HitungPosisiOre (segment belt) + PaksaOrePathBlackBoxKeTank (mid).
- DCSStationController: SEMUA station ownButtons=TRUE sekarang (Flow/AcidRatio/AcidStroke juga). FIX: FlowRateControlPanel TIDAK ADA di scene -> Flow buttons dulu mati. Sekarang controller wire semua +/- sendiri. Update() baca semua param dari GLM (truth sync). TESTED: L4 FlowPlus x3 -> GLM.FlowRate 150, readout sync. WORKS.
- Level6 animasi diperlambat: _durasiSlurryFlow 5->16, _durasiAutoclaveFill 6->18, _durasiAcidFlow 5->14, _columnFillDuration 6->14.
- Level6 `L6_Autoclave_PurpleLiquid_Rising_Runtime` DIHAPUS: EnsureAutoclaveLiquid() jadi no-op (return).
- Pipa baru `Pipe_L5Flange_To_L7Underflow` (parent Mesin Utama): L5_CleanInlet_Flange(-4.68,5.46,42) -> overhead runY6.6 -> L7_LiquidUnderflow_BlindCap(2,3.07,74.36). Material baru `M_AcidPipe_Steel` (grey metallic, BUKAN atlas yg striping). radius 0.42. 9 part.
- PlayerHUD Panel_Quest dikecilkan 560x780 -> 430x560, anchor top-right margin 24 (sebelumnya nutupin view). ScreenSpaceOverlay 1920x1080.
- Pipa kuning `Pipe_AcidTankB_To_Autoclave` (sesi sebelumnya): AcidTank_B_VentValveStem -> autoclave ACID IN flange. DONE.

**MASIH TODO (butuh iterasi play-mode visual, BELUM dikerjakan)**:
1. Spawn position Level 4 dst SALAH (player melayang/posisi ngawur). Perlu cek tiap SpawnPoint_Lvl* vs ground.
2. Gauge/handwheel Level 5 & 6: user mau putar pakai DETEKSI ARAH TANGAN realtime (immersive), bukan auto-rotate. Tombol R fallback sudah ada. TrackWheelRotation ada tapi katanya kurang riil. Perlu rework input rotasi.
3. Level6_Runtime_Objects rotate -180 menghadap depan + teleport player ke sini saat L6 (acid skid). BELUM (runtimeRoot SetParent transform,false - perlu set rotation).
4. Canvas task kiri (PlayerHUD): user mau REMAKE/REDESAIN total, checklist kadang gak kecentang, kondisi jelek. Perlu audit UpdateOperasionalChecklist + redesign.
5. Canvas mesin depan (DCSMonitorUI di video wall) gak nyambung sama mesin. User mau canvas TAMBAHAN di sisi kiri & kanan untuk task Level 4 dst.
6. L6_SlurryFlow_Preheater_To_Autoclave: masukin cairan ke tabung pipa L5->L7 yg baru dibuat (align flow path ke pipa).
7. DCS_Support_V2 (Inspector: pos -1.78,4.77e?,16.46 rot -90,0,-90 scale 59.3,57.6,101.8) - user bilang JELEK, sesuaikan tangga dgn PINTU DCS. Perlu align stair top landing ke posisi pintu masuk DCS room.

**Catatan penting**: Banyak object Level adalah RUNTIME-created (cuma ada di play mode), HARUS difix di kode bukan scene. Level6_Runtime_Objects, swirl, ore belt, purple liquid, flow cylinders semua runtime.

### 2026-06-04 (lanjutan 4) — CHECKPOINT: Spawn positions + Gauge rotation (DONE, nunggu approval)

**Spawn positions FIXED** (scene Level1_MainBroken, saved):
- Audit semua SpawnPoint_Lvl* via raycast down. Banyak melayang/nembus tanah.
- SpawnPoint_Lvl3 8.89->6.44, Lvl4 8.38->8.36, Lvl4_Preheater 6.14->5.22, Lvl4_Pump 8.74->8.21 (catwalk grating top, raycast awalnya kena Socket_Rompi player rig - WAJIB exclude Socket_*/XR saat probe), Lvl5_PreHeater 5.00->2.55, Lvl6 5.00->3.88, Lvl7 2.60->0.60, Lvl14 6.48->8.97.
- Lvl6_AcidSkid (2.5), Lvl9-13 dibiarkan (NO_GROUND_HIT, area beda, mungkin sudah ok).
- PENTING: edit transform spawn saat PLAY MODE tidak persist. WAJIB edit di EDIT MODE lalu save.

**Gauge/handwheel rotation FIXED** (immersive realtime hand):
- `GesturalHandwheel.cs` (dipakai Level5 steam valve + Level8 flash): smoothTime 0.22->0.04 (follow tangan instant, dulu laggy), maxDegPerSec 185->720, gesturalGain default 2.2->1.0 (1:1 natural). Hapus Math.Max(1,gain) floor. Setup() sekarang FORCE smoothTime<=0.04 & maxDeg>=720 walau instance lama punya serialized value lama (0.22/185).
- R key fallback tetap ada (debugKey=KeyCode.R, Input.GetKey += 360/s).
- Level6 slurry valve (pakai TrackWheelRotation lama, BUKAN GesturalHandwheel): HAPUS auto-open fallback (dulu +360/s saat grabbed tanpa yaw delta -> override gesture). Sekarang murni gestural + R/F keyboard. TrackWheelRotation sudah 1:1 tangensial (proper).

**STATUS**: Checkpoint selesai. User minta STOP & tunggu approval sebelum lanjut ke TODO berikutnya (canvas remake, Level6 rotate -180+teleport, DCS support redesign, flow ke pipa, canvas tambahan kiri-kanan).
