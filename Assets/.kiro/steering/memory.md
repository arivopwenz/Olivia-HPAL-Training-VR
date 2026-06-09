---
inclusion: always
---

# Olivia HPAL VR — Persistent Memory

> **AUTO-LOAD**: Loaded at the start of each session (Kiro steering).
> **AUTO-WRITE**: At session end, APPEND a new session log (never overwrite previous).

## Project Snapshot
- **Workspace**: `C:\Users\mp2dz\Olivia` (Unity project root is one level above `Assets`).
- **Main scene**: `Assets/Scenes/Level1.unity` (semua level hidup berdampingan di 1 scene).
- **Engine**: Unity 6 + URP + XR Interaction Toolkit 3.4.x. Target: VR (XR Origin) + desktop simulator.
- **Git**: branch `main`, remote `https://github.com/arivopwenz/Olivia-HPAL-Training-VR.git`. Commit pakai bahasa Indonesia.
- **Blender**: `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe` (jalan headless `--background --python`). Punya BlenderMCP addon (kadang tidak konek ke MCP; headless selalu bisa).
- **User language**: Indonesian + istilah teknis English. Style: direct, sering ALL CAPS saat semangat/frustrasi, mau hasil konkret + visual, sering bilang "lnjutkn"/"GAS".

## CRITICAL — Penomoran Level (membingungkan, hafalkan!)
Display number ≠ enum `GameLevelManager.GameLevel`. Level 9 lama (FlashVessel) sudah di-MERGE ke Level 8.
| Display | Enum | Controller | Isi |
|---|---|---|---|
| Level 8 | `Level8_Monitoring` (8) | Level8FlashTrainController | Flash Vessel & Letdown 3-stage |
| (skip) | `Level9_FlashVessel` (9) | Level9FlashVesselController | DIPENSIUNKAN — auto lompat ke Level10_CCD |
| **Level 9** | `Level10_CCD` (10) | **Level10CCDController** | **CCD (pemisahan padat-cair) + PLS sampling + Lab QC** |
| **Level 10** | `Level11_MHP` (11) | **Level11MHPController** | **Neutralization/Pemurnian + MHP precipitation** |
| Level 11 | `Level12_TailingDischarge` (12) | Level12TailingFilterController | Tailing neutralization + Filter Press |
| Level 12 | `Level13_TailingWaste` (13) | Level13DryStackController | Dry Stack Tailing |
| Level 13 | `Level14_Emergency` (14) | — | Darurat K3 / ESD |

DCS button mapping: `Level10_CCD` pakai tombol DCS **9**, `Level11_MHP` pakai tombol DCS **10**.

## HPAL Process Flow (research-verified)
Ore→Slurry→Pump→PreHeat→Acid→Autoclave→Flash Letdown→**CCD**→(2 aliran):
1. **CCD OVERFLOW (PLS, cairan jernih kaya Ni/Co)** → **Pemurnian/MHP (Level 10)**: pre-neutralization (limestone CaCO₃) → Fe/Al/Cr removal (slaked lime Ca(OH)₂) → MHP precipitation (MgO, pH ~7-8 → endapan Ni/Co hijau-kebiruan = bahan baku baterai EV).
2. **CCD UNDERFLOW (padatan/lumpur = tailing)** → **Tailing Filter Press**.
Sumber: Nickel Institute, Taganito/Coral Bay HPAL case studies, paper MHP (MgO precipitation).

## Plant Layout (world coords, Level1.unity, parent "Mesin Utama")
- CCD_Field: center (1, 4, 62), tanks z≈108-120, 3 thickener. Rake roots: `Rake_Arm_Root(.001/.002)`.
  - CCD overflow header ≈ (19, 6.7, 108). CCD underflow pump station ≈ (-15, 1, 122).
- Level11_MHP_Field: center (41,6,61), tanks x≈67-80 z≈106. Inlet `Neutralization_Inlet_Flange` (67.5,1.8,106.7). Model `Level11_PurificationMHP_BlenderRig`.
- Tailing Filter Press: `Final_FilterPress_Unit` di `Level13_DryStack_BlenderRig` ≈ (22, 2.4, 147) + clarifier + cake conveyor.
- SpawnPoint_DCS (-2.1, 8.4, 16.3). SpawnPoint_Lvl10/11 dst per level.

## Reusable Patterns (WAJIB diingat)
- **VR button HARUS XRSimpleInteractable + BoxCollider** (UnityEngine.UI.Button + GraphicRaycaster saja TIDAK bisa diklik ray XR di world-space canvas). Tambah keyboard fallback (Enter/L).
- **Serialized scene values override code defaults** — kalau ubah default field, cek & set juga nilai serialized di komponen scene (mis. Level8 `_handwheelFullOpenDegrees` ternyata 1800 di scene).
- **Stale references**: banyak controller punya field serialized NULL/nyasar ke model lama. Re-resolve di OnLevelStarted via AutoFindReferences. Cek dengan reflection + SerializedObject.
- **Rake/disc dari FBX** sering ter-orientasi salah (authored di local XY plane). Untuk thickener: set root euler (270,0,0) supaya disc horizontal, lalu spin pakai `RotateAround(tankAxis, Vector3.up)`.
- **Baked text mirror di FBX** (UV/scale negatif) tidak bisa diperbaiki via transform flip aman. Solusi: sembunyikan renderer baked + overlay TextMesh readable yang billboard ke player (lihat `FixBakedLabels` di Level10CCDController).
- **execute_code snippet TIDAK bisa lihat tipe project** (mis. ProcessPipeFlowAnimator) kecuali via reflection `AppDomain...GetType`. Tapi tipe baru hanya muncul setelah compile selesai (GetClass() != null).
- **Blender→Unity coords**: U2B(ux,uy,uz)=(ux,uz,uy). Dengan FBX preset axis_up=Y, axis_forward=-Z, bake_space_transform=True, hasil import di Unity sering ter-mirror X&Z → koreksi cepat: rotasi instance 180° di Y.
- Screenshot putih dari `manage_camera` tanpa view_position = quirk capture game-view; pakai view_position/view_target untuk verifikasi visual yang andal.

## Session Log

### 2026-05-28 — Level 5/6/7 (ringkas)
Level 5 steam valve fixes (teleport loop guard, relaxed APD, auto-rotate fallback). Level 6 acid injection rebuild (slurry valve group pivot, calibration column, DCS acid panel runtime, LOCAL START/LEAK OK). Level 7 autoclave partial (X-Ray, scale, gauge) — sample port dipindah ke flash vessel. UniversalTaskMarker + PlayerHUD checklist per level. Debug skip ContextMenu per level di GameLevelManager.

### 2026-05-31 — Level 8 gauge + Level 9 (CCD) full rework + Level 10 pipes
**Level 8 (Flash Train) — gauge/handwheel dipermudah + teleport:**
- `_gesturalGain` (field ada tapi TIDAK pernah dipakai di math) → diwire ke UpdateHandwheel + GetGesturalDelta. Default 3.2→5.
- `_handwheelFullOpenDegrees` default 540→300, TAPI scene punya nilai serialized 1800 → di-set ulang ke 300 di scene + gain 5.
- `MakeStandSpot` autoclave valve: stand 2.6m→4.2m, height offset 1.4→1.6 (wheel tak nutupi layar).

**Level 9 = CCD (`Level10CCDController`) — bug besar diberesin + immersive:**
Akar masalah: controller ditulis utk model CCD lama, scene pakai `CCDIndustrialUVRedesign` baru → refs stale.
- Stop aktifkan stub bar lama (`Feed_Inlet_FromFlash_Liquid`, `Overflow_ToPurification_Liquid`) = batang melayang aneh.
- 3 rake roots NULL + 2 ter-orientasi vertikal, 1 melayang → reorient (270,0,0) horizontal, submerged y≈3, center tank axis; spin via RotateAround world-Y. Di-bake ke scene.
- Animasi separation: PLS surface keruh(coklat)→jernih(teal) via MaterialPropertyBlock, feedwell core menyusut, underflow pool naik. Drive motor/agitator/pompa berputar.
- Separation FX `CCD_Separation_FX` material NULL (magenta) → EnsureFxMaterial + 3 overflow trickle FX per launder.
- Sample station di-upgrade industrial (cabinet, hazard band, spout, valve, bottle, label billboard).
- Teleport observasi: `ResolveFieldStandSpot` hitung dari bounds train (standBack proporsional tinggi tank).
- **Mirrored baked text** (WASH WATER/legend/CCD labels) → `FixBakedLabels`: sembunyikan renderer baked + overlay TextMesh readable + `BillboardOverlayLabels`.
- **Lab QC bug FIXED**: tombol ACCEPT canvas world-space TIDAK bisa diklik VR → `AddButton` sekarang attach XRSimpleInteractable + BoxCollider (AttachXrButtonNextFrame) + fallback keyboard Enter (`_pendingAcceptAction`). Canvas hasil diperbesar (scale 0.85), eye-level, 2.2m depan, title bar.
- QC full-loop di-test bersih: DCS9→separation→3 sample(proximity)→submit→analisa→ACCEPT→PLS lulus→voice report "ccd aktif pls lulus qc"→complete. No console errors.

**Level 10 pipes (Blender headless):**
- Script `Assets/_build_ccd_pipes.py` → export `Assets/Art/CCDProcessPipesBlender/CCD_Process_Pipes.fbx`.
- 2 pipe run: `PLS_Overflow_Pipe` (CCD overflow 19,6.7,108 → MHP inlet 67.5,2.2,107, elevated rack + flanges + supports) & `Underflow_Slurry_Pipe` (CCD pump -15,1.4,122 → filter press 21,2.6,146.5). Tiap run punya inner `*_Flow` tube material distinct (PLS hijau / slurry coklat).
- Import + instance `CCD_Process_Pipes` di scene, rotasi 180° Y untuk koreksi mirror X/Z → landing tepat di endpoint. Diparent ke "Mesin Utama".
- Materials import OK (UV_PipeSteel_Grey, UV_PLS_FlowGreen, UV_Underflow_SlurryBrown).
- Buat `Assets/Scripts/Simulation/ProcessPipeFlowAnimator.cs` (emisi gelombang menjalar; SetFlowing toggle; sembunyi saat off). **BELUM ke-attach** — pas mau attach, Unity refresh timeout & tipe belum ter-compile (GetClass NULL). 

**TODO next session (Level 10):**
1. Pastikan `ProcessPipeFlowAnimator` ter-compile (refresh/compile Unity), attach ke `PLS_Flow` & `Underflow_Flow`, default SetFlowing(false).
2. Wire Level10CCDController: saat CCD separation selesai → nyalakan flow PLS pipe (ke MHP) + underflow pipe (ke filter press) sebagai visual aliran padatan/cairan. (User: CCD nyambung ke pemurnian + padatan ke filter press, "cuma pipa & animasi padatannya ngalir ke 2 tempat itu".)
3. Rework `Level11MHPController` jadi 3-stage realistis (limestone→lime→MgO), pH + warna larutan berubah, sampling MHP, fix refs stale + baked text mirror.
4. JANGAN sambungkan Level 10/11 ke Darurat (user eksplisit).
5. Cleanup temp files: `Assets/_build_ccd_pipes.py`, `Assets/_find_blender.ps1`, root `_patch1.py` (untracked).
6. Save scene + commit Indonesia + push origin main.

**Files modified 2026-05-31:**
- `Assets/Scripts/Simulation/Level8FlashTrainController.cs`
- `Assets/Scripts/Simulation/Level10CCDController.cs` (banyak: rake, separation, FX, sample, FixBakedLabels, Lab QC button)
- `Assets/Scripts/Simulation/ProcessPipeFlowAnimator.cs` (BARU)
- `Assets/_build_ccd_pipes.py` (BARU, temp), `Assets/_find_blender.ps1` (temp)
- `Assets/Art/CCDProcessPipesBlender/CCD_Process_Pipes.fbx` (BARU, dari Blender)
- `Assets/Scenes/Level1.unity` (rake reorient, pipe instance, Level8 gauge values, sample/labels runtime)

---

### 2026-05-31 (Part 28) — Sambung CCD (Lv9) → MHP (Lv10) + Tailing Filter Press (pipa profesional + flow)

**Konteks**: User minta sambungkan Level 9 (CCD) ke Level 10 (MHP) dan ke level terakhir (tailing), kerjakan Level 10, research realistis industri nikel, pipa via Blender headless. Pertanyaan user: CCD memisahkan cairan-padatan → perlu pipa ke Filter Press? **JAWAB: YA.** CCD = Counter-Current Decantation, 2 keluaran:
1. **OVERFLOW** (PLS cairan jernih kaya Ni/Co) → **Pemurnian/MHP (Level 10)**.
2. **UNDERFLOW** (padatan/lumpur tailing) → **Tailing Filter Press** (level terakhir).

**Temuan awal**: ProcessPipeFlowAnimator.cs sudah compile. Level10CCDController SUDAH punya StartProcessPipeFlows/StopProcessPipeFlows/AnimateProcessPipeFlow (cari root `CCD_Process_Pipes` child `PLS_Flow`/`Underflow_Flow`). Instance `CCD_Process_Pipes` lama ADA di scene (rot Y=180, parent Mesin Utama) TAPI PLS pipe berakhir SALAH di (67.5,1.78,106.7) — MHP inlet sudah pindah ke (73.42,8.21,111.88) sejak rebuild MHP v2 (Part 14). Underflow pipe lama sudah OK.

**Koordinat aktual (verified)**: CCD overflow head `OverflowPLS_ToPurification_Head` (21.6,6.97,107.2); CCD underflow pump station (-16.8,1.1,122.15); MHP inlet `Neutralization_Inlet_Flange` (73.42,8.21,111.88); `Final_FilterPress_Unit` boundsCtr (22.5,2.4,147.3).

**Rebuild pipa (Blender headless)**: `Assets/Art/CCDProcessPipesBlender/build_ccd_process_pipes_v2.py` → `CCD_Process_Pipes_v2.fbx`. High-fidelity (bevel valve, shade_smooth). 2 run: PLS_Overflow_Pipe (CCD→MHP inlet, elevated rack y≈7→8.2, + letdown valve handwheel oranye) & Underflow_Slurry_Pipe (CCD pump→filter press, + knife valve). Flow tube inner di-JOIN jadi 1 mesh `PLS_Flow` (hijau emissive) & `Underflow_Flow` (coklat emissive) supaya controller resolve.
- **Konvensi koordinat TERBUKTI**: author Unity-coords `u2b(ux,uy,uz)=(ux,uz,uy)`, export normal (apply_unit_scale, -Z fwd, Y up, NO bake_space_transform), instance Unity rot **Y=180** → landing TEPAT di koordinat Unity. Test instance konfirmasi: PLS flange end @ (73.42,8.21,111.88) PAS MHP inlet, start @ (21.6,6.95,107.2) PAS CCD head, underflow end @ (21.5,2.6,146) PAS filter press.

**Unity**: DestroyImmediate CCD_Process_Pipes lama → InstantiatePrefab v2, name `CCD_Process_Pipes`, parent Mesin Utama, pos 0, rot Y=180, scale 1. Reflection: Level10CCDController._plsFlowPipe=PLS_Flow, _underflowFlowPipe=Underflow_Flow OK; Start enable=True / Stop enable=False (toggle benar). 

**Edit code**: Level10CCDController.cs baris 125 — tambah `StopProcessPipeFlows();` di OnLevelStarted (setelah AutoFindReferences) → flow tube TERSEMBUNYI sampai pemisahan CCD selesai, lalu StartProcessPipeFlows() nyala saat separation complete (sudah ada di RunCCDSequence). 0 compile error.

**Verifikasi transisi (statik, GLM)**: kontigu — Level10_CCD (DCS 9, voice "ccd aktif pls lulus qc") → Level11_MHP (DCS 10, voice "mhp terbentuk", targetPH 5.5) → Level12_TailingDischarge (DCS 11, "limbah dialirkan"). Transisi `(int)level+1` + guard Level9_FlashVessel auto-redirect ke Level10_CCD. JANGAN sambung Lv10/11 ke Darurat (tetap dipatuhi).

**Level 10 (MHP) controller** (Level11MHPController.cs) — sudah fungsional & research-aligned: DCS 10 → fade teleport field → neutralization pH 1.2→5.5 (limestone/lime, 8s) → MHP precipitation (MgO, kualitas 0→92%, 9s) → MHP_Sample_Flow+Product → NotifyLevel11MHPComplete → lapor HT "mhp terbentuk". Semua child di Level11_MHP_Field ADA (Feed_From_CCD_Liquid, Reagent_Liquid_Line, Neutralization_To_Polishing_Liquid, Polishing_To_MHP_Liquid, MHP_Sample_Flow/Product, FX, 6 Agitator_Root). TIDAK di-rewrite (sudah benar).

**Files modified**:
- `Assets/Art/CCDProcessPipesBlender/build_ccd_process_pipes_v2.py` (BARU)
- `Assets/Art/CCDProcessPipesBlender/CCD_Process_Pipes_v2.fbx` (BARU)
- `Assets/Scripts/Simulation/Level10CCDController.cs` (StopProcessPipeFlows di OnLevelStarted)
- `Assets/Scenes/Level1.unity` (replace instance CCD_Process_Pipes v2)

**TODO next**: Play-test mode end-to-end Lv9→Lv10 (transisi + flow nyala saat CCD selesai) belum dijalankan di play mode (verifikasi sejauh ini reflection/statik + landing presisi). Cleanup: screenshot test di Assets/Screenshots (harmless). Old FBX `CCD_Process_Pipes.fbx` & `CCD_ConnectionPipes.fbx` masih ada (tidak dipakai, bisa dihapus nanti). Commit Indonesia + push manual saat user minta.


### 2026-05-31 (Part 29) — Level 10 (MHP) jadi GAMEPLAY INTERAKTIF + INFORMATIF (HPAL)

**User**: "buat sangat interaktif dan berfungsi, sangat informatif industrial sesuai HPAL nikel, AKU INGIN JUARA". Sebelumnya Level 10 (enum Level11_MHP) pasif (tekan DCS -> nonton animasi -> lapor).

**Research (web + scene)**: Pemurnian HPAL 3 tahap: (1) PRA-NETRALISASI limestone CaCO3 pH~1.5->3.5 buang Fe3+/Al3+ (endapan coklat + gypsum); (2) POLISHING kapur Ca(OH)2 pH->5.0 buang Al/Cr/sisa Fe; (3) PRESIPITASI MHP MgO pH->7.0-7.5 @50-60C -> Ni(OH)2+Co(OH)2 (hijau-kebiruan) = MHP, bahan baku baterai EV. Recovery Ni ~82-95%, Co ~92%, window pH 6.0-8.4 (Springer 2025, ResearchGate Moa Bay, Nickel Institute). Produk MHP ~40% Ni, ~3-4% Co, moisture ~48%.

**Mesin Level11_MHP_Field (inspeksi)**: 3 dosing skid REAL (Neutralization_Reagent_Dosing_Skid / Lime_Dosing_Skid / MGO_Dosing_Skid, masing2 DosingPump+ScrewFeeder+FeederMotor+Silo), 3 tank (pH_Gauge+pH_Probe+ReagentLance+StatusStrip), agitator (Agitator_Root + AGI_* shaft), liquid flow (Feed_From_CCD_Liquid, Reagent_Liquid_Line, Neutralization_To_Polishing_Liquid, Polishing_To_MHP_Liquid), sampling (MHP_Sample_Flow/Product/Cup + 4 product bag). SpawnPoint_Lvl11 (74.11,0,93.51).

**REWRITE Level11MHPController.cs (interaktif, 550 baris, validate 0 error)**:
- DCS10 -> fade teleport field -> BuildOperatorStation (tombol DOSING cube XRSimpleInteractable+BoxCollider + info panel 3D billboard TextMesh) di depan spawn.
- 3 tahap dosing GATED: tekan tombol (XR ray/poke) ATAU keyboard SPACE/1 -> DoseRoutine 5s: pH naik bertahap live (SetPH ke GLM + panel), liquid di-Tint warna (MaterialPropertyBlock: coklat->teal->hijau), skid dosing terkait beranimasi (ResolveSkidMotors rotate), FX emission naik, stage 3 MHP quality 0->92%.
- Info panel LIVE tiap tahap: reagen+formula, reaksi kimia, fungsi (impurity dibuang), target pH, pH sekarang, MHP%.
- Setelah 3 tahap -> proximity sampling (jalan ke MHP_Sample_Cup radius 3.2m) -> MHP_Sample_Product hijau.
- Lab QC pop-up 3D (quad + TextMesh assay: pH7.5, Ni41%, Co3.6%, recovery Ni94/Co92, Fe/Al/Cr<0.1%, Mn ditekan, moisture48%, VERDICT DALAM SOP) + tombol ACCEPT (XR + Enter).
- ACCEPT -> NotifyLevel11MHPComplete -> lapor HT 'MHP terbentuk' -> transisi Level 11.
- Public props utk HUD: LevelActive/Stage1Done/Stage2Done/Stage3Done/SampleTaken/LabAccepted/PHCurrent/MHPQualityCurrent.
- Helper self-contained: MakeText (TextMesh + builtin font LegacyRuntime.ttf + font.material), OpaqueMat (URP/Lit), AttachXrButton (BoxCollider + XRSimpleInteractable + colliders.Add + select/hover listener), GenNoise/GenChime audio, BillboardTo. Keyboard fallback: SPACE/1 dose, jalan = sample, L submit lab, Enter accept, T lapor (WT).
- File ditulis via `write` tool (overwrite konten, .meta/GUID TETAP -> komponen di scene GO 'Level11_MHP_Field' tidak putus).

**PlayerHUD.cs**: checklist Level 10 (baris 879-882) diganti per-langkah baca FindFirstObjectByType<Level11MHPController> (Stage1/2/3Done, SampleTaken, LabAccepted) + voice keyword diselaraskan 'MHP terbentuk' (sebelumnya teks 'MHP presipitasi berhasil' beda dari keyword GLM).

**MASALAH BELUM SELESAI**: Unity editor compile pipeline HANG — EditorApplication.isCompiling=True menetap >10 menit (execute_code Roslyn snippet tetap jalan, 0 error CS di console & validate_script). Diperparah aku memicu refresh_unity scope=all force (recompile SELURUH proyek 14-level = berat) berulang. Assembly-CSharp belum reload -> Stage1Done dll belum muncul -> BELUM bisa play-test runtime. RequestScriptCompilation() sudah dipanggil, belum kelar. Kemungkinan editor butuh fokus / ada dialog modal / tinggal tunggu lama.

**TODO next**: tunggu compile selesai (jangan picu scope=all force lagi) -> verifikasi reflection (props muncul) -> play-test/AutoFindReferences -> screenshot operator station + info panel + lab -> (scene tak perlu save, elemen interaktif dibangun runtime; controller sudah di scene). Commit Indonesia + push manual saat user minta.

**Files modified**: Assets/Scripts/Simulation/Level11MHPController.cs (rewrite), Assets/Scripts/UI/PlayerHUD.cs (checklist Level 10 per-langkah).


### 2026-05-31 (Part 30) — Level 11 (Tailing & Filter Press) jadi GAMEPLAY INTERAKTIF + INFORMATIF

**User**: "langsung buatkan level berikutnya HARUS BENER DETAIL YA RESEARCH DULU". Level berikutnya = Level 11 display = enum Level12_TailingDischarge (DCS 11, voice 'limbah dialirkan').

**Research (web)**: Manajemen limbah HPAL: tailing (underflow CCD = leach residue asam + gypsum) -> NETRALISASI limestone/kapur (CaCO3/Ca(OH)2) pH ~2.3->8 (buang asam sisa + endapkan logam berat, baku mutu 6-9) -> FILTER PRESS plate&frame dewatering moisture cake 60%-> <25% (stackable) -> filtrat jernih ke WWTP, cake ke DRY STACK (stabil anti-jebol; Indonesia Halmahera dry stacking). Sumber: Nickel Institute, ResearchGate dry stack nickel residue, McLanahan/Metchem filter press, UWA dewatering.

**Mesin (inspeksi)**: controller LAMA cari `Level12_TailingFilter_Field` yg TIDAK ADA -> refs NULL + spawn SALAH (SpawnPoint_Lvl12 @ (27.8,2.35,25.8) z=25 jauh dari mesin). Mesin ASLI = `Level13_DryStack_BlenderRig` (pos 23.18,0.13,146.12) berisi: Final_Neutralization_Tank @ (37.29,3.98,146) [Polishing_Agitator_Root, Limestone_Pour_Stream, Neutralized_Surface, GNT_LimeHopper, Limestone_Bag], Final_FilterPress_Unit @ (22.5,2.18,146) [PressPlate_00..17, Filtrate_Channel, GFP_FiltrateToWWTP], Cake_Transfer_Conveyor @ (14.58) [Cake_Block_00..05, Conveyor_Roller_00..09, Cake_On_Conveyor], pH_Monitor_Panel @ (37.29,2.18,152) [pH_Monitor_Needle, pH_Status_Green/Red, GPH_Dial], Environmental_Beacon_Green/Red @ (5.86,~3,151), Polished_Tailing_Flow @ (32.67,3.21,146). DryStack jauh di z~250 (GDS_*).

**REWRITE Level12TailingFilterController.cs (interaktif, 487 baris, validate 0 error, 3 warning benign)**:
- DCS11 -> fade teleport field COMPUTED (28,1.5,140) hadap +z (BUKAN SpawnPoint_Lvl12 yg salah) -> BuildOperatorStation (1 tombol aksi cube XRSimpleInteractable+BoxCollider + info panel 3D billboard) di (28,2.85,142.6).
- 2 tahap GATED via tombol (XR ATAU keyboard SPACE/1):
  * TAHAP 1 NETRALISASI: NeutralizeRoutine 6s -> pH 2.3->8.0 live (SetPH + jarum pH_Monitor_Needle rotate -80..+80 deg + pH_Status_Green/beacon hijau ON), Limestone_Pour_Stream aktif, Neutralized_Surface tint coklat->abu, Polished_Tailing_Flow aktif.
  * TAHAP 2 FILTER PRESS: FilterPressRoutine 8s -> moisture cake 60%->22%, Filtrate_Channel aktif (ke WWTP), Cake_Block_00..05 muncul progresif + tint gelap (kering), Conveyor_Roller spin.
- Info panel LIVE: reagen+formula+reaksi, fungsi, target pH/moisture, pH & moisture sekarang.
- Lalu proximity inspeksi cake (jalan ke Cake_On_Conveyor radius 3.5m) -> Compliance QC pop-up 3D (pH 8.2 baku mutu 6-9, moisture 22% <25% dry-stack OK, filtrat jernih TSS rendah ke WWTP, logam berat < baku mutu, VERDICT AMAN LINGKUNGAN) + ACCEPT (XR+Enter) -> NotifyLevel12TailingFilterComplete -> lapor HT 'limbah dialirkan'.
- Public props: LevelActive/NeutralizeDone/FilterPressDone/Inspected/ComplianceAccepted/PHCurrent/CakeMoisture. Helper sama persis pola MHP (MakeText builtin font, OpaqueMat URP/Lit, AttachXrButton, GenNoise/GenChime, BillboardTo, TeleportTo(pos,fwd)).
- File via `write` tool overwrite (GUID/.meta tetap, komponen scene tak putus).

**PlayerHUD.cs**: checklist Level 11 (baris 897-900) diganti per-langkah baca FindFirstObjectByType<Level12TailingFilterController> (NeutralizeDone/FilterPressDone/Inspected/ComplianceAccepted) + voice keyword diperbaiki ke 'limbah dialirkan' (sebelumnya teks 'tailing netral, filter press OK' beda dari keyword GLM).

**MASALAH BESAR BELUM SELESAI**: Unity editor compile pipeline MASIH HANG dari Part 29 (~1 jam, EditorApplication.isCompiling=True menetap; sudah coba UnlockReloadAssemblies x3 + RequestScriptCompilation + AssetDatabase.Refresh -> tak menolong; execute_code Roslyn tetap jalan; 0 error CS). Assembly-CSharp BELUM reload -> Level11MHPController(Stage1Done) & Level12TailingFilterController(NeutralizeDone) BELUM muncul -> BELUM bisa verifikasi/play-test KEDUA level (MHP Part 29 + Tailing Part 30). Diperparah aku memicu refresh scope=all force berulang di proyek 14-level. KEMUNGKINAN BESAR perlu USER: fokus window Unity / RESTART editor Unity (kode aman di disk, GUID utuh).

**TODO next (begitu Unity pulih)**: refresh -> verifikasi props muncul (Stage1Done, NeutralizeDone) -> AutoFindReferences kedua controller resolve mesin -> play-test alur MHP (dosing x3 -> sampling -> lab -> lapor) & Tailing (netralisasi -> filter press -> inspeksi -> compliance -> lapor) -> screenshot -> (scene tak perlu save, elemen runtime; controller sudah di scene). JANGAN picu scope=all force lagi. Commit Indonesia + push manual saat user minta.

**Files modified**: Assets/Scripts/Simulation/Level12TailingFilterController.cs (rewrite interaktif), Assets/Scripts/UI/PlayerHUD.cs (checklist Level 11 per-langkah).


### 2026-05-31 (Part 31) — VERIFIKASI SUKSES: Unity pulih + play-test Level 10 (MHP) & Level 11 (Tailing)

**Unity editor PULIH** (user kemungkinan fokus/restart): isCompiling=False, kedua controller baru ter-load (MHP.Stage1Done & Tailing.NeutralizeDone resolve).

**Temuan**: Level12TailingFilterController BELUM ter-attach ke GameObject manapun (instances=0) -> level tailing tak akan jalan. FIX: manage_components add Level12TailingFilterController ke `Level13_DryStack_Field` (instanceID 79400). Save scene. AutoFindReferences resolve SEMUA: _rig, _agitatorRoot, _limestonePour, _neutralizedSurface, _filtrateChannel, _polishedFlow, _phNeedle, _phStatusGreen/Red, _beaconGreen/Red, _cakeBlocks[6], _rollers[10], _playerRigRoot, _teleportTargetDcs.

**PLAY-TEST Level 10 (MHP) END-TO-END (via reflection di play mode)**:
- MulaiLevel(Level11_MHP) + OnDcsButtonPressed(10) -> LevelActive=True, operator station terbangun (_doseButton active=True), pH=1.5.
- TryDose() x3: tahap1 pH 1.5->3.5 (Stage1Done), tahap2 (lime) pH->5.0 (Stage2Done), tahap3 (MgO) pH->7.5 + MHPQuality 0->92% + sampleFlow active (Stage3Done). Field stage = `_stageIndex` (BUKAN _stage).
- Paksa _sampleTaken + ShowLabCanvas() -> labCanvas BUILT; OnLabAccept() -> LabAccepted=True, QuestComplete=True, GLM._level11MhpComplete=True. (method MHP: TryDose, ShowLabCanvas, OnLabAccept; field _dosing, _stageIndex, _sampleTaken, _labCanvas)

**PLAY-TEST Level 11 (Tailing) END-TO-END**:
- MulaiLevel(Level12_TailingDischarge) + OnDcsButtonPressed(11) -> LevelActive=True, processStarted=True, operator station terbangun (_btn active), pH=2.3.
- TryAction() tahap1 NeutralizeRoutine -> pH 2.3->8.0 (NeutralizeDone), beaconGreen ON. TryAction() tahap2 FilterPressRoutine -> CakeMoisture 60->22% (FilterPressDone).
- Paksa _inspected + ShowQc() -> qcCanvas BUILT; OnAccept() -> ComplianceAccepted=True, QuestComplete=True, GLM._level12TailingFilterComplete=True. (method tailing: TryAction, ShowQc, OnAccept; field _busy, _stage, _inspected, _qcCanvas)
- Screenshot play-mode tailing field (view 26,8,133 -> 26,3,146): operator station + panel info terbangun di depan mesin.

**STATUS: KEDUA LEVEL (10 MHP + 11 Tailing) FULLY FUNCTIONAL & VERIFIED.** Keluar play mode, komponen tailing tetap ter-attach (tailingInstances=1 di Level13_DryStack_Field, scene saved). 0 compile error.

**CATATAN play-test reflection**: sampling/inspeksi pakai proximity (pemain jalan ke cup/konveyor) — di reflection di-paksa _sampleTaken/_inspected lalu panggil ShowLabCanvas/ShowQc langsung. Di VR/desktop asli pemain jalan ke target (radius MHP 3.2m, tailing 3.5m) untuk trigger natural. Tombol dosing/aksi: XR ray/poke ATAU keyboard SPACE/1; lab/compliance ACCEPT: XR ATAU Enter; submit lab MHP [L].

**Files**: Scene Level1.unity (tambah komponen Level12TailingFilterController ke Level13_DryStack_Field, saved). Tidak ada edit .cs baru sesi ini (verifikasi saja).


### 2026-05-31 (Part 32) — Level 12 (Dry Stack Tailing / pembuangan limbah AKHIR) jadi GAMEPLAY INTERAKTIF

**User**: "OKe lanjut level berikutnya Tailing Limbah?" = Level 12 display = enum Level13_TailingWaste (DCS 12, controller Level13DryStackController, voice 'dry stack aman'/'pH 8.5'/'dry stack safe'/'tailing safe').

**Research (web)**: Dry Stack Tailings Facility (DSTF) — cake yg sudah dinetralkan (pH~8.5) + di-dewater (moisture <25%) di-spread + DIPADATKAN dalam terraced lift di atas GEOMEMBRANE LINER -> timbunan UNSATURATED stabil (TANPA bendungan/kolam = anti-jebol, beda wet tailings dam). Closure: geomembrane cap + tanah + revegetasi. Monitoring: PIEZOMETER (pore pressure rendah), rembesan -> polishing pond -> WWTP. Sumber: Hatch LUCY Project, ResearchGate "Dry Stacking Fine Grained Nickel Residue in the Tropics", TBP Nickel (Indonesia Halmahera) Climate Change Waste Management, Davies "Filtered Dry Stacked Tailings Fundamentals", geomembrane liner review.

**Mesin DSTF (inspeksi)**: semua child Level13_DryStack_BlenderRig (rig @ 23,0.1,146) TAPI DSTF digeser jauh ke z~191-270 (Part 19). Objek: GDS_ContainPad @ (20,0,230) [pad/ground], GDS_Geomembrane @ (20,0.3,230) [liner], DryStack_Storage @ (20,0.4,230) -> DryStack_Pile_00..05 (terraced bench, y naik 1.7->4.4 lalu offset), DryStack_SafeCover @ (20,5.5,212) [rehab cap], GDS_Piezometer_0..3 + GDS_PiezoCap_0..3 (sudut x±, z199/261), GDS_MonitorWell @ (58.5,1.4,258), GDS_PolishPond_Water @ (-13.4,1,258), GDS_SignBoard @ (20,4,270) [B3], GDS_Berm_N/S/E/W, fence posts. SpawnPoint_Lvl13 @ (-15.2,0,131.5) DEKAT MESIN (z131), BUKAN di DSTF z230.

**REWRITE Level13DryStackController.cs (interaktif, 460 baris, validate 0 error, 3 warning benign)** — controller LAMA pasif (lakukan neutralization+filterpress+stacking, overlap Level 11). Baru FOKUS DSTF:
- DCS12 -> fade -> EnsurePadGround (tambah BoxCollider ke GDS_ContainPad biar pemain ada pijakan di DSTF) -> teleport COMPUTED (20,0.9,207) hadap +z (BUKAN SpawnPoint_Lvl13 yg di z131) -> BuildOperatorStation (tombol cube XRSimpleInteractable + info panel 3D billboard) @ (20,2.4,209.2).
- 2 tahap GATED (tombol XR/SPACE/1):
  * TAHAP 1 STACKING: StackRoutine 7s -> DryStackProgress 0->100%, DryStack_Pile_00..05 muncul progresif + tint coklat-abu (compacted), dust FX (DryStack_Dust_FX NULL di scene, di-guard).
  * TAHAP 2 CLOSURE: ClosureRoutine 6s -> DryStack_SafeCover muncul + tint hijau (rehab grass cap), GDS_PiezoCap_0..3 tint hijau (piezometer AMAN), GDS_PolishPond_Water tint keruh->jernih (rembesan).
- Info panel LIVE: proses + fungsi + progress/pH(8.5)/moisture(22%). pH & moisture nilai TETAP (sudah dicapai di Level 11).
- Proximity inspeksi (jalan ke DryStack_Storage radius 12m, DSTF besar) -> Compliance QC pop-up 3D (moisture 22% unsaturated, pH 8.5 baku mutu 6-9, geomembrane liner intact, 4 piezometer aman, rembesan->polishing pond->WWTP jernih, closure+revegetasi, VERDICT DSTF AMAN anti-jebol) + ACCEPT (XR+Enter) -> NotifyLevel13DryStackComplete -> lapor HT 'dry stack aman'.
- Public props: LevelActive/StackingDone/ClosureDone/Inspected/ComplianceAccepted/DryStackProgress/PHCurrent/CakeMoistureCurrent/QuestComplete. Helper SAMA pola MHP/Tailing (MakeText builtin font, OpaqueMat URP/Lit, AttachXrButton, GenNoise/GenChime, BillboardTo, TeleportTo) + EnsurePadGround baru.
- File via `write` overwrite (GUID/.meta tetap; komponen LAMA di Level13_DryStack_Field tetap ter-link; field lama serialized jadi orphan harmless, field baru null lalu di-resolve AutoFindReferences).

**PlayerHUD.cs**: checklist Level 12 (baris 914-917) diganti per-langkah baca FindFirstObjectByType<Level13DryStackController> (StackingDone/ClosureDone/Inspected/ComplianceAccepted) + voice keyword TETAP 'dry stack aman, pH 8.5' (sudah cocok GLM).

**VERIFIKASI**: compile (refresh scope=scripts; timeout 60s tapi compile beres, isCompiling=False). DryStack.StackingDone/ClosureDone props muncul. instances=1 di Level13_DryStack_Field (GUID preserved). AutoFindReferences resolve: _rig,_containPad,_geomembrane,_safeCover,_polishPondWater,_dryStackPiles[6],_piezoCaps[4]; _dustFx NULL (FX tak ada, di-guard). Console 0 compile error (cuma generators.ai.unity.com network). PLAY-TEST end-to-end (reflection): DCS12->station built (btn active)->TryAction stacking progress 0->100 (StackingDone)->TryAction closure (ClosureDone, safeCover active=True)->paksa _inspected+ShowQc (qcCanvas BUILT)->OnAccept (ComplianceAccepted=True, QuestComplete=True, GLM._level13DryStackComplete=True). Screenshot DSTF play-mode (view 20,14,198->20,4,234).

**STATUS: Level 12 (Dry Stack) FULLY FUNCTIONAL & VERIFIED.** Field stage = `_stage`. Method: TryAction, StackRoutine, ClosureRoutine, ShowQc, OnAccept. Tidak perlu save scene (tak ada perubahan objek scene; komponen sudah ter-attach, GUID preserved). 0 compile error.

**Files**: Assets/Scripts/Simulation/Level13DryStackController.cs (rewrite interaktif DSTF), Assets/Scripts/UI/PlayerHUD.cs (checklist Level 12 per-langkah).

**Sisa level**: Level 13 display = Level14_Emergency (Darurat K3/ESD) — belum dikerjakan interaktif.


### 2026-05-31 (Part 33) — Integrasi GUDANG PRODUK MHP ke Level 10 (stage akhir: bagging & dispatch, animasi smooth)

**User**: "boleh integrasikan. dan juga harus ada animasi yang sangat smooth ya! no bug" — gabungkan show-piece `MHP_ProductWarehouse_BlenderRig` ke Level 10 (enum Level11_MHP) sebagai stage AKHIR setelah Lab QC ACCEPT.

**Inspeksi (execute_code)**: gudang @ world: lantai `MHP_Yard_Pad` (103.4,1.7,148), `Bagging_ActiveBag_MHP_TopHeap` (106.41,3.04,155), `Bagging_FillChute` (106.41,3.26,155), `Bagging_Hopper` (y4.36), 8× `ExportBag_NN_FIBC_Bag` (~101-102,2.5,157.5) + masing2 `_MHP_TopHeap`. Controller `Level11MHPController` field stage = `_stageIndex` (0-2 dosing, 3 sampling, 4 lab, 5→repurpose jadi GUDANG, 6 report). Helper ADA: MakeText (builtin LegacyRuntime.ttf), OpaqueMat (URP/Lit), AttachXrButton (XRSimpleInteractable+BoxCollider+colliders.Add), Tint, GetCam/GetPlayerHead/BillboardTo, Start/Stop audio, GenNoise/GenChime, Child/FindChild/HasAny. TIDAK ada TeleportTo(pos,fwd) — kutambah baru (pakai XROrigin MoveCameraToWorldLocation+MatchOriginUpCameraForward).

**EDIT Level11MHPController.cs** (6 apply_text_edits, GUID preserved, bottom-to-top biar line stabil):
1. Sisip method block @line350 (sebelum BuildOperatorStation): StartWarehouseSequence (fade→EnsureWarehouseGround→TeleportTo (104,2,150) hadap +z→ResetWarehouseHeaps→BuildDispatchStation), TryDispatch, DispatchRoutine (8s, Mathf.SmoothStep, ApplyBaggingFill, fillStream+audio), ApplyBaggingFill (heap scale 0.04→full smoothstep; active bag fill [0,0.3], 8 export heaps stagger [0.3,1]; weigh kg counter), ResetWarehouseHeaps/RestoreWarehouseHeaps (scale heaps, base disimpan), UpdateWarehousePanel (billboard + info 40%Ni/3-4%Co/moisture48%/FIBC/dispatch%), SetFillStream/UpdateFillStream (cylinder runtime UV scroll + sin emission HALUS, no blink), EnsureWarehouseGround (BoxCollider ke MHP_Yard_Pad dari mesh bounds), BuildDispatchStation (cube button XR + quad panel + weigh display quad, semua OpaqueMat+MakeText), ShowDispatchButton/ShowWhPanel/HideDispatchStation, EnsureWarehouseRefs (GameObject.Find rig + Child/FindChild heaps + capture base scale), TeleportTo.
2. OnLabAccept (line338-342): HAPUS `_questComplete=true` + `NotifyLevel11MHPComplete()` langsung; ganti → `_stageIndex=5` + StartCoroutine(StartWarehouseSequence). **NotifyLevel11MHPComplete sekarang dipanggil HANYA di akhir DispatchRoutine** (stage 6).
3. Update() (line180): tambah input stage5 (SPACE/1 → TryDispatch), UpdateFillStream saat _dispatching, UpdateWarehousePanel saat _warehouseStarted.
4. OnLevelStarted: not-active branch + HideDispatchStation/SetFillStream(false)/RestoreWarehouseHeaps; active reset + warehouse flags + EnsureWarehouseRefs + RestoreWarehouseHeaps (showcase tetap full bag sebelum stage gudang).
5. Public props (line99): `BaggingDone`, `DispatchProgress`.
6. Fields (line88, semua PRIVATE non-serialized): _warehouseRig/_warehouseFloor/_baggingHeap/_exportHeaps[], _dispatchButton/_dispatchLabel, _whPanel/_whText, _weighDisplay/_weighText, _fillStream/_fillStreamMat, _warehouseStarted/_dispatching/_baggingDone, _dispatchProgress/_dispatchDuration=8f, _baggingHeapBase/_exportHeapBase[].
- Fix tambahan: `_dispatching=false` di akhir DispatchRoutine (biar UpdateFillStream berhenti).

**PlayerHUD.cs**: tambah baris checklist Level 10 (dalam blok `if(l10!=null)`, sebelum `}` line888): `Check(l10.BaggingDone)` "Bagging & dispatch produk MHP ke refinery".

**VERIFIKASI**: validate 0 error (3 warning benign). Compile scope=scripts (JANGAN scope=all force). read_console: cuma error jaringan generators.ai.unity.com (benign). **PLAY-TEST reflection**: EnsureWarehouseRefs resolve rig=MHP_ProductWarehouse_BlenderRig, floor=MHP_Yard_Pad, baggingHeap OK, exportHeaps=8, baseScale=(1,1,1). ResetWarehouseHeaps→0.04. GLM `_level11MhpComplete`=False SEBELUM dispatch (✓ Notify tak terpanggil di lab accept). Set _dispatchDuration=0.25 + TryDispatch → setelah selesai: baggingDone=True, dispatchProgress=100, questComplete=True, stageIndex=6, baggingHeap & export heaps scale=(1,1,1) (terisi smooth), floorCollider=True, panel+weigh built, button hidden saat dispatch. Screenshot gudang play-mode (view 99,6.5,148→105,3,156) render OK.

**STATUS: Stage gudang Level 10 FULLY FUNCTIONAL & VERIFIED.** Animasi: heap fill smoothstep, fillStream UV scroll+sin emission halus (no kelap-kelip), weigh kg counter, export bags stagger fill. NotifyLevel11MHPComplete HANYA setelah dispatch 100%. Semua null-guarded. **TIDAK perlu save scene** (dispatch station dibangun runtime, refs di-resolve runtime via GameObject.Find; controller sudah ter-attach di Level11_MHP_Field, warehouse rig sudah di scene; field warehouse non-serialized).

**Alur Level 10 lengkap sekarang**: DCS10 → 3 dosing (pH 1.5→7.5, MHP 92%) → sampling → Lab QC ACCEPT → **fade→teleport GUDANG (104,2,150)** → tombol BAGGING & DISPATCH (XR/SPACE) → DispatchRoutine 8s (bag terisi + 8 export bag terisi + weigh kg + fillStream + panel info) → NotifyLevel11MHPComplete → lapor HT 'MHP terbentuk' → transisi Level 11.

**Files modified**: Assets/Scripts/Simulation/Level11MHPController.cs (6 edit + 1 fix, GUID utuh), Assets/Scripts/UI/PlayerHUD.cs (1 baris checklist). Scene TIDAK diubah/save.


---

### 2026-05-31 (Part 34) — ORE CRUSHER (Level 3 area) REDESIGN v3 high-fidelity + crushed ore + belt animasi

**User**: "GAS REMODEL" mesin Ore Crusher (Level 3). High-fidelity Blender headless, UV texture realistik spt mesin sebelumnya, belt eskalator bergerak, ore = CRUSHED kecil (bukan boulder), muncul saat conveyor NYALA. Respon Indonesia.

**RESEARCH**: Nickel Institute — laterit basah di-crush/screen (buang coarse) sebelum slurry prep (<2mm). Flow Tambang→Crusher→Slurry Tank→Pre-Heater. Roadmap penjelasan_lengkap sec Crusher.

**SCENE LAMA**: Rig `Crusher Ore`(84.3,9.1,48.5)/Mesin Utama → `Crusher_Ore_Water_Process_Industrial`(0,0,0) → `L2_Blender_UV_OreCrusher_Escalator_Redesign` (flat ~200 part L2_V2_*). Belt `L2_V2_Wide_Inclined_Rubber_Ore_Belt` ctr(118.9,6.6,56.9) size(32,3.4,4.8), tail(134.9,5.1,57) crusher-end → head(102.9,8.2,56.8) discharge, inklinasi naik 3.1m.

**CONTROLLER `Level3OreSlurryController.cs`** (2283→2295 baris) SUDAH animasi conveyor: `_pakaiOreAsliDariBelt=true` → ore asli di belt (nama mengandung `rounded_ore_rock_on_belt`) di-cache `CacheSceneOreOnBelt`, digerakkan `UpdateSceneOreOnBelt`/`UpdateRuntimeOreConveyor` (path start/mid/end), `UpdateOreBeltMaterial` scroll `_oreBeltMaterial` via `_BaseMap`/`_MainTex` offset → **belt mesh WAJIB punya texture**. `NamaOreConveyorBekas` auto-hide objek mengandung `conveyor` dalam 45m dari slurry tank → HINDARI 'conveyor' di nama struktur baru.

**BUILD** `Assets/Art/Level2OreCrusherBlender/build_ore_crusher_v3.py` (headless, 217 obj): finalize bevel(harden_normals)+cube_project UV+shade_auto_smooth. Texture FlashCCD UV set (BrushedSteel/316L/DarkRubber/Hazard/SafetyYellow/Concrete/ThickUnderflow-BrownPurple ore/ChemicalPump-Blue/EmergencyRed). Komponen: ROM flared hopper+throat, primary crusher body+jaw liners+mouth recess+2 flywheel+hub+drive motor+guard+skid+concrete pad, inclined rubber belt SURFACE (textured), head/tail pulley, 15 trough roller, L/R hazard skirt+deep truss+diagonal+`Jumbo_Belt_Support`+foot, `Wide_Maint_Catwalk`+rail, discharge chute+rubber lip, service stair+platform+rail, e-stop+lamp+badge+bolt. Ore: 26 chunky `Rounded_Ore_Rock_In_Hopper` (0.42-0.85m) + **60 CRUSHED angular `Rounded_Ore_Rock_On_Belt_NN` (0.11-0.27m)** scatter di permukaan belt.

***KOORDINAT (Part 22 convention) — KONFIRMASI ULANG***: author Unity-world, `u2b(ux,uy,uz)=Vector((-ux,-uz,uy))`, dims `dsz=swap(y,z)`, export `FBX_SCALE_ALL+bake_space_transform`, axis_up=Y axis_forward=-Z, instance **IDENTITY (pos0,rot0,scale1)** → landing TEPAT di world coords. `box()`/`cyl()` (location pakai u2b langsung) landing benar. **BUG**: `aligned_box` versi-1 pakai `transform_apply(rotation)` setelah rotation_difference → centroid GESER di Y (belt landed Y=-4.8 bukan 6.6; box/cyl tetap benar). **FIX**: aligned_box dibangun dari **8 vertex eksplisit** (hitung sudut di Unity-space dgn basis L/S/N lalu konversi u2b each, from_pydata) — bulletproof. Setelah fix: belt ctr(118.9,6.7,56.9) size(32,3.3,4.4) minY5.0→maxY8.3 (inklinasi benar). ***LESSON: untuk part ber-rotasi via Blender→FBX bake, JANGAN transform_apply(rotation); bangun dari vertex eksplisit di target-space lalu konversi.***

**REPLACE**: DestroyImmediate `L2_Blender_UV_OreCrusher_Escalator_Redesign` lama, InstantiatePrefab v3 → world transform identity → SetParent(`Crusher_Ore_Water_Process_Industrial`, worldPositionStays=true) → rename sama. Belt ctr setelah parent (118.9,6.7,56.9) TEPAT. Controller `_oreBeltVisual`=belt baru, `_oreStartPoint/Mid/End`=null (auto-recompute via EnsureOrePathRuntime). Scene saved.

**EDIT controller** (apply_text_edits, GUID utuh): tambah method `HideSceneOreOnBelt()` (set semua _sceneOrePieces inactive) + panggil di `SetConveyorOreFxAktif` saat `!aktif && _pakaiOreAsliDariBelt`. `SetConveyorOreFxAktif(false)` dipanggil di start (line260) + reset (515/620/642); `(true)` saat sequence ore jalan (line497). → **ore on-belt HIDDEN saat conveyor off, MUNCUL saat nyala**. Validate 0 error.

**VERIFIKASI** (compile scope=scripts, timeout 60s tapi beres, 0 error; cuma jaringan generators.ai.unity benign). **PLAY-TEST reflection**: HideSceneOreOnBelt exists=True, _oreBeltVisual=belt baru. SetConveyorOreFxAktif(false)→oreActive=0 (HIDDEN). SetConveyorOreFxAktif(true)→**60 ore MUNCUL aktif**; step UpdateRuntimeOreConveyor 30x → ore00 gerak dist=11.8 sepanjang belt menuju tank; belt `_BaseMap` offset (0,0)→(0,-0.38) **SCROLL** OK. Screenshot crusher rig render OK (texture loaded, struktur lengkap). Stop play.

**STATUS: ORE CRUSHER v3 FULLY FUNCTIONAL & VERIFIED.** Belt scroll + crushed ore muncul-saat-nyala + bergerak ke tank. Konvensi koordinat Part 22 re-konfirmasi + aligned_box vertex-eksplisit fix. 

**Files**: Assets/Art/Level2OreCrusherBlender/build_ore_crusher_v3.py (BARU), Level2_OreCrusher_IndustrialUV_v3.fbx (BARU, 217 obj), Assets/Scripts/Simulation/Level3OreSlurryController.cs (HideSceneOreOnBelt + call), Assets/Scenes/Level1.unity (replace crusher rig, saved).

**Sisa level**: Level 13 display = Level14_Emergency (Darurat K3/ESD) — belum interaktif (deferred user).


---

### 2026-05-31 (Part 35) — ORE CRUSHER v4: belt nyambung crusher↔slurry tank + black box spawn + sekuens startup (sirine+jerk) + HD PBR

**User (Indonesia)**: (1) BUG belt/eskalator & crusher ada GAP → harus NYAMBUNG tanpa gap; (2) Black box di crusher = titik SPAWN ore keluar dari mesin; (3) Belt BERSIH di awal (hapus 60 static rock); (4) Sekuens saat task selesai: mesin ON + SIRINE → eskalator dorongan MUNDUR lalu NAIK → baru ore keluar dari black box; (5) animasi eskalator realistis (ore di belt naik pelan); (6) belt+crusher WAJIB nyambung ke SLURRY TANK (riset industrial); (7) redesign slurry tank + water tank; (8) UV/texture crusher+tank HD realistik.

**RISET (web)**: Kearl Lake/US Patent 8388831/Nickel Institute HPAL → crusher discharge → inclined feed conveyor → discharge ke TOP slurry/mixing tank (via chute/hood) + air + agitator. Belt head harus OVER tank dgn chute drop ore ke inlet.

**KEPUTUSAN tank**: rig Slurry Tank terlalu kompleks (150+ child, banyak controller hook: Slurry_Fill, Agitator, Dark_Recessed_Ore_Inlet, dll) untuk rebuild geometri → upgrade MATERIAL ke HD PBR di Unity (FBX gak bisa bawa node Blender; URP/Lit normal map+metallic+smoothness+tiling = path HD yang benar).

**SISTEM PATH CONTROLLER (penting)**: `EnsureOrePathRuntime` auto-hitung: start=`HitungFallbackOreStartPoint` (ujung belt JAUH dari tank = sisi crusher), mid=`Steel_Discharge_Chute_Into_Inlet`/`Dark_Recessed_Ore_Inlet` (inlet tank), end=permukaan slurry. `_pakaiOreAsliDariBelt=false` → runtime spawn ore (`BuatRuntimeOreConveyorFx`, 34 pieces) bergerak sepanjang path. Belt scroll via `UpdateOreBeltMaterial` (offset Y NEGATIF=maju, POSITIF=mundur) cuma jalan saat `_runtimeOreConveyorAktif`. **Maka: belt panjang nyambung crusher→tank → ore otomatis spawn di sisi crusher (black box) → diangkut ke tank.**

**BUILD `build_ore_crusher_v4.py`** (172 obj, evolve v3, konvensi Part 22: u2b=(-ux,-uz,uy), dsz swap y/z, aligned_box dari 8 vertex eksplisit anti-bug-rotasi, export FBX_SCALE_ALL+bake, instance IDENTITY):
- Belt 1 PANJANG: TAIL=(140,2.7,56.5) crusher black box LOW → HEAD=(99.8,9.2,55.4) over tank rim HIGH. Landed: ctr(119.9,6.0,56.0), minX99.7→maxX140.1 (nyambung dua-duanya, no gap).
- `L2_V2_Crusher_Discharge_BlackBox` (141,2.7,56.4) dark box = SPAWN + lip rubber.
- `L2_V2_Heavy_Discharge_Chute_To_Tank` (98,7.9,55.3) over tank + rubber lip + hood.
- Belt BERSIH: 0 `Rounded_Ore_Rock_On_Belt` (hapus 60 static rock v3). Hopper feed 22 chunky rock tetap.
- HD texel: cube_project uv lebih kecil (1.0-2.4 = lebih banyak tiling = crisp) + bump node dari texture (micro-relief). 19 trough roller, head/tail pulley, skirt/truss/support nyambung belt panjang. Struktur pakai nama `Belt_Support`/`Maint_Catwalk` (HINDARI 'conveyor'). Belt keep substring `wide_inclined_rubber_ore_belt`.

**REPLACE Unity**: DestroyImmediate old `L2_Blender_UV_OreCrusher_Escalator_Redesign` → InstantiatePrefab v4 → identity → SetParent(Crusher_Ore_Water_Process_Industrial, worldPositionStays) → rename. Controller: `_oreBeltVisual`=belt v4, `_oreStartPoint/Mid/End`=null, `_pakaiOreAsliDariBelt`=FALSE (clean belt + runtime spawn). Scene saved.

**HD PBR MATERIAL (Unity execute_code)**: Buat normal map `UV_SteelDetail_Normal.png` (copy UV_BrushedSteel_Grey.png → TextureImporter NormalMap + convertToNormalmap + heightmapScale 0.06). Apply ke 7 material (metallic/smoothness/_BumpMap+_NORMALMAP keyword/_BaseMap tiling): Nickel_Slurry_Tank_Industrial_Green, Nickel_WaterTank_PaintedSteel, Agitator_Metal, Nickel_Crusher_DarkSteel, M_L2_BrushedSteel, M_L2_CrusherHull, M_L2_ProcessBlue.

**EDIT controller `Level3OreSlurryController.cs`** (apply_text_edits, GUID utuh, validate 0 error, compile 0 error):
- Tambah `StartupMesinDanEskalator()` coroutine + `SetBeltOffsetRuntime(v)` helper + `GenSirenClip(dur)` (sirine prosedural sweep 480Hz wail 1.2Hz + rumble 68Hz). Self-contained (no field baru — AudioSource lokal "Level3_Mesin_Siren_Audio" dibuat+destroy).
- Phase1 jerk MUNDUR 0.5s (belt offset +0.07*sin, spinner flywheel/pulley CCW). Phase2 eskalator NAIK ramp 2.6s SmoothStep (belt offset = -_runtimeOreBeltOffset maju, spinner CW) + sirine loop.
- Dipanggil di `MainkanSequenceOreSlurry` SEBELUM `AnimasikanOreMasukKeTank` (line 456). Jadi: machine ON+sirine → jerk mundur → eskalator naik → BARU `AnimasikanOreMasukKeTank` (`SetConveyorOreFxAktif(true)` → 34 runtime ore spawn dari sisi crusher black box → travel ke tank → drop ke slurry).

**VERIFIKASI play-test (reflection)**: StartupMesinDanEskalator jalan → belt _BaseMap offset (0,-0.41) FORWARD (eskalator naik) + sirine obj dibuat+dibersihkan. SetConveyorOreFxAktif(true) → 34 runtime ore (root Level3_Runtime_Ore_Belt_Flow) muncul di belt (ore x118.8 mid-belt) + belt scroll lanjut (-0.66). Screenshot render OK (HD texture, struktur nyambung). 0 error (cuma XR haptic + jaringan generators.ai.unity benign). Stop play → serialized PERSIST: _oreBeltVisual=belt v4, _pakaiOreAsliDariBelt=False, crusher v4 in scene (blackbox+chute, 0 static_ore_on_belt).

**STATUS: ORE CRUSHER v4 FULLY FUNCTIONAL & VERIFIED.** Belt nyambung crusher↔slurry tank (no gap), belt bersih, black box spawn, sekuens sirine+jerk+naik, HD PBR material crusher+tank, ore spawn dari black box → angkut → masuk tank.

**Files**: Assets/Art/Level2OreCrusherBlender/build_ore_crusher_v4.py (BARU), Level2_OreCrusher_IndustrialUV_v4.fbx (BARU 172 obj), Assets/Scripts/Simulation/Level3OreSlurryController.cs (StartupMesinDanEskalator+SetBeltOffsetRuntime+GenSirenClip+call), Assets/Art/FlashCCDIndustrialUVRedesign/Textures/UV_SteelDetail_Normal.png (BARU normal map), Assets/Scenes/Level1.unity (replace crusher v4, _pakaiOreAsliDariBelt=false, saved).

**CATATAN**: HD slurry tank/water tank = via MATERIAL PBR (geometri tank tetap, controller hook utuh). Kalau user mau geometri tank betul-betul di-rebuild, harus preserve semua nama child controller-tied (Slurry_Fill, Agitator+Hub+Blade, L4_LiquidStartPoint, Slurry_Tank_Out, Dark_Recessed_Ore_Inlet, dll) + Unpack+reparent pattern.



---

### 2026-05-31 (Part 36) — ORE CRUSHER v4 polish: JAW CRUSH + flywheel spin-in-place + dust FX (gap "terbaik")

**User**: "kerjakan yang task ore crusher yang kurang ini ya! YANG TERBAIK POKOKNYA!" — isi gap supaya crusher v4 production-quality.

**Analisis gap**: 6 fitur v4 (belt nyambung, belt bersih+black box spawn, sekuens sirine+jerk+naik, HD PBR, 34 runtime ore, belt scroll) SUDAH ada. Grep `Level3OreSlurryController.cs` + inspeksi rig confirm yg ANIMATED: belt scroll, flywheel/pulley spin (saat startup), runtime ore conveyor, slurry fill naik, water flow FX, bubble FX. Yg KURANG (paling autentik crusher) = **JAW CRUSHER STATIS** (5 jaw obj ada tapi diam) + **NO DUST FX**.

**Part crusher (rig `L2_Blender_UV_OreCrusher_Escalator_Redesign`, identity, child local==world)**:
- Jaw liner: `L2_V2_Left_Smooth_Jaw_Liner` rendCtr z54.8, `..._Right_..._Liner` z57.6 (chamber center z56.2). **PIVOT di ORIGIN (0,0,~0), geometri baked-offset** → translate `.position` menggeser mesh world.
- Flywheel: `L2_V2_Jumbo_Flywheel_A/B_Smooth` + `Flywheel_Hub_A/B`, rendCtr ~(150.4,3.0,53.2/59.2), **PIVOT JUGA di ORIGIN**.
- Mouth recess `L2_V2_Dark_Jaw_Mouth_Recess` (141.5,4.8,56.2); black box discharge (141.4,3.3,56.4).

**EDIT `Level3OreSlurryController.cs`** (script_apply_edits anchor_replace, GUID utuh, validate 0 error, compile scope=scripts 0 error):
- Tambah fields `_crusherFxAktif` (bool) + `_crusherDustGo` (GameObject).
- `CrusherCrushFxLoop()` coroutine: kumpulkan jawL/jawR + flywheel (`Contains("flywheel")` → 4 obj), capture base `.position` + flyCtr (renderer bounds center sbg pivot tetap). While `_crusherFxAktif`: jaw squeeze ±0.16*|sin(ph)| di world-Z (tutup ke center), flywheel `RotateAround(flyCtr[i], Vector3.forward, 430*dt)`, dust manual `Emit(2)` tiap 0.07s. Reset jaw + stop dust di akhir.
- `EnsureCrusherDust(worldPos)`: PS box di mouth (141.5,5.6,56.2), startColor coklat α0.45, rate 24, **material `Sprites/Default` tinted (anti-magenta)**.
- Start: `_crusherFxAktif=true; StartCoroutine(CrusherCrushFxLoop())` di awal `StartupMesinDanEskalator`.
- Stop hook: `if(!aktif) _crusherFxAktif=false;` di `SetConveyorOreFxAktif` → mati otomatis di semua stop point (line 260 start, 595 akhir AnimasikanOreMasukKeTank, 700/722 reset). Jadi crusher nyala saat startup → ore → tank, lalu winds down.
- Inline spinner di startup diubah `flywheel||pulley` → `pulley` saja (flywheel sekarang dimiliki CrusherFxLoop, hindari double-spin).

**2 BUG ditemukan & fixed via play-test reflection**:
1. **Flywheel ORBIT bug**: `Rotate(Vector3.forward, Space.Self)` pada pivot-di-origin bikin geometri MENGORBIT world-origin (flyA rendCtr y JATUH ke -31.25!). **FIX**: `RotateAround(capturedRendCenter, Vector3.forward, deg)` → spin di tempat (y tetap 3.0). LESSON: untuk part FBX dgn pivot di origin + geometri baked-offset, ROTASI pakai RotateAround(bounds.center), bukan Rotate(Space.Self); TRANSLASI pakai .position aman.
2. **Dust rateOverTime gak auto-emit**: PS dibuat runtime via AddComponent, `isEmitting=True` + rate=24 tapi `particleCount=0` (quirk PS runtime). `Emit(8)` works → manual. **FIX**: emit manual `dust.Emit(2)` tiap 0.07s di loop. LESSON: PS runtime-created sering tak auto-emit rateOverTime; pakai Emit() manual yg terbukti jalan.

**VERIFIKASI play-mode (reflection)**: start CrusherCrushFxLoop → dustCount=72, flyA rendCtr (150.40,**3.00**,53.20) rotZ berubah (spin di tempat), jawL rendCtr z 54.8→54.96 (squeeze). Stop play → no persist (semua runtime). Screenshot crusher (view 140,12,38→145,4,56) — agak gelap tapi mekanik terverifikasi numerik.

**STATUS: ORE CRUSHER v4 — jaw crush + flywheel spin-in-place + dust FX DITAMBAH & VERIFIED.** Crusher kini punya signature crushing motion. TIDAK perlu save scene (FX runtime, jaw movement runtime, tak ada objek scene berubah). Cuma 1 file .cs diedit (GUID utuh).

**Files**: Assets/Scripts/Simulation/Level3OreSlurryController.cs (CrusherCrushFxLoop + EnsureCrusherDust + fields + start/stop wiring). Scene TIDAK diubah. Temp screenshot Assets/Screenshots/screenshot-20260531-200552.png (harmless, DeleteAsset diblok safety).


---

### 2026-05-31 (Part 37) — SLURRY TANK (Level 3) upgrade: agitator aduk + pipa slurry->preheater + material natural

**User**: "UPGRADE LAGI!" area Slurry Tank biar proses slurry KELIHATAN: (1) agitator berputar mengaduk (air+ore), (2) pipa dari tank/pump -> Preheater (lewat slurry pump existing) + animasi flow, (3) material/lighting natural (orange emissive band + steel flat + slurry ungu = gak natural).

**SEMUA 3 DELIVERABLE SELESAI & VERIFIED play-mode (compile 0 err, scene saved).**

**(4) MATERIAL natural (execute_code shared .mat + SaveAssets)**: M_L3_OrangeBand emission KILL (was 0.50,0.23,0.03) + base orange-brown (0.62,0.30,0.10) met0 smo0.35; Agitator_Metal emission kill met0.85 smo0.52; Slurry_Fill base ungu(0.42,0.18,0.55)->muddy brown(0.34,0.27,0.19) emisi near-off met0 smo0.55; M_Level3_SlurryWaterTanks_UVAtlas met0.55 smo0.42; Nickel_Slurry_Tank_Industrial_Green met0.6 smo0.45; M_L3_BrushedSteel met0.6; Nickel_Crusher_DarkSteel met0.55. Helper killEmi (disable _EMISSION + EmissiveIsBlack saat <0.02) + setPbr (_Metallic/_Smoothness/_Glossiness).

**(2) AGITATOR (Level3OreSlurryController.cs, 2 anchor_replace)**: Controller SUDAH punya sistem agitator (UpdateRuntimeAgitator, _agitatorVisibleParts, MulaiAgitatorJikaPerlu, CacheVisibleAgitatorParts) TAPI cuma cache AgitatorVerticalShaft (spin invisible) + start setelah final report. FIX: di awal AnimasikanIsiSlurrySampaiBatas() (sesudah SiapkanSlurryFillUntukIsi()) tambah `if(_agitatorVisibleParts.Count==0)CacheVisibleAgitatorParts(); EnsureSlurryImpeller(); _runtimeAgitatorAktif=true;`. Field baru _slurryImpellerGo + method EnsureSlurryImpeller(): cari CariTransformNamaContains("AgitatorVerticalShaft") utk center XZ (fallback _slurryFill/transform), bikin GO L3_SlurryAgitator_RuntimeImpeller @(shaft.x,3.6,shaft.z) parent controller transform, 2 tier x 4 paddle cube (localPos=Quaternion.Euler(0,ang,0)*Vector3(1.05,tier*1.15,0), rot Euler(0,ang,20), scale 1.35,0.12,0.5) + shaft-stub cylinder, semua Agitator_Metal, collider Destroy, lalu add impeller root ke _agitatorVisibleParts. Idempotent (guard _slurryImpellerGo!=null). Rotasi: UpdateRuntimeAgitator RotateAround(center=HitungPusatAgitatorVisible) + Rotate(up) tiap part. **VERIFIED play**: impeller @(91.4,3.6,55.1) 9 children, blade rotated 32.8deg/40frames (orbit di poros). NOTE: impeller submerged y=3.6 (posisi aduk realistis di dalam slurry; kalau ketutupan tank top bisa naikkan y).

**(3) PIPA slurry->preheater (Blender headless)**: build_slurry_to_preheater_pipe.py -> SlurryToPreheater_Pipe.fbx. Konvensi Part 22 (u2b(ux,uy,uz)=Vector((-ux,-uz,uy)), FBX_SCALE_ALL+bake_space_transform, axis -Z/Y, instance IDENTITY). Waypoints Unity world (93.6,1.6,44)->(93.6,5.6,44)->(19.2,5.6,44.4)->(18,4.6,44.4): dari slurry pump z44 ke PREHEATER instance INLET z44 (pilih z44 bukan z56 spy run lurus + tak clip slurry tank z51-59). Outer X-ray glass r0.34, inner flow r0.24 (JOIN 1 mesh SlurryToPreheater_SlurryFlow utk animator), flange 2 ujung + 5 support column (x=82,68,54,40,27) + feet. Bucket nama: *_XRayGlass* (5 seg+elbow), *_Flow*/SlurryToPreheater_SlurryFlow (1 joined), *_Steel* (12). Unity .mat di Assets/Art/SlurryToPreheaterPipe/: M_SlurryPipe_XRayGlass (URP/Lit transparent _Surface=1 ZWrite off SrcBlend5/DstBlend10 base(0.62,0.72,0.82,0.30) met0.4 smo0.85), M_SlurryPipe_Flow (brown 0.34,0.26,0.18 emission 0.30,0.18,0.08 RealtimeEmissive), M_SlurryPipe_Steel (0.58,0.6,0.63 met0.7 smo0.42). ProcessPipeFlowAnimator attach ke SlurryToPreheater_SlurryFlow GO (_flowRenderers={flowR}, _fluidColor=(0.42,0.26,0.12), _flowOnStart=true). **VERIFIED play**: SetFlowing(true) -> flowing=True. Pipe center (55.9,2.8,44.2) size (76.6,6.3,1.6). Scene SAVED (pipa objek scene permanen, parent "Mesin Utama").

**Controller sha256 baru**: e7c1b3557b643ece4d7c8f5991c9314e30ef5f9f7be1730f440323083c396af0.

**Files**: Assets/Scripts/Simulation/Level3OreSlurryController.cs (EnsureSlurryImpeller + impeller start early), Assets/Art/SlurryToPreheaterPipe/build_slurry_to_preheater_pipe.py (BARU), SlurryToPreheater_Pipe.fbx (BARU), Assets/Scenes/Level1.unity (pipe instance + materials assigned + PPFA, saved). Temp screenshots di Assets/Screenshots (harmless, DeleteAsset diblok safety).



---

### 2026-06-01 (Part 38) — SLURRY TANK Level 3 REBUILD: OPEN-TOP + visible PENGADUK (agitator) + ore-path (0,0,0) fix

**User**: "harusnya tengahny kosong dan ada pengaduknya! BUAT ULANG" — slurry tank tampil seperti tutup tertutup solid; harus OPEN-TOP (tengah hollow kelihatan) + ada pengaduk/impeller terlihat dari atas di editor.

**Root cause**: shell dibangun pakai `primitive_cylinder_add` (DEFAULT CAPPED) -> tutup atas solid putih = end-cap silinder. Impeller blades cuma dibuat runtime via C#, tak ada di model statik.

**REBUILD (build_slurry_water_tank_v2.py, 5 strReplace, FBX re-export objects=356)**:
- `open_tube()` helper BARU: `primitive_cylinder_add(end_fill_type='NOTHING')` + SOLIDIFY modifier (thickness 0.16, use_rim) APPLIED -> tabung open-top dinding-tebal interior kelihatan. Shell pakai ini (R6.5, H7.55).
- Full-disk surface ring DIGANTI low resting pool (`RestingSlurryPool` @ y1.05, R6.28, H1.5) supaya tengah tetap kosong.
- Shaft+hub DIGANTI: `StirrerColumn_Static` (y5.6) + `StirrerHub_Static` (y2.7) + 2 tier x 4 paddle `StirrerBlade_Static_*` (8 total, inner@0.42r outer@2.25r). Nama HINDARI keyword cache-filter ("agitator"+"blade"/"shaft", "impellerblade") -> tetap kosmetik statik, tak ganggu rotasi runtime.
- Ore inlet rename `Steel_Discharge_Chute_Into_Inlet` -> `OreInletRecess_Plate` (maksudnya: FBX baru tak resolve ke baked-(0,0,0)).

**KEY FBX FACT (re-confirmed)**: box/cyl/cone children FBX = transform.position (0,0,0) + geometri baked di world coords. open_tube (pakai SOLIDIFY modifier) malah LANDED dengan transform.position benar (91.41,4.08,55.14). torus + C# GameObjects = real transform.position. => static FBX blades TAK BISA diputar controller (pakai transform.position + filter 20m dari slurry center yg exclude (0,0,0)). Maka impeller RUNTIME C# tetap yg berputar; static blades kosmetik DISEMBUNYIKAN saat runtime impeller jadi (hindari dobel).

**Rig re-replace (execute_code)**: DestroyImmediate old rig -> InstantiatePrefab v2 -> identity -> SetParent(Mesin Utama,true) -> world identity (localScale=invers lossyScale Mesin Utama) -> rename `Level3_SlurryWaterTanks_Industrial_UV_Auto` (356 children) -> recreate empty marker `L3_SlurryTank_AgitatorVerticalShaft` @ (91.41,5.0,55.14) [C# GameObject, authoritative utk EnsureSlurryImpeller XZ centering] -> 7 TextMesh labels -> save scene.

**Controller edit 1 (Level3OreSlurryController.cs)**: di EnsureSlurryImpeller(), sebelum `_agitatorVisibleParts.Add`, hide static cosmetic blades:
```csharp
foreach (var _st in CariSemuaTransformTermasukInactive())
    if (_st != null && _st.name.IndexOf("StirrerBlade_Static", System.StringComparison.OrdinalIgnoreCase) >= 0 && _st.gameObject.activeSelf)
        _st.gameObject.SetActive(false);
```

**BUG DITEMUKAN saat play-test & FIXED — ore path lewat origin (0,0,0)**:
- `EnsureOrePathRuntime` resolve `_oreMidPoint` via `CariTransformNamaContains(... "Steel_Discharge_Chute_Into_Inlet" ...)`. Rig LAMA inactive `Mesin Utama/Slurry Tank/L3_SlurryTank_v2_Rig` MASIH punya child nama itu (transform.position 0,0,0; renderer bounds center BENAR (97.7,8,55)). Lerp ore pakai `_oreMidPoint.position` LANGSUNG (line ~861/865/1650/1958) -> ore lewat world origin = path rusak. (Bug ini PRE-EXISTING, bukan dari rebuild, tapi muncul/ketauan sekarang.)
- **FIX (2 edit, validate 0 err, compile 0 err)**:
  1. Setelah name-chain resolution, guard BARU: jika `_oreMidPoint.position.sqrMagnitude < 0.0025f` (baked FBX di origin), rebuild `_runtimeOreMidPoint` di `GetComponentInChildren<Renderer>(true).bounds.center` (fallback HitungFallbackOreMidPoint).
  2. Override line `if (_runtimeOreMidPoint != null && _oreMidPoint == _runtimeOreMidPoint)` ditambah `&& _runtimeOreMidPoint.position.sqrMagnitude < 0.0025f` supaya tak menimpa nilai renderer-bounds.
- **VERIFIED play**: _oreMidPoint kini (97.70,8.00,55.00) [Level3_Runtime_Ore_MidPoint] BUKAN (0,0,0); ore0 jalan 127->117 sepanjang belt (Y naik 4.6->5.8, no origin dip). Start (127.14,4.45,56.89), End (88,-0.3,55.01).

**VERIFIED total (play-mode reflection)**: open shell 400 verts solidified (size 13.16x7.55x13.16 = hollow open-top), RestingSlurryPool ada, StirrerColumn/Hub/8 Blade statik ada; RuntimeImpeller @ (91.41,3.60,55.14) 9 children + BERPUTAR (rotY 220->340 delta120); static blades activeSelf=0 hidden=8 (NO dobel impeller). Marker AgitatorVerticalShaft @ (91.41,5.0,55.14). Compile 0 error.

**CATATAN penting (FBX baked + nama-chain)**: kalau rename objek di rig BARU supaya tak match name-chain, rig LAMA inactive yg masih punya nama itu akan ke-pick (baked 0,0,0). Solusi umum: resolve transform yg baked-(0,0,0) -> pakai renderer.bounds.center, JANGAN transform.position. Pattern guard ini sekarang ada di EnsureOrePathRuntime.

**Console**: 0 compile error. Ada 3 "Script error (LevelXController): Start() can not take parameters" utk Level11MHPController/Level12TailingFilterController/Level13DryStackController — TIDAK disentuh sesi ini (di luar scope slurry tank), kemungkinan latent (helper bernama Start berparameter). Sisanya benign baseline (OpenXR loader, XR haptic, generators.ai.unity network).

**Files**: Assets/Art/Level3SlurryWaterTankBlender/build_slurry_water_tank_v2.py (5 edit), Level3_SlurryWaterTank_IndustrialUV_v2.fbx (356 obj), Assets/Scripts/Simulation/Level3OreSlurryController.cs (hide-static-blades + ore-mid (0,0,0) guard + override guard), Assets/Scenes/Level1.unity (rig replace + marker + labels, saved).

**TODO next (opsional)**: 3 controller (MHP/Tailing/DryStack) punya "Start() can not take parameters" — cek & rename method Start berparameter jadi nama lain bila user mau (di luar scope sesi ini).



---

### 2026-06-01 (Part 39) — Pipa L3 Slurry Tank → Slurry Pump → Preheater (X-ray + slurry ore flow)

**User**: buat pipa nyambung dari `L3_SlurryTank_FrontInspection_DarkGasket` ke `L5_CleanOutlet_Flange` (DUA flange) + tambahan pipa DIATAS slurry pump (bantuan tenaga dari slurry tank ke preheater dibantu slurry pump), DIDALAM pipa ada SLURRY ORE + animasi mengalir dari pipa slurry ke preheater. "GERAK TEKNIK FISIKANYA MAIN".

**Koordinat (renderer.bounds.center, verified)**: gasket `L3_SlurryTank_FrontInspection_DarkGasket` (84.67,4.04,55.15); DUA `L5_CleanOutlet_Flange` (nama sama, satu per preheater instance): A=(20.57,9.89,57.09) di `Level5_PreHeater_..._Overview (1)`, B=(20.57,9.90,45.26) di `Level5_PreHeater_..._Overview`. Slurry pump casing `L4_SlurryPump_Blue_Volute_Casing` (62.78,1.35,55.70).

**BUILD (edit-time execute_code, BUKAN Blender)** — root `L3_SlurryTank_To_Preheater_Pipe` parent "Mesin Utama", 2 child: `Pipe_Glass` (outer X-ray) + `Pipe_SlurryFlow` (inner). Helper `seg(parent,mat,a,b,r)` = cylinder (up=dir, scale x/z=r*2, y=len/2, collider dihapus); `joint(parent,mat,p,r)` = sphere di waypoint; `lay(pts[])` = seg+joint sepanjang waypoint. rOut=0.42, rIn=0.28.
- Route: gasket(84.67,4.04,55.15) → (84.67,2.6,55.15) turun → (66,2.6,55.7) → pump suction (62.8,2.6,55.7) → riser UP atas pump (62.8,7.8,55.7) → tee (24,9.9,55.7) header → branch A ke flange A (21.2,9.9,57.09); branch B ke (24,9.9,45.26)→flange B (21.2,9.9,45.26). Jadi: tank → INTO pump (boost) → riser diatas pump → header → 2 preheater flange.
- Materials (URP/Lit, dibuat runtime): glass transparent _Surface=1 SrcBlend5/DstBlend10 ZWrite0 q3000 base(0.62,0.72,0.82,**0.26**) smooth0.9 (X-ray, slurry kelihatan); steel met0.7 (flange di gasket+2 outlet+pump tie-in); slurry brown base(0.46,0.28,0.14) EMISSION on (0.32,0.18,0.07) smooth0.55.
- Flow animasi: `ProcessPipeFlowAnimator` di-AddComponent ke `Pipe_SlurryFlow` (auto-pakai child renderer). SerializedObject set `_flowOnStart=true`, `_fluidColor=(0.55,0.34,0.16)`, `_waveSpeed=1.6`. → emisi gelombang menjalar = slurry mengalir tank→preheater (auto saat play).

**CATATAN execute_code**: panggilan PERTAMA gagal silent (success:false null) — transient. Re-run identik dgn try/catch → glass=17, flow=13, saved=True. (Selalu wrap try/catch utk surface error.)

**VERIFIED play**: animator IsFlowing=True; persist setelah stop (glass=17, flow=13, parent Mesin Utama). Screenshot render OK (X-ray glass + slurry coklat di dalam). Scene SAVED (EditorSceneManager.SaveScene di edit-mode build).

**Files**: Assets/Scenes/Level1.unity (root `L3_SlurryTank_To_Preheater_Pipe` ditambah + saved). TIDAK ada file .cs baru (pakai ProcessPipeFlowAnimator existing). Screenshot Assets/Screenshots/screenshot-20260601-090132.png (harmless).


---

### 2026-06-01 (Part 40) — L3 Slurry Tank→Pump→Preheater Pipe REDESIGN HD (Blender headless, gantikan pipa C# edit-time)

**User**: redesign pipa `L3_SlurryTank_To_Preheater_Pipe` (yang Part 39 dibuat kasar via edit-time C# cylinders) jadi high-fidelity industrial nikel-HPAL slurry pipeline, build di Blender headless, UV+texture realistik spt mesin sebelumnya, "prompt level 6" = kualitas pro "benar-benar seperti di industrinya". Respon Indonesia, kode MINIMAL.

**RISET**: Roadmap flow Crusher→Slurry Tank→Slurry Pump→Pre-heater→Acid→Autoclave. Web: rubber-lined steel slurry pipe (abrasion-resistant), flanged spool pieces, pump suction/discharge spool, rubber expansion joint di pump (isolasi getaran), pipe rack support, ~3 m/s, 80°C. Konvensi dikonfirmasi dari build_slurry_to_preheater_pipe.py + build_ccd_process_pipes_v2.py + build_ore_crusher_v4.py (finalize image-textured).

**Endpoint (renderer.bounds.center, verified)**: gasket `L3_SlurryTank_FrontInspection_DarkGasket` (84.67,4.04,55.15) = START; DUA `L5_CleanOutlet_Flange` A=(20.57,9.89,57.09) B=(20.57,9.90,45.26); slurry pump `L4_SlurryPump_Blue_Volute_Casing` (62.78,1.35,55.70).

**BUILD (Blender headless)**: `Assets/Art/SlurryToPreheaterPipe/build_slurry_preheater_pipe_v2.py` (222 baris) → `SlurryToPreheater_Pipe_IndustrialUV_v2.fbx`. Konvensi Part 34: u2b(ux,uy,uz)=Vector((-ux,-uz,uy)), dir vector pakai u2b linear, finalize(bevel harden_normals angle35 + cube_project UV + auto_smooth), mat() image-textured PNG dari FlashCCDIndustrialUVRedesign/Textures + Bump node, export FBX_SCALE_ALL+bake_space_transform axis -Z/Y object_types MESH → instance IDENTITY di Unity landing tepat.
- Route: gasket → drop → pump suction (62.8,2.8,55.7) → discharge riser UP (62.8,8.7,55.7) → rack → **omega expansion loop** (52→52,11.7→45.5,11.7→45.5,9.9) → TEE (24,9.9,55.7) → branch A ke flange A; branch B ke flange B. RO=0.50 RI=0.34 (OD 1.0m abrasion main), branch 0.8×.
- `lay()`: inner flow = 1 cyl kontinu per segmen polyline (di-join nanti); outer = flanged spool tiap 6m, tiap spool ke-3 transparent sight-glass (`*_GlassSpool_N`), sisanya rubber (`*_RubberSpool_N`); flange (steel disc + 6 bolt) per joint; orange pressure band; elbow sphere per vertex. Pump detail: 3 rubber expansion-joint ring + hazard band + `DischargeKnifeValve` (blue body + steel bonnet + orange handwheel). Pipe rack: column+concrete foot+yellow shoe di 7 posisi. Inner flow di-join → single mesh **`SlurryToPreheater_SlurryFlow`** utk animator.
- Ran: **OLIVIA_SLURRYPIPE_V2_OK** (DeprecationWarning use_nodes harmless).

**UNITY (execute_code)**: Instantiate FBX, DestroyImmediate old `L3_SlurryTank_To_Preheater_Pipe` (C# cylinders), set world identity → SetParent(Mesin Utama, true) → rename `L3_SlurryTank_To_Preheater_Pipe`. Glass spool (nama mengandung "Glass", 8) → URP/Lit transparent (_Surface=1 SrcBlend5/DstBlend10 ZWrite0 q3000 base(0.62,0.72,0.82,0.26) smooth0.9 X-ray). `SlurryFlow` mesh → URP/Lit emissive slurry (base(0.46,0.28,0.14) emission(0.34,0.19,0.08) smooth0.55) + `ProcessPipeFlowAnimator` (_flowOnStart=true, _fluidColor=(0.55,0.34,0.16,1), _waveSpeed=1.6 via SerializedObject). Rubber/steel/orange/concrete/hazard/blue keep imported UV-textured FBX mat.

**QUIRK**: execute_code 1× gagal SILENT (success:false null) transient — wrap try/catch + re-run identik (sukses attempt ke-2). Screenshot camera WAJIB pass view_position+view_target.

**VERIFIED**: total=241 renderers, glass=8, flowAssigned=1, animator=True, boundsCtr=(53.3,6.1,51.2) size=(64.2,12.6,13.0) bounds x21.2-85.4 y-0.2-12.4 z44.7-57.7 (cocok endpoint). Play mode: **flowing=True**. Stop play. 2 screenshot positioned (overview + pump tie-in detail). Scene SAVED. 0 compile error baru.

**STATUS: L3 SLURRY PIPE HD FULLY FUNCTIONAL & VERIFIED.** Pipa nyambung gasket slurry tank → masuk pump (boost) → riser diatas pump → expansion loop di rack → TEE → 2 preheater CleanOutlet flange. X-ray sight-glass spool lihat slurry coklat emissive mengalir (ProcessPipeFlowAnimator gelombang). 

**Files**: Assets/Art/SlurryToPreheaterPipe/build_slurry_preheater_pipe_v2.py (BARU 222 baris), SlurryToPreheater_Pipe_IndustrialUV_v2.fbx (BARU), Assets/Scenes/Level1.unity (replace pipe + materials + PPFA, saved). Screenshots Assets/Screenshots (harmless).


---

### 2026-06-01 (Part 41) — L3 Slurry Pipe DILURUSKAN (omega loop dibuang) + slurry LEWAT pump (pump jadi berguna)

**User (Indonesia)**: pipa `L3_SlurryTank_To_Preheater_Pipe` (HD v2 dari Part 40) masih "bengkong-bengkong" (omega expansion loop + zigzag tinggi). Minta: (1) pipa LURUS, tanpa tekukan; (2) nyambung ke slurry tank; (3) posisi pipa JANGAN di luar tapi DI DALAM, naik TEGAK LURUS ke atas, lalu LANGSUNG horizontal ke preheater; (4) "BUAT SLURRY PUMP INI HARUS BERGUNA ENTAH GIMANA CARANYA" — slurry harus fisik LEWAT slurry pump (pump fungsional).

**SOLUSI (edit `build_slurry_preheater_pipe_v2.py`, Blender headless rebuild)**: Ganti blok route omega-loop dengan rute LURUS yang mengalirkan slurry tank → pump suction → pump discharge → riser tegak → header lurus → 2 preheater flange. Pump kini di JALUR ALIRAN aktif (berguna).
```python
G    = (84.67, 4.04, 55.15)   # L3_SlurryTank_FrontInspection_DarkGasket (tie-in tank)
PIN  = (66.0, 2.4, 55.70)     # pump SUCTION flange (slurry masuk pump)
PB   = (62.8, 2.4, 55.70)     # pump DISCHARGE / riser base (slurry keluar pump -> naik)
HY   = 9.9                    # header / preheater inlet height
TEE  = (22.0, HY, 55.70)
RO, RI = 0.50, 0.34
main = [G, (84.67,2.4,55.70), PIN, PB, (62.8,HY,55.70), TEE]   # turun -> pump -> RISER TEGAK -> header
brA  = [TEE, (22.0,HY,57.09), (20.7,HY,57.09)]   # -> L5_CleanOutlet_Flange A (z57.09)
brB  = [TEE, (22.0,HY,45.26), (20.7,HY,45.26)]   # -> L5_CleanOutlet_Flange B (z45.26)
```
Supports pipe-rack diupdate ke posisi rute lurus (buang support omega-loop): (75,2.4,55.7),(58,HY,55.7),(46,HY,55.7),(34,HY,55.7),(25,HY,55.7),(22,HY,50.5),(22,HY,45.26). Pump tie-in detail (3 rubber expansion-joint ring y2.55/2.8/3.05, hazard band y4.4, `DischargeKnifeValve` y6.6) TETAP — sekarang duduk di riser discharge tegak (x62.8,z55.70). Konvensi Part 22/40 UTUH: u2b(ux,uy,uz)=Vector((-ux,-uz,uy)), FBX_SCALE_ALL+bake_space_transform axis -Z/Y → instance Unity IDENTITY landing tepat. Output FBX `SlurryToPreheater_Pipe_IndustrialUV_v2.fbx`.

**UNITY (execute_code)**: DestroyImmediate pipa lama (omega), InstantiatePrefab v2 lurus → identity → SetParent(Mesin Utama,true) → rename. **QUIRK**: 1st execute_code gagal ambiguous `Object` cast (line 38) → fix pakai `(UnityEngine.Object)c`; 2nd sukses. Glass spool (nama "Glass", 7) → URP/Lit transparent X-ray; `SlurryToPreheater_SlurryFlow` → emissive slurry + `ProcessPipeFlowAnimator` (_flowOnStart=true, _fluidColor=(0.55,0.34,0.16,1), _waveSpeed=1.6). Scene saved.

**VERIFIED (edit-mode setelah keluar play)**: total=218 glass=7 flow=1 parent="Mesin Utama" animFound=True flowOnStart=True. boundsMin=(20.6,-0.2,44.7) boundsMax=(85.4,10.6,57.7) — x preheater→tank, **y max 10.6 (TURUN dari 12.4 omega; loop hilang = pipa lurus)**, z 44.7→57.7. **sceneDirty=False (TERSIMPAN)**. Play mode: flowing=True (slurry mengalir tank→pump→riser→header→preheater). 2 screenshot positioned diambil (overview + samping); render agak gelap karena lighting scene, tapi rute lurus terkonfirmasi numerik via bounds.

**LESSON ulang**: `execute_code` ambiguous `Object` → pakai `UnityEngine.Object`. `Bounds` di-seed dari `rs[0].bounds` (JANGAN `new Bounds()` dari origin — bikin min palsu ~0). `execute_code` kadang gagal silent/transient → re-run.

**STATUS: L3 SLURRY PIPE LURUS + PUMP DI JALUR ALIRAN — DONE & VERIFIED.** Tank → drop → pump suction → discharge → riser TEGAK → header LURUS → TEE → 2 CleanOutlet preheater. Pump kini berguna (slurry fisik lewat). 

**Files**: Assets/Art/SlurryToPreheaterPipe/build_slurry_preheater_pipe_v2.py (route+support diedit), SlurryToPreheater_Pipe_IndustrialUV_v2.fbx (rebuild), Assets/Scenes/Level1.unity (replace pipe lurus, saved). Screenshots Assets/Screenshots (harmless).



---

### 2026-06-01 (Part 42) — L3 Slurry Pipe: intake TERCELUP di dalam slurry @ StirrerColumn(1) → naik lewat ATAS bibir tangki → preheater

**User (Indonesia)**: pipa `L3_SlurryTank_To_Preheater_Pipe` intake-nya mau MASUK ke DALAM slurry di posisi GameObject `L3_SlurryTank_StirrerColumn_Static (1)`, lalu naik TEGAK lewat ATAS bibir tangki, baru menuju preheater. ("pipanya didalam slurry gitu lo trus lewat atas dan baru menuju ke preheater").

**Koordinat verified (execute_code, pakai renderer.bounds.center karena geometri FBX kadang baked di origin)**:
- `L3_SlurryTank_StirrerColumn_Static (1)` (intake yg user pilih): bc=(86.68,6.62,55.14), membentang Y 1.95→11.29. (transform.position baked = (-4.77,-0.74,0.07) → JANGAN pakai, pakai bounds center.)
- RestingSlurryPool (permukaan slurry): top Y≈1.81, dasar 0.29 → "dalam slurry" = y~0.8.
- OpenShell tangki (bibir/rim): top Y≈7.86, XZ center ~(91.4,55.1), dinding x84.8–98.
- Pump `L4_SlurryPump_Blue_Volute_Casing` (62.78,1.35,55.70); preheater `L5_CleanOutlet_Flange` A=(20.57,9.89,57.09) B=(20.57,9.90,45.26).

**EDIT `build_slurry_preheater_pipe_v2.py` (3 strReplace, Blender headless rebuild)** — ganti blok route + supports + pump tie-in detail:
```python
SX, SZ = 86.68, 55.14               # StirrerColumn(1) intake XZ
PSX, PDX = 64.6, 61.0               # pump suction X (over-top turun) / discharge X (riser naik)
HY   = 9.9
TEE  = (22.0, HY, 55.70)
RO, RI = 0.50, 0.34
main = [(SX, 0.8, SZ),              # intake TERCELUP di dalam slurry
        (SX, 10.6, SZ),             # naik TEGAK lewat bibir tangki (rim 7.86)
        (PSX, 10.6, 55.70),         # over-the-top menuju pump
        (PSX, 2.4, 55.70),          # turun ke pump SUCTION (pump berguna)
        (PDX, 2.4, 55.70),          # lewat body pump -> discharge
        (PDX, HY, 55.70),           # discharge riser TEGAK naik
        TEE]                        # header LURUS ke preheater
brA  = [TEE, (22.0,HY,57.09), (20.7,HY,57.09)]   # CleanOutlet A
brB  = [TEE, (22.0,HY,45.26), (20.7,HY,45.26)]   # CleanOutlet B
```
Pump tie-in detail (3 rubber exp-joint y2.55/2.8/3.05, hazard y4.4, `DischargeKnifeValve` y6.6) dipindah ke discharge riser x=PDX(61.0),z55.70. Supports: over-top (78,10.6,55.7),(70,10.6,55.7) + header (54/44/34/25,HY,55.7) + branch (22,HY,45.26). Konvensi Part 22/40 UTUH: u2b(ux,uy,uz)=Vector((-ux,-uz,uy)), FBX_SCALE_ALL+bake axis -Z/Y → instance IDENTITY. Output `SlurryToPreheater_Pipe_IndustrialUV_v2.fbx`. Ran OLIVIA_SLURRYPIPE_V2_OK.

**UNITY (execute_code, try/catch)**: DestroyImmediate pipa lama → InstantiatePrefab v2 → identity → SetParent(Mesin Utama,true) → rename. Glass spool (nama "Glass",8) → URP/Lit transparent X-ray; `SlurryToPreheater_SlurryFlow` → emissive slurry + `ProcessPipeFlowAnimator` (_flowOnStart=true,_fluidColor=(0.55,0.34,0.16,1),_waveSpeed=1.6). Scene SAVED.

**VERIFIED**: total=234 glass=8 flowAssigned=True anim=True. boundsMin=(20.6,-0.2,44.7) boundsMax=(87.4,11.3,57.7) → **x max 87.4 = intake di x86.68 ✓, y max 11.3 = riser lewat bibir tangki rim 7.86 ✓**, intake submerged y~0.8 di dalam slurry pool (0.29–1.81) ✓. Play mode: flowing=True enabled=True. Keluar play mode. Screenshot scene-view gelap (lighting quirk) tapi geometri terkonfirmasi numerik via bounds.

**STATUS: DONE & VERIFIED.** Intake tercelup di dalam slurry @ StirrerColumn(1) → naik tegak lewat bibir tangki → over-top → pump (berguna) → riser → header lurus → 2 preheater flange. Slurry mengalir (ProcessPipeFlowAnimator).

**LESSON ulang**: transform untuk objek FBX yg baked di (0,0,0) → pakai renderer.bounds.center, JANGAN transform.position (scene_view_frame butuh NAMA GameObject, bukan koordinat array). 

**Files**: Assets/Art/SlurryToPreheaterPipe/build_slurry_preheater_pipe_v2.py (route+supports+pump-detail diedit), SlurryToPreheater_Pipe_IndustrialUV_v2.fbx (rebuild), Assets/Scenes/Level1.unity (replace pipe, saved).


---

### 2026-06-01 (Part 43) — SLURRY TANK Level 3 jadi BERFUNGSI: agitated conditioning vessel HPAL (turbine + drive + solids tersuspensi + panel instrumen live) — PIPA LURUS TAK DISENTUH

**User (Indonesia)**: slurry tank jangan cuma pajangan — buat BERGUNA & autentik industri HPAL. "Pipa yang udah lurus itu jangan ubah tapi boleh menambah" → ADITIF SAJA. "GAS".

**Riset/keputusan**: Fungsi inti slurry conditioning/feed tank HPAL = jaga padatan laterit nikel TERSUSPENSI (~38% solids) via agitator + umpan kontinu ke pre-heater/autoclave. Diperlihatkan secara visual + informatif TANPA menyentuh pipa lurus (`L3_SlurryTank_To_Preheater_Pipe`).

**Komponen BARU** `Assets/Scripts/Simulation/SlurryConditioningTankRunner.cs` (MonoBehaviour, play-mode only): `OnEnable`→`Build()` jika `Application.isPlaying`; `OnDisable`→`Destroy(_root)`. Idempotent (guard child `L3_SlurryConditioning_Runtime` sudah ada). Di-`AddComponent` ke rig `Level3_SlurryWaterTanks_Industrial_UV_Auto` (scene SAVED). Aditif murni — TIDAK konflik dgn agitator runtime `Level3OreSlurryController` (impeller controller di y3.6 saat sequence; turbine baru di bottom+0.7, jalan kontinu).

**Geometri (renderer bounds)**: center XZ (91.41,55.14); slurry surface y≈1.78; bottom y≈0.45; inner radius ≈5.7; OpenShell top y≈7.86. Konstanta `_c,_surfaceY,_bottomY,_radius`. Mat helper `Mat(Color,metal,smooth)` pakai `Shader.Find("Universal Render Pipeline/Lit")` (`_BaseColor/_Metallic/_Smoothness`). `_ore`=(0.40,0.27,0.16), `_steel`=(0.55,0.57,0.60) metallic.

**Runtime root `L3_SlurryConditioning_Runtime` berisi**: (1) `Agitator_Turbine` hub+5 pitched blade (Space.World Y-rot `_rpm=46`) di tank center bottom; (2) shaft cylinder turbine→gearbox + `_coupling` disc 4 bolt berputar (bukti drive); (3) `_solidCount=46` ore chunk (cube/sphere brown) orbit poros vertikal (inner faster) + turbulensi radial + bob vertikal + self-spin = "solids stay suspended"; (4) `Slurry_Instrument_Panel` backplate gelap + `TextMesh` (`LegacyRuntime.ttf`/`Arial.ttf`, hijau, billboard ke Camera.main) readout LIVE: Status SUSPENDED/HOMOGEN, Density ~38% solids (osilasi), Level ~86%, Agitator 46 RPM, Suhu 80C, Feed→Pre-Heater ~452 m3/h.

**VERIFIED (play-mode reflection)**: rends=60, turbine=True, panel=True, turbineCtr=(91.4,1.2,55.1), rotY berubah (5.6 setelah frame), panel line1="Density : 38.0 % solids". Keluar play mode → edit mode: rig=True, comp=True (PERSIST), runtimeRootInEdit=False (rebuild tiap play, benar), **straightPipeIntact=True (pipa lurus TAK DISENTUH)**. Screenshot top-down & front gelap (lighting quirk) tapi mekanik terverifikasi numerik. 0 compile error (1 warning benign).

**LESSON**: TextMesh runtime butuh font built-in (`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` fallback `"Arial.ttf"`) + assign `font.material` ke renderer (hindari teks invisible/magenta). Komponen play-mode-only: edit mode tank tampak unchanged — verifikasi via PLAY. Aditif: tank fungsional tanpa ubah pipa/agitator controller existing.

**Files**: Assets/Scripts/Simulation/SlurryConditioningTankRunner.cs (BARU), Assets/Scenes/Level1.unity (komponen ditambah ke rig slurry tank, saved). Pipa & build .py pipa TIDAK diubah.


---

### 2026-06-01 (Part 44) — Valve Level 5/7 → mekanisme gestural BARU (seperti FV1) + fix spawn Level 6 + debug ore belt Level 3

**User**: (1) ganti mekanisme buka valve di Pre-Heater (Level 5) & Autoclave (Level 7) dengan yang BARU gestural ikut gerak tangan seperti `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel` (Level 8). (2) Spawn Level 6 salah. (3) (nanti) tambah debug `Level3_Runtime_Ore_Belt_Flow`.

**Mekanisme Level 8 (acuan)**: `UpdateHandwheel`/`GetGesturalDelta` track delta yaw dari `interactor.up` (fallback `.right`) diproyeksikan ke bidang disc × `_gesturalGain` (5); XRSimpleInteractable (objek TIDAK ketarik); hover & grab sama-sama memutar; clamp raw dYaw 35°; TANPA auto-open. Bug lama: pakai `.forward` (salah untuk gesture putar roda) + XRGrabInteractable (objek ketarik) + auto-rotate fallback.

**Level 5 (Level5SteamValveController.cs)**: `TrackVRRotation` diganti pakai `.up`/`.right` twist + clamp raw 35 + gain (`_skalaResponsRotasiVR*3`). `WireXRGrabListeners` diganti: hapus XRGrabInteractable+Rigidbody, pasang XRSimpleInteractable + register collider eksplisit + listener select+hover (set `_sedangDiGrab`/`_valveHover` + `_interactorAttach`). Update: kondisi `(_sedangDiGrab || _valveHover)`, auto-open fallback dimatikan (`if(false)`). Tambah field `_valveHover`, `_valveSimple`. Visual wheel tetap via `_rotasiAkumulasi`→UpdateVisuals (tak diubah).

**Level 7 (Level7AutoclaveController.cs)**: `TrackInletValveRotation` diganti pakai `.up`/`.right` twist + clamp 35 + gain 5, hapus auto-rotate. `EnsureInletValveInteractable` diganti: XRSimpleInteractable + register collider + select(OnInletGrabbed/Released)+hover listener. Update: `(_inletValveGrabbed || _inletValveHover)`. Tambah field `_inletValveHover`. Visual tetap via `RotateHandwheelParts`/HandwheelVirtualPivot.

**Fix Spawn Level 6**: `SpawnPoint_Lvl6` LAMA = (18,2,56) rotY=320 — **y=2 ~3m DI BAWAH platform pre-heater** (lantai platform y≈5, valve `RealSteamValve_Pivot_Lvl5` @ (17.98,5.37,40.17)) → pemain melayang/menunduk lihat peta. FIX: set `SpawnPoint_Lvl6` = posisi `SpawnPoint_Lvl5_PreHeater` (31.97,5.00,46.00) rotY=0 (spawn Level 5 yang terbukti benar, task pre-heater sama). Scene SAVED. (Level 5 teleport pakai XROrigin MoveCameraToWorldLocation+MatchOriginUpCameraForward ke `_teleportTargetField`=SpawnPoint_Lvl5_PreHeater.)

**Debug Level 3**: tambah `[ContextMenu]` `DebugOreBeltFlowOn/Off` di Level3OreSlurryController → `SetConveyorOreFxAktif(true/false)` (uji `Level3_Runtime_Ore_Belt_Flow` via klik-kanan komponen, play mode).

**Verifikasi**: compile 0 error CS. Belum play-test VR (perlu headset/simulator untuk rasa gestural). 

**Files**: Level5SteamValveController.cs, Level7AutoclaveController.cs, Level3OreSlurryController.cs, Assets/Scenes/Level1.unity (SpawnPoint_Lvl6 dipindah, saved).


---

### 2026-06-01 (Part 45) — Putaran valve L5 & L7 dibuat PERSIS Level 8 via komponen GesturalHandwheel

**User**: "tolong putarannya ganti persis seperti di level 8 flash vessel putarannya" — bukan cuma input, GERAK PUTAR roda harus identik Level 8.

**Akar beda**: Level 8 `ApplyHandwheelRotation` memutar SEMUA part (hub+ring+spoke) mengelilingi pivot WORLD pada axis WORLD (disc normal) via `Quaternion.AngleAxis(deg, axisWorld)`. Level 5 lama cuma `_valveWheel.localRotation` di axis LOCAL. Level 7 `RotateHandwheelParts` aktif malah cuma memutar pivot EMPTY (`_inletValvePivot.localRotation`), part tak ikut.

**Solusi**: komponen baru `Assets/Scripts/Simulation/GesturalHandwheel.cs` = salinan mekanisme Level 8 (gestural twist `interactor.up` fallback `.right` proyeksi ke bidang disc × gain 5, clamp dYaw 35°, XRSimpleInteractable hover+grab, register collider eksplisit, ApplyHandwheelRotation part-about-world-pivot) + auto-infer axis = arah TERTIPIS bounds gabungan (normal disc) & pivot = pusat bounds. Expose `OpenPercent01`. Keyboard R fallback.
- **Level 7**: `EnsureInletValvePivot` rewrite → attach GesturalHandwheel ke hub `L7_LiquidUnderflow_Handwheel_Hub` + parts (hub+ring+4 spoke), buang HandwheelVirtualPivot; guard `_inletGH`. Update (Phase.BukaInletValve) baca `_inletGH.OpenPercent01` → `_inletValveOpenPercent` (RotateHandwheelParts/TrackInletValveRotation/UpdateInletValveVisuals/EnsureInletValveInteractable jadi dead). Call site coroutine `EnsureInletValveInteractable()` → `EnsureInletValvePivot()`.
- **Level 5**: `WireXRGrabListeners` → attach GesturalHandwheel ke `_valveWheel` (fullOpenDegrees=_totalDerajatFullOpen). Update baca `_valveGH.OpenPercent01 * _totalDerajatFullOpen` → `_rotasiAkumulasi`. Baris `_valveWheel.localRotation=...` di UpdateVisuals dimatikan (GH yang putar). Field `_valveGH` ditambah.

**Verifikasi**: compile 0 error CS. Belum play-test VR (perlu simulator). Tuning: gain via `GesturalHandwheel.gesturalGain` (default 5, sama Level 8), `fullOpenDegrees` per controller.

**Files**: Assets/Scripts/Simulation/GesturalHandwheel.cs (BARU), Level5SteamValveController.cs, Level7AutoclaveController.cs.



---

### 2026-06-01 (Part 46) — REDESAIN MODEL stir/handwheel di Pre-Heater (Level 5) jadi clean industrial

**User**: "engga maksudku itu Tolong desain ulang khussus stir di preheater ini!" — BUKAN rotasi (sudah Part 44/45), tapi MODEL 3D handwheel di pre-heater yang kasar harus didesain ulang. Respon Indonesia, kode MINIMAL.

**Temuan**: `_valveWheel` Level 5 = `RealSteamValve_Pivot_Lvl5` = pivot EMPTY (tanpa mesh) → wiring GH Part 45 di Level 5 sebenarnya memutar APA-APA yang tak kelihatan. Handwheel kasar yang TERLIHAT = part FBX terpisah di bawah `Level5_PreHeater_Blender_Industrial_UV_Overview`: `L5_Decorative_Steam_Valve_Handwheel_OuterRing` (sz 1.56×1.56×0.09, disc normal Z), `_Hub`, `_Spoke_00..03` (spoke 01/03 nyembul salah arah di Z = penyebab terlihat berantakan), + `L5_Steam_Valve_Body_Red` + `L5_Steam_Valve_Riser`. Semua material `M_Level5_PreHeater_UVAtlas`. Pusat wheel ≈ (17.99, 5.38, 40.20), disc normal world +Z, radius luar ≈ 0.78.

**SOLUSI — bangun handwheel BARU procedural di Unity edit-mode (`execute_code`)** (pilih Unity-procedural ketimbang Blender utk hindari pitfall FBX bake/axis — memory warn transform_apply pada part ber-rotasi):
- **Mesh torus rim** kustom (bidang XY, normal +Z, R=0.72, tube 0.07, 48×14 segmen) disimpan sbg asset `Assets/Art/Level5Handwheel/L5_Handwheel_Rim.asset`.
- **Cylinder primitive** (collider dibuang): Hub (sumbu Z), 5 spoke radial, Grip knob (sumbu Z di atas), StemNut tengah.
- **3 material URP/Lit**: `M_L5HW_Yellow` (rim, hazard), `M_L5HW_Steel` (hub/spoke/stem), `M_L5HW_Grip` (grip hitam).
- Root `L5_SteamValve_Handwheel_Redesign` parent `Mesin Utama`, world pos (17.99, 5.38, 40.20), rotasi identity, lossyScale dipaksa 1.0 (localScale=1/parent.lossyScale). + SphereCollider r=0.72 utk XR grab.
- **Sembunyikan 6 part kasar lama** (`SetActive(false)`) di semua instance (rig pre-heater terduplikasi → hid=14).
- **Re-point `_valveWheel`** Level 5 → root baru via SerializedObject. Save scene + AssetDatabase.SaveAssets.
- Hasil: `OK hid=14 parent=Mesin Utama rootWorld=(17.99,5.38,40.20) lossy=(1,1,1) | _valveWheel -> L5_SteamValve_Handwheel_Redesign`.
- **EFEK SAMPING POSITIF**: karena `_valveWheel` sekarang = root yang PUNYA mesh children (rim+spoke+hub+grip), `GesturalHandwheel.Setup(root,null)` bisa hitung bounds → infer axis = tertipis = Z, pivot = bounds center → memutar seluruh assembly rigid di tempat. Ini juga MEMPERBAIKI rotasi Level 5 Part 45 yang sebelumnya tak kelihatan (pivot empty).

**Verifikasi visual**: screenshot game-view gelap (lighting quirk), TAPI scene_view (manage_scene scene_view_frame target=L5_SteamValve_Handwheel_Redesign → manage_camera screenshot capture_source=scene_view) JELAS: rim torus kuning bersih, 5 jari-jari steel, hub, grip hitam atas, terpasang tepat di atas body valve merah + riser, menghadap +Z ke pemain. Part kasar lama hilang.

**LESSON**: (1) screenshot scene_view TIDAK terima view_position/view_target — frame dulu via manage_scene scene_view_frame (butuh NAMA GameObject), lalu manage_camera screenshot capture_source='scene_view'. (2) Untuk handwheel yang GH putar: pastikan target `_valveWheel` punya mesh children (bukan pivot empty) supaya bounds/axis ter-infer. (3) Body valve merah + riser SENGAJA dipertahankan (cuma wheel yang kasar).

**Files**: Assets/Scenes/Level1.unity (root handwheel baru + 6 part lama disembunyikan + serialized `_valveWheel` re-point, saved), Assets/Art/Level5Handwheel/L5_Handwheel_Rim.asset (BARU mesh torus). TIDAK ada .cs diubah (GesturalHandwheel.cs dsb = Part 45).



---

### 2026-06-01 (Part 47) — SEMUA handwheel operator dibuat gestural (auto-setup) seperti FV1_To_FV2

**User**: minta handwheel diputar dengan arah ditentukan tangan pemain PERSIS seperti `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel` (Level 8). Frustrasi karena handwheel yang dilihat "belum berubah" (tiap kali objek beda: L5_SteamValve_Handwheel_Redesign, lalu L5_Condensate_Drain_Handwheel, dll).

**Akar masalah**: `GesturalHandwheel` (komponen mekanisme gestural Part 45, identik Level 8) cuma di-`Setup()` oleh controller (L5 `_valveGH`, L7 `_inletGH`) SAAT di dalam alur level. Di editor/luar level komponen diam → handwheel terlihat "belum berubah". Selain itu tiap handwheel beda objek; user menunjuk yang belum diberi komponen.

**FIX `GesturalHandwheel.cs`** (apply_text_edits): tambah `[SerializeField] bool _autoSetupOnStart` + `Start()` (panggil `AutoSetup()` kalau belum `_ready`) + `AutoSetup()`:
- root ber-anak mesh (mis. `L5_SteamValve_Handwheel_Redesign`) → `Setup(transform,null)` = putar ROOT, anak ikut (hindari double-rotate).
- objek `*_Hub` (part terpisah sibling: Hub/OuterRing/Spoke_NN) → kumpulkan sibling se-prefix → `Setup(hub, parts)`.
- handwheel SATU-MESH standalone (bukan `_Hub`) → `Setup(transform,null)` = putar diri sendiri.
- Kompatibel dgn controller L5/L7 (mereka `GetComponent` dulu lalu re-`Setup`, [DisallowMultipleComponent] aman).
- LESSON edit: salah hitung baris bikin `debugKey` dobel (CS0102) → hapus baris duplikat. Validator unbalanced-braces saat replace parsial method → ganti SELURUH method sekaligus (brace self-contained).

**Pemasangan (edit-mode, serialized, `_autoSetupOnStart=true`, scene saved)**:
- Grup `*_Handwheel_Hub` (7): L5_Condensate_Drain_Handwheel_Hub (z49 & z61), L5_Drain_Valve_Handwheel_Hub, L7_LiquidUnderflow_Handwheel_Hub, 3× L5_Decorative_Steam_Valve_Handwheel_Hub (inactive, harmless).
- Standalone satu-mesh (18): L3_SlurryTank_OutletIsolationValve_YellowHandwheel, L3_ProcessWaterPipe_ControlValve_YellowHandwheel, 8× L4_Drain_Valve_Handwheel, Manual_Runoff_Valve_Handwheel, Manual_Bypass_Handwheel, AcidTank_A/B_Vent/OutletIsolationHandwheel, Underflow_KnifeValve_Handwheel, PLS_LetdownValve_Handwheel.
- Plus `L5_SteamValve_Handwheel_Redesign` (root, dipasang lebih awal).
- **DILEWATI** (dikelola sistem rotasi Level 8 sendiri, hindari bentrok): FV*_Hub, AutoclaveToFlash_LetdownValve_Handwheel_Hub, L7_AcidInject_Handwheel_Hub.

**VERIFIKASI play-mode (reflection, simulasi twist tangan)**:
- L5_SteamValve_Handwheel_Redesign: axis Z, +15°→deg naik, −15°→deg turun (arah ikut tangan), guard anti-loncat 35° (sama Level 8).
- L5_Condensate_Drain_Handwheel: axis Z, hub diam (moved 0), spoke orbit (moved 0.17) = putar di poros, bukan tumbling.
- L3 yellow (satu-mesh): axis Y, twist→putar 60° di tempat (self moved 0).
- Compile 0 error. Console cuma noise benign (ArgumentNullException tanpa stack + generators.ai.unity network).

**Cara pakai**: `GesturalHandwheel` sekarang reusable — attach ke handwheel mana pun + set `_autoSetupOnStart=true` → otomatis berputar ikut arah tangan (twist controller.up proyeksi ke bidang disc × gain 5). Untuk handwheel yang dikelola controller, biarkan controller yang Setup.

**Files**: Assets/Scripts/Simulation/GesturalHandwheel.cs (auto-setup), Assets/Scenes/Level1.unity (26 handwheel + GesturalHandwheel, saved).



---

### 2026-06-01 (Part 48) — TANGGA INDUSTRI Ore_Tangga (2): pijakan akses player ke walkway (HD PBR, procedural Unity)

**User**: desain tangga industri di GameObject `Ore_Tangga (2)` sebagai pijakan player, HARUS realistis HD 4k texture, autentik industri nikel.

**Temuan**: `Ore_Tangga (2)` pos (83.29,7.66,39.93) rotY0, axis X. Bounds (106.02,1.00,2.29), maxY=8.16 (deck top) — bukan tangga, tapi walkway datar panjang 106m. Player butuh tangga akses dari tanah (y≈0.1) naik ke deck 8.16.

**Keputusan**: dibangun PROCEDURAL di Unity edit-mode (bukan Blender) — realisme via HD PBR material di primitive, placement presisi, collider trivial. Texture HD dari `Assets/Art/FlashCCDIndustrialUVRedesign/Textures/` (UV_BrushedSteel_Grey + UV_SteelDetail_Normal normalmap + UV_SafetyYellow_Rails + UV_Hazard_BlackYellow).

**BUILD** (execute_code) → root `Ore_Tangga2_IndustrialStair` parent "=== ENVIRONMENT ===", 213 transform. Layout inline X (z0=39.93, w=1.6): Flight1 x14→19.75 y0.1→4.13; mid landing x19.75→22.25 ytop4.13; Flight2 x22.25→28 y4.13→8.16; top landing x28→30.7 ytop8.16 (sambung walkway low-X ~30.28). 42 tread grating, riser, nosing kuning, 2 sloped channel stringer/flight (Quaternion.Euler(0,0,atan2(rise,run)*Rad2Deg)), railing 2 sisi (top@+1.0, mid@+0.5, toe, post tiap ~1.4m), 4 support column landing + base plate ke tanah.
- **Material URP/Lit**: mGr grating (BaseMap BrushedSteel tiled3x3 + normalmap + _NORMALMAP keyword + _BumpScale, metallic0.72), mSt steel (tiled1.5x metallic0.78), mYe SafetyYellow (rails/nosing), mHz hazard (landing edge).
- **Collider strategy**: visual parts BoxCollider di-Destroy; 4 collider aktif = 2 invisible smooth ramp BoxCollider/flight (MeshRenderer.enabled=false, offset along slope normal nv=(-sin a,cos a,0) by 0.15) + 2 landing deck collider → permukaan jalan player mulus.
- box() helper: box(name,center,size,rotation,material,bool collider) — collider true: keep BoxCollider+disable renderer; false: destroy collider.
- Idempotent: DestroyImmediate `Ore_Tangga2_IndustrialStair` lama dulu. Tambah BoxCollider ke `Ore_Tangga (2)` walkway (walkwayCol=True) supaya tangga mengarah ke deck walkable.

**VERIFIED**: stair=True objs=213 treads=42 colliders=4 walkwayCol=True. Screenshot scene_view (frame via manage_scene scene_view_frame + manage_camera capture_source=scene_view): tread/nosing terlihat, railing 2 sisi, material HD ter-render (brushed steel + kuning + hazard), tersambung walkway. Scene SAVED (dirty=False).

**CATATAN**: ground gnd=0.1 diasumsikan (terrain dekat x14,z40 tak dikonfirmasi presisi — kalau terrain beda, column mungkin float/clip; sesuaikan gnd bila perlu). Tangga inline sepanjang walkway axis X.

**Files**: Assets/Scenes/Level1.unity (root `Ore_Tangga2_IndustrialStair` + walkway collider, saved). Tidak ada .cs/FBX baru (procedural runtime build, semua objek scene permanen).



---

### 2026-06-01 (Part 49) — TANGGA ZIGZAG SAFETY di KEDUA ujung Catwalk Ore_Tangga2 (switchback deck→tanah, procedural Unity)

**User**: "diakhir atau setiap penghujung kasih tangga zigzag dari atas kebawah ya, yang safety! gas remodel" — tambah tangga zigzag/switchback di SETIAP ujung catwalk datar `Ore_Tangga2_IndustrialCatwalk` (Part 48), turun dari deck (top y=8.16) ke tanah (y≈0.1), HARUS "safety" (railing + hazard).

**Catwalk acuan (Part 48 geom)**: span X x0=30.3 x1=136.3, center z0=39.93, lebar w=2.0, deck top topY=8.16. Catwalk tetap DATAR (cuma tangga yang turun).

**BUILD (execute_code, procedural Unity edit-mode, BUKAN Blender)** → root `Ore_Tangga2_ZigzagStairs` parent "=== ENVIRONMENT ===", **218 objek**, idempotent (DestroyImmediate dulu). Param: ground=0.2, w=1.5 (lebar tangga), railH=1.05, R=3.6 (run per flight di X), nf=3 flights, rise=(top-ground)/3≈2.65, slope ≈36°. yRange 0.08→9.29, xRange 25.1→141.5, tex=True.
- Helper `box(name,center,scale,rot,mat,keepCollider)` (Cube, parented, sharedMaterial, collider dibuang kalau false).
- `railLine(a,b)`: top rail @+railH, mid rail @+railH*0.5, post vertikal tiap ~1.0m (balusters, jalan utk flight miring) via Quaternion.LookRotation.
- `flightX(zC,yTop,yBot,xS,xE,nm)`: grating slab miring `Euler(0,0,atan2(dy,dx))` collider KEEP (ramp walkable), hazard kick-plate 2 sisi Z, railLine 2 sisi.
- `landing(c,sx,sz,...)`: grating box datar + hazard strip + railLine sisi terpilih (sisi terbuka = tempat flight nyambung).
- `tower(xEnd,dir)`: switchback 2 lane Z0=z0-w/2, Z1=z0+w/2, far xf=xEnd+dir*R. F0(Z0,top→top-rise,xEnd→xf), F1(Z1,top-rise→top-2rise,xf→xEnd), F2(Z0,top-2rise→ground,xEnd→xf) + 3 turn-landing + kolom oranye di sudut. Dipanggil tower(136.3,+1) ujung tinggi (+X) & tower(30.3,-1) ujung rendah (-X).
- Material URP/Lit: mGr grating (UV_BrushedSteel_Grey tiled + UV_SteelDetail_Normal + _NORMALMAP), mHz hazard (UV_Hazard_BlackYellow), mYe SafetyYellow (rail/nosing), mOr orange (kolom). Tekstur dari Assets/Art/FlashCCDIndustrialUVRedesign/Textures/.

**VERIFIKASI visual**: screenshot positioned (view_position+view_target wajib) ujung tinggi (150,7,32→139,4,41) JELAS: grating tread + nosing kuning, rail 2 sisi (top+mid+toe) + baluster, hazard landing, kolom oranye nyambung deck. Switchback bersih turun deck→tanah. (Screenshot wide-angle awal terlihat ramai karena 2 tower+catwalk+rail satu frame — close-up confirm kualitas OK.) Scene SAVED.

**CATATAN**: ground=0.2 diasumsikan (sama pola Part 48; terrain di x≈30.3 & x≈136.3 z≈39.93 belum dikonfirmasi presisi — kalau terrain beda, kolom dasar bisa float/clip, sesuaikan ground). Flight slab collider KEEP = permukaan jalan player; visual box lain collider dibuang.

**Files**: Assets/Scenes/Level1.unity (root `Ore_Tangga2_ZigzagStairs`, saved). Tidak ada .cs/FBX baru (procedural runtime edit-mode, objek scene permanen). Screenshots Assets/Screenshots (harmless).



---

### 2026-06-01 (Part 50) — Fix ProcessPipeFlowAnimator null + gating SlurryToPreheater_SlurryFlow ke Level 4 (slurry pump)

**(1) FIX ERROR console**: `ProcessPipeFlowAnimator.cs:107 ArgumentNullException` di GetPropertyBlock(_mpb). Sebab: `_mpb` (MaterialPropertyBlock, non-serialized) ke-reset null tiap domain reload, tapi `EnsureInit()` early-return krn `_initialized` non-serialized... sebenarnya keduanya reset, TAPI race di [ExecuteAlways] bikin _initialized true sementara _mpb null. FIX minimal: pindah `if(_mpb==null)_mpb=new MaterialPropertyBlock();` ke SEBELUM `if(_initialized) return;` di EnsureInit. 0 error setelah recompile. (Error kedua generators.ai.unity = jaringan Unity AI, benign.)

**(2) USER**: `SlurryToPreheater_SlurryFlow` (inner flow mesh pipa L3 slurry tank→preheater, Part 40-42, parent `L3_SlurryTank_To_Preheater_Pipe`) tadinya `_flowOnStart=true` (selalu nyala/terlihat). Mau: HIDDEN dulu, MUNCUL+animasi mengalir tank→preheater HANYA saat Level pump slurry (Level 4) jalan.

**Temuan**: Level4SlurryPumpController di GO `Level4Controller`. Sudah punya `SetLevel4PipeFlow(bool active)` (pakai ProcessPipeNetwork route ids, pipa lain). Dipanggil: `OnLevelStarted`→SetLevel4PipeFlow(false) (reset), fase `MenungguLaporanFlow`+`ObservasiPump`→(true) saat pump aktif (flow rate 450 tercapai). Direction animasi udah toward preheater (`off=-Repeat(_scroll,1)`, komen "negatif=maju ke preheater").

**Solusi (minimal, nebeng SetLevel4PipeFlow)**:
- Set `_flowOnStart=false` di animator SlurryToPreheater_SlurryFlow (SerializedObject) + disable renderer sekarang + save scene → HIDDEN di start/edit/play sampai dipicu.
- Level4SlurryPumpController.cs: tambah field `private ProcessPipeFlowAnimator _slurryToPreheaterFlow;` + di AWAL `SetLevel4PipeFlow(active)` lazy-find `GameObject.Find("SlurryToPreheater_SlurryFlow").GetComponent<ProcessPipeFlowAnimator>()` lalu `.SetFlowing(active)`. Jadi: OnLevelStarted(false)=hidden, pump aktif(true)=muncul+mengalir, keluar level(false)=hidden. SetFlowing(true) juga enable renderer (ProcessPipeFlowAnimator.SetFlowing toggle r.enabled).

**VERIFIED (reflection edit-mode)**: init rendEnabled=False (hidden); invoke SetLevel4PipeFlow(true)→rendEnabled=True+flowing; (false)→rendEnabled=False. Compile 0 error CS. Scene saved (renderer disabled + _flowOnStart=false).

**Files**: Assets/Scripts/Simulation/ProcessPipeFlowAnimator.cs (mpb null guard), Level4SlurryPumpController.cs (field + toggle di SetLevel4PipeFlow), Assets/Scenes/Level1.unity (_flowOnStart=false + renderer off SlurryToPreheater_SlurryFlow, saved).


---

### 2026-06-07 (Part 51) — Frustum + Occlusion Culling di scene Level1_MainBroken (bake + static flags + verifikasi)

**User**: tanya konsep frustum culling vs occlusion culling, lalu minta diterapkan di game VR ini ("GAS, Occlusion + Frustum").

**Scene aktif**: `Assets/Scenes/Level1_MainBroken.unity` (BUKAN Level1.unity — ada 3 scene: Level1.unity 6/4, Level1_MainBroken.unity 6/7 dimodif hari ini = yang dikerjakan, Level1_MergeDesign.unity 6/4). **CATATAN: occlusion data per-scene; kalau pindah ke Level1.unity harus rebake di sana.**

**Baseline (execute_code)**: 6302 renderer aktif, ~1,43 jt triangle, cuma 84 ber-flag static, `umbraDataSize=0` (occlusion BELUM pernah bake), 0 LODGroup, Main Camera far=1000 occ=True, scene bounds 420×22×380m. URP "Performance URP Config" Quality Very Low, shadowDistance 85.

**Frustum culling**: otomatis (built-in), tak perlu setup. Terbukti efektif: dari viewpoint spawn DCS (-7.68,1.52,-5.41), 6332 mesh renderer → cuma **178 di dalam frustum** (~97% dipotong frustum dari sudut itu).

**Occlusion culling — yang dikerjakan**:
1. **Static flags** (execute_code, exclusion heuristik): excludeAll (runtime/_fx/flow/particle/ore_belt/conveyor/dust/player/xr/camera/canvas/spawn/teleport/billboard/audio/light) di-skip total; excludeOccluder (handwheel/flywheel/agitator/rake/impeller/spoke/stir/roller/pulley/valve/door/rotor/blade = rotator-in-place) → Occludee saja. Sisanya: Occludee Static; yang maxDim≥3m → + Occluder Static. Hasil: setOccludee=4723, setOccluder=1045, skipped=2840. (Edit-mode active-only akhir: Occludee=3708, Occluder=830.)
2. **Parameter bake**: `smallestOccluder=2.5` (mesin/dinding besar), `smallestHole=0.25`, `backfaceThreshold=100`.
3. **Bake**: `StaticOcclusionCulling.Compute()`. **Data**: umbra=298080, asset disk `Assets/Scenes/Level1_MainBroken/OcclusionCullingData.asset` = **814.724 byte**. Camera `useOcclusionCulling=True`. Scene saved, sceneDirty=False.

**GOTCHA PENTING (occlusion bake via MCP/Unity 6)**:
- `Compute()` jalan ASYNC: return cepat (3-23s) tapi `umbraDataSize` baca 0 SAAT ITU; data terdaftar ~10-20s wall-time KEMUDIAN (poll di call berikutnya → 298080). JANGAN panik lihat 0 langsung setelah Compute.
- **JANGAN** `GenerateInBackground()` setelah `Compute()` — itu mulai re-bake yang MENGOSONGKAN data lama; lalu `Cancel()` meninggalkannya kosong (umbra=0). Cukup `Compute()` saja, tunggu, poll.
- `umbraDataSize` baca **0 sesaat setelah SaveOpenScenes** (runtime PVS di-unload), TAPI data tetap aman di asset disk → lazy-reload balik ke 298080 di call berikutnya. Verifikasi sebenarnya = ukuran file `OcclusionCullingData.asset` di disk, bukan umbraDataSize transient.
- `MarkAllScenesDirty()` sebelum save bikin umbra flicker 0 juga — tak perlu, cukup SaveOpenScenes.
- `manage_graphics stats_get` return **draw_calls=0 di play mode lewat MCP** (UnityStats butuh Game view fokus/render) — tak andal. A/B occlusion via `Renderer.isVisible` JUGA noisy (isVisible agregat semua kamera termasuk Scene view di play mode) → toggle occ on/off kasih hasil sama (inconclusive). Verifikasi occlusion andal: pakai Occlusion Culling Visualization window / Stats panel / Frame Debugger di editor manual, ATAU posisikan kamera di ruang tertutup + tutup Scene view.

**Rekomendasi disampaikan ke user** (belum dikerjakan): LOD groups (0 sekarang) untuk mesin jauh, `QualitySettings.layerCullDistances` untuk pisah area jauh (Dry Stack z~230 vs CCD z~110), pertimbangkan turunkan far plane 1000 (opsional, hati2 clip struktur besar lintas-plant). Rebake occlusion tiap layout mesin berubah (overhead maintenance nyata mengingat sering replace FBX instance).

**Files**: Assets/Scenes/Level1_MainBroken.unity (static flags + camera occ + saved), Assets/Scenes/Level1_MainBroken/OcclusionCullingData.asset (BARU, 814KB bake). Tidak ada .cs diubah.


---

### 2026-06-08 (Part 52) — FIX FATAL: masker & HT hilang gara-gara Occludee Static (occlusion culling, BUKAN frustum)

**User**: setelah bake occlusion Part 51, masker & HT KADANG tidak nampak (fatal). Tanya: occlusion culling atau frustum culling?

**JAWABAN: OCCLUSION CULLING.** Akar masalah: bulk-marking Part 51 menandai objek **Occludee Static** secara luas. Masker & HT itu objek BERGERAK (di-equip/menempel ke player). Static occlusion menganggap posisi occludee TETAP di posisi saat bake → begitu objek dibawa pindah, sistem culling pakai cell baked lama → objek ke-cull (hilang) walau di depan mata. Frustum culling TIDAK begini (pakai bounds aktual real-time). **Prinsip: objek yang TRANSLASI saat runtime TIDAK BOLEH Occludee/Occluder Static.**

**Culprit spesifik (verified)**: `APD Level 2/Socket_Scanner_RespiratorMask/RespiratorMask` & `APD Level 2/Socket_Scanner_WalkieTalkie/Walkie Talkie` — keduanya Occludee Static. Plus seluruh subtree `XR Origin (XR Rig)` (controller visual, `TorsoAnchor/Socket_WalkieTalkie/WT_ChestDock_Visual`, dll) ikut ke-mark.

**FIX (execute_code, edit-mode)**:
1. Clear flag `OccludeeStatic|OccluderStatic` rekursif dari subtree: `XR Origin (XR Rig)`, `APD Level 2` + objek nama mengandung walkie/masker/respirator/mask/helm/kacamata/sarung/earplug/rompi/sepatu/scanner/held/carry/handheld. → **45 flag dibersihkan**.
2. `StaticOcclusionCulling.Clear()` + `Compute()` rebake → umbra 298080→**295936** (lebih kecil, konsisten objek bergerak dikeluarkan dari occludee set).
3. `SaveOpenScenes()`. Verifikasi: RespiratorMask & Walkie Talkie `Occludee=False Occluder=False`; leftover static di XR/APD = **0**; `cam.useOcclusionCulling=True` (occlusion tetap jalan utk mesin statis); sceneDirty=False.

**Hasil**: masker/HT (& semua item equippable + isi XR rig) kini DINAMIS → tak pernah di-cull static occlusion → tampil benar kapan pun in-view (cuma frustum-cull by bounds aktual). Occlusion benefit utk mesin statis tetap ada.

**LESSON occlusion culling (WAJIB)**: sebelum bake, EXCLUDE dari Occludee/Occluder Static: (a) semua child `XR Origin`/player rig, (b) semua prop equippable/held (APD, masker, HT, scanner), (c) apa pun yang re-parent/translasi saat runtime. Rotator-in-place (handwheel/agitator/flywheel) relatif aman (bounds tetap) tapi sebaiknya occludee-only, bukan occluder. Heuristik exclude Part 51 KURANG kata kunci ini → menyebabkan bug. Scene: Level1_MainBroken.unity.

**Files**: Assets/Scenes/Level1_MainBroken.unity (clear static flags XR/APD + rebake occlusion, saved), Assets/Scenes/Level1_MainBroken/OcclusionCullingData.asset (re-baked). Belum di-commit/push (user belum minta).


---

### 2026-06-08 (Part 53) — FIX HT lengket gak bisa di-grab + buat Socket_Gloves_Baju di dada

**User**: (1) HT (Walkie Talkie) gak bisa diambil sama sekali — lengket/nempel terus (regresi setelah rescript). Awalnya bisa grab → lapor → balik ke posisi. (2) Buat socket sarung tangan di dada (TorsoAnchor) seperti masker, nempel di bawah-tengah badan.

**Struktur socket dada (TorsoAnchor child XR Origin, localPos 0,1.04,0.22)**:
- `Socket_Respirator_Baju` (masker) localPos (0.22,-0.08,0.02): **XRSocketInteractor + BoxCollider** (bersih, jadi acuan).
- `Socket_WalkieTalkie` (HT) localPos (-0.22,-0.08,0.02): XRSocketInteractor + BoxCollider + **WalkieTalkieWearableSocket** (script custom = biang lengket).

**ROOT CAUSE (2 loop re-dock per-frame yang bertengkar dgn XRSocketInteractor)**:
1. `WalkieTalkieWearableSocket.LateUpdate` → `DockNow()` TIAP FRAME saat HT tidak `isSelected` (DockNow juga `SelectExit` SEMUA interactor termasuk socket).
2. `PhaseManager.Update` → tiap 0.25s panggil `PastikanWalkieTalkieAdaDiSocketDada()` (→ DockNow) saat `WalkieBelumDiSocketDada()` true. Begitu HT di-grab keluar socket, parent berubah → `WalkieBelumDiSocketDada()` true → tiap 0.25s HT direnggut balik ke dada = gak bisa dipegang.

**FIX (2 file, compile 0 error)**:
- `WalkieTalkieWearableSocket.cs` LateUpdate di-rewrite: deteksi `heldByHand` = `_grab.isSelected` oleh interactor NON-`XRSocketInteractor` (tangan/ray). Saat heldByHand → JANGAN dock (biarkan dibawa). Saat transisi dilepas (`_wasSelected` true→false) → `DockNow()` SEKALI saja. Saat idle → biarkan XRSocketInteractor dada menahan HT (seperti masker), TANPA DockNow per-frame → HT tetap bisa di-grab. `_wasSelected` sekarang lacak heldByHand.
- `PhaseManager.cs`: tambah `WalkieSedangDipegangPlayer()` (cek grab.isSelected oleh interactor non-socket). Guard kondisi Update re-dock: tambah `&& !WalkieSedangDipegangPlayer()` → tidak menarik HT balik selama dipegang player.
- Blok di `WalkieTalkieManager.cs` (PTT show/hide HT di tangan) TIDAK disentuh — event-driven (tekan/lepas T), bukan per-frame, sudah benar (re-enable grab + dock sekali saat selesai).

**Socket sarung tangan dada (request #2)**: duplikat `Socket_Respirator_Baju` → rename `Socket_Gloves_Baju`, child TorsoAnchor, localPos **(0, -0.22, 0.04)** (bawah-tengah dada), buang child duplikat + komponen WalkieTalkieWearableSocket bila ke-copy → tersisa **XRSocketInteractor + BoxCollider** (identik pola masker). Verified: 3 socket dada (Respirator 0.22/-0.08, WalkieTalkie -0.22/-0.08, Gloves 0/-0.22) semua socketInteractor=True. Scene saved.

**CATATAN**: socket gloves dibuat sesuai permintaan "buat soketnya aja" — belum di-wire ke objek gloves/flow gameplay (objek `Gloves` masih di `APD Level 2/Socket_Scanner_Gloves` + ada `Socket_Gloves` di Main Camera). Kalau mau gloves otomatis nempel & flow APD pakai socket dada ini, perlu wiring tambahan (PhaseManager/Level controller) — tanya user dulu.

**LESSON**: untuk item wearable di socket dada, PAKAI XRSocketInteractor saja (seperti masker) — jangan tambah script yang DockNow/SelectExit tiap frame (bertengkar dgn socket → item lengket/gak bisa di-grab). Re-dock paksa HARUS guard "sedang dipegang player" (cek interactor non-socket).

**Files**: Assets/Scripts/Simulation/WalkieTalkieWearableSocket.cs (LateUpdate rewrite), Assets/Scripts/Simulation/PhaseManager.cs (WalkieSedangDipegangPlayer + guard Update), Assets/Scenes/Level1_MainBroken.unity (Socket_Gloves_Baju, saved). Belum commit/push (user belum minta).


---

### 2026-06-08 (Part 54) — Level 3: hapus teleport observasi ngawur (tetap di SpawnPoint_Lvl3) + fix ChoicePanel button VR-clickable

**User**: (1) Setelah ore masuk semua, player malah ke-teleport ke spot ngawur (di dalam/dekat tank lihat agitator). Mau TETAP di SpawnPoint_Lvl3. (2) `Level3_ChoicePanel_Auto` button cuma bisa diklik mouse, bukan VR.

**FIX #1 — teleport observasi**: di `Level3OreSlurryController`, saat ore sampai slurry (`NotifyLevel3OreReachedSlurry`) ada `if(_teleportKeTitikObservasi && _teleportTargetObservation!=null) TeleportPlayer(_teleportTargetObservation)` → `_teleportTargetObservation` = "SpawnPoint_Lvl3 - Slurry Tank" = spot ngawur di tank. Solusi: matikan `_teleportKeTitikObservasi`. Ubah default field `true`→`false` DI KODE, + set nilai SERIALIZED di komponen scene `false` (tadinya True, override default — pakai SerializedObject). Player tetap di `_teleportTargetField`=SpawnPoint_Lvl3 (posisi field observasi). Scene saved. (GOTCHA dikonfirmasi lagi: serialized scene value override code default → wajib set keduanya.)

**FIX #2 — ChoicePanel button VR (`LevelTransitionChoicePanel.cs`)**: panel SUDAH attach XRSimpleInteractable+BoxCollider, TAPI `AttachXrSimpleInteractable` set `bc.size = rect.sizeDelta` → untuk button anchor-stretch (anchorMin≠anchorMax, offset 0), `sizeDelta`≈(0,0) → collider nyaris nol → ray VR tak pernah kena (mouse kena via GraphicRaycaster). FIX: `Canvas.ForceUpdateCanvases()` lalu pakai `rect.rect.width/height` (fallback hitung dari fraksi anchor × ukuran canvas parent bila rect belum valid), `bc.size=(w,h,20)`, register `simple.colliders` eksplisit. **VERIFIED play-mode**: collider Btn_Lanjut & Btn_Lihat = (430,187,20) → ≈0.43×0.19m dunia, xrSimple=True, collidersReg=1 (sebelumnya ~0). Ray VR sekarang kena.

**LESSON (VR world-space button)**: BoxCollider untuk XRSimpleInteractable di UI anchor-stretch JANGAN pakai `rect.sizeDelta` (≈0). Pakai `rect.rect.width/height` setelah `Canvas.ForceUpdateCanvases()`, atau hitung dari fraksi anchor × ukuran parent. Tambah ketebalan Z (≥20 canvas unit) supaya ray kena.

**Files**: Assets/Scripts/Simulation/Level3OreSlurryController.cs (default flag false), Assets/Scripts/UI/LevelTransitionChoicePanel.cs (collider sizing fix), Assets/Scenes/Level1_MainBroken.unity (_teleportKeTitikObservasi=false serialized, saved). 0 compile error. Belum commit/push.


---

### 2026-06-08 (Part 55) — Ore di belt JANGAN menggelinding (freezeRotation) tapi tetap bergerak

**User**: ore di conveyor belt Level 3 (`Level3_Runtime_Ore_Belt_Flow` > `Ore_Rock_On_Belt_00..07`, komponen `OreBeltConveyorPhysics`) saat run MENGGELINDING/berputar. Mau tetap bergerak maju tapi animasi diam (tanpa rotasi).

**Sebab**: ore = Rigidbody dinamis didorong velocity + gesekan belt → akumulasi angular velocity = menggelinding. Plus `ResetOreToBelt()` set `rb.transform.rotation = Random.rotation` (orientasi acak tiap reset).

**FIX (`OreBeltConveyorPhysics.cs`, 4 edit, 0 error)**:
- `SetupOres()`: tambah `rb.freezeRotation = true;` (kunci SEMUA rotasi fisika; translasi tetap jalan). Tambah list `_spawnRotations` + capture `child.rotation` saat setup (sejajar `_ores`/`_spawnPositions`).
- `ResetOreToBelt()`: ganti `Random.rotation` → `_spawnRotations[i]` (orientasi authored TETAP, tidak acak/berputar).
- Translasi (`vel.x/z = horiz * _conveyorSpeed` di FixedUpdate) TIDAK disentuh → ore tetap bergerak maju ke tank.

**VERIFIED play-mode**: `freezeRotation=True`, `angularVelocity.magnitude=0` semua ore, rotasi euler konstan sebelum/sesudah. CATATAN verifikasi: posisi tampak diam saat probe karena **frame play mode tidak maju di antara execute_code call** (Thread.Sleep memblok main thread; MCP probe cepat tak nge-tick FixedUpdate) — ini artefak test, BUKAN regresi. `freezeRotation` hanya kunci rotasi, tak pernah posisi; logika translasi utuh → gerak maju tetap jalan.

**LESSON**: (1) untuk objek "bergerak tapi jangan berputar" → `Rigidbody.freezeRotation=true` (kunci rotasi, translasi via velocity tetap). (2) Verifikasi GERAK runtime via MCP tidak andal: `Thread.Sleep` di execute_code memblok tick Unity; probe antar-call kadang 0 frame maju → posisi tampak statis palsu. Pakai play asli user untuk konfirmasi gerak, atau Physics.Simulate manual.

**Files**: Assets/Scripts/Simulation/OreBeltConveyorPhysics.cs (freezeRotation + spawn rotation tetap). Tidak ada scene save (perubahan kode runtime; komponen di objek runtime). Belum commit/push.


---

### 2026-06-08 (Part 56) — FIX ore nyangkut di chute (Ore_Rock_On_Belt_10/11 stuck di bibir tank)

**User**: 1 ore (`Ore_Rock_On_Belt_10`) stuck di (99.5, 10.49, 56) — area chute/bibir tank, gak masuk tank.

**Sebab**: di `OreBeltConveyorPhysics.FixedUpdate`, cabang chute (`pos.x <= _headX=100`) kode LAMA cuma dorong HORIZONTAL (`v.x/v.z` ke pusat tank) + andalkan gravitasi turun. Ore yang ke-nudge naik (anti-stall, y=10.49 di atas head belt 9.25) nyangkut di collider chute → gravitasi gak bisa narik turun → stuck selamanya. Tak ada timeout/force-catch.

**FIX (`OreBeltConveyorPhysics.cs` cabang chute)**: arahkan velocity LANGSUNG ke titik DALAM tank 3D `(tankCenterX, tankCatchY, tankCenterZ)` (turun + ke pusat, full _conveyorSpeed) + ANTI-NYANGKUT: akumulasi `_stallTimer` di chute, force-catch (`_fellIntoTank.Add` + `SetActive(false)`) bila `dist<=1.5` ATAU `prevChute>2.5s`. Slurry menggantikan ore secara visual di tank.

**VERIFIED play-mode**: SetRunning(true) → ore bergerak naik belt (x 138→100) → masuk tank progresif → **12/12 fell, SemuaOreMasukTank=True, 0 ore tersisa** (termasuk ore10/11 yg tadinya stuck). 

**LESSON KRITIS (MCP play-test)**: setelah edit script, `manage_editor play` LANGSUNG kadang JALANKAN ASSEMBLY LAMA (domain belum reload) → fix tampak "gagal". Tanda: field runtime baru (mis. `_stallTimer` entry) tak muncul walau logika seharusnya set. SOLUSI: setelah edit, WAJIB `refresh_unity(compile=request, scope=scripts, wait_for_ready)` + cek `EditorApplication.isCompiling==false` SEBELUM `play`. (Part 55 frame-not-advancing + Part 56 stale-assembly = dua jebakan verifikasi MCP play-mode.) Frame play mode JUGA hanya maju kalau tidak diblok Thread.Sleep; poll lintas call tanpa sleep → frame maju normal (terbukti frame 1→247→729).

**Files**: Assets/Scripts/Simulation/OreBeltConveyorPhysics.cs (chute drive-to-tank + force-catch). Belum commit/push.


---

### 2026-06-08 (Part 57) — UniversalTaskMarker per-task Level 4 (Slurry Pump)

**User**: Level 4 belum punya UniversalTaskMarker untuk SETIAP task (cuma DCS button + HT).

**Sebab**: `UniversalTaskMarker.ResolveTarget` case Level4_SlurryPump lama cuma `if(!SudahTekanTombolDcs) DcsButton(4); if(!SudahLaporanHt) WalkieTalkie;` → task flow-rate, observasi pump, observasi preheater TIDAK ada marker.

**FIX (`UniversalTaskMarker.cs`)**: case Level4 → `ResolveLevel4Target()` baru, phase-aware pakai `_glm.CurrentLevel4Phase` (enum `Level4Phase`, backing field `_level4Phase`):
- Idle/MenungguTombolDcs → `FindDcsButton(4)`
- AturFlowRate → `FindByName("Btn_FlowPlus","Widget_FlowRate","A_PARAM_Flow_PLUS")` (tombol + flow di meja DCS)
- MenungguLaporanFlow → `FindWalkieTalkie()` (lapor awal "slurry pump aktif")
- ObservasiPump → `FindByName("SlurryPump_Field","PumpMotor_Audio")`
- ObservasiPreheater → `FindByName("Level5_PreHeater_Blender_Industrial_UV_Overview (1)", ...)` (instance z~56 dekat pump)
- MenungguLaporanAkhir → `FindWalkieTalkie()` (lapor akhir "cairan sudah di preheater")
- KembaliKeDcs/Selesai → null

**Nama objek verified di scene**: Btn_FlowPlus ADA (-4.33,9.46,17.74), SlurryPump_Field ADA (90.37,0.5,56.43), PumpMotor_Audio ADA. **PreHeater_Field_1/PreHeater_Field TIDAK ADA** (controller AutoFind pakai nama itu tapi objek scene-nya `Level5_PreHeater_Blender_Industrial_UV_Overview (1)` @ z56.21) → pakai nama itu utk marker.

**VERIFIED play-mode (reflection, set `_level4Phase` tiap nilai, invoke ResolveLevel4Target)**: Idle/MenungguTombolDcs→Tombol DCS 4, AturFlowRate→Btn_FlowPlus, ObservasiPump→SlurryPump_Field, ObservasiPreheater→Level5_PreHeater...(1), MenungguLaporanFlow & MenungguLaporanAkhir→Walkie Talkie, KembaliKeDcs/Selesai→null. SEMUA task Level 4 kini punya marker. 0 compile error.

**LESSON**: UniversalTaskMarker resolver per-level = switch by level lalu (untuk level multi-step) by phase controller (`CurrentLevelNPhase`). Verifikasi cepat: set backing field enum fase via reflection + invoke resolver method (tak perlu main full level). Nama target WAJIB dicek ada di scene dulu (PreHeater_Field_1 ternyata nama mati).

**Files**: Assets/Scripts/UI/UniversalTaskMarker.cs (ResolveLevel4Target + case Level4). Belum commit/push.


---

### 2026-06-08 (Part 58) — Level 6 fixes: spawn field/DCS, task masker, steer hilang (occlusion handwheel)

**User (4 hal)**: (1) spawn field Level 6 ketinggian, mau pas di SpawnPoint_Lvl6; (2) balik ke DCS malah tenggelam; (3) tambah task pakai masker di field sebelum lapor HT; (4) steer/handwheel hilang saat play (ada di edit).

**Diagnosa teleport**: pola KANONIK proyek (LevelTeleportManager/Level10/14) = spawn point = posisi KAKI; teleport = `MoveCameraToWorldLocation(feet + up*CameraYOffset)` + `MatchOriginUpCameraForward`. Level 6 `TeleportPlayer` SALAH: `MoveCameraToWorldLocation(target)` + `SetPositionAndRotation(target)` (double, tanpa CameraYOffset). CameraYOffset=1.36. SpawnPoint_Lvl6 di y=1.53 padahal lantai field (Industrial_Site_Ground) y=0 → feet melayang 1.53m. SpawnPoint_DCS y=8.36 (benar, Level 2 pakai ini).

**FIX (1+2) — `Level6AcidInjectionController.TeleportPlayer` → kanonik**: `cameraTarget = target.position + up*origin.CameraYOffset; MoveCameraToWorldLocation(cameraTarget); MatchOriginUpCameraForward(...)`. HAPUS `SetPositionAndRotation(target)` override (else-branch fallback hanya saat XROrigin null, kompensasi camY). + **turunkan SpawnPoint_Lvl6 y 1.53→0.05** (ke lantai). VERIFIED play (invoke TeleportPlayer reflection): SpawnPoint_Lvl6 → rigY=0.05 camY=1.41 (berdiri di lantai, tak melayang); SpawnPoint_DCS → rigY=8.36 camY=9.72 (sama Level 2, tak tenggelam).

**FIX (3) — task masker**: `PlayerHUD` checklist Level 6, sisip `{Check(PhaseManager.isRespiratorWorn)} Pakai masker (APD wajib sebelum kerja di lapangan)` setelah lapor outlet, sebelum "Putar valve preheater". + `UniversalTaskMarker.ResolveLevel6Target`: setelah Level6OutletReportDone, jika `!isRespiratorWorn` → marker arahkan ke `RespiratorMask`/`Socket_Respirator_Baju` (sebelum valve).

**FIX (4) — STEER HILANG saat play = OCCLUSION CULLING lagi**: 115 part handwheel/valve/wheel/spoke ber-OccludeeStatic (dari bulk-mark Part 51; exclude-occluder TAPI tetap occludee). Part kecil dekat occluder besar (autoclave) → di-false-cull bake → hilang di play (ada di edit krn occlusion cuma aktif runtime). FIX: clear Occludee+Occluder dari part nama mengandung handwheel/valve/spoke/wheel/stir/grip/_rim/steer/knob/lever (**105 dibersihkan**) → rebake occlusion (umbra 295936→292176) → save scene.

**LESSON**: (a) Teleport XR HARUS kanonik: spawn=KAKI, `MoveCameraToWorldLocation(feet+up*CameraYOffset)`, JANGAN tambah `SetPositionAndRotation(spawn)` (bikin rig-root di spawn → kamera +1.36 ketinggian). (b) Occlusion culling = musuh berulang objek interaktif/kecil (handwheel, masker, HT) → SELALU exclude dari OccludeeStatic sebelum bake. Part 52 (masker/HT) + Part 58 (handwheel) pola sama.

**Files**: Level6AcidInjectionController.cs (TeleportPlayer kanonik), PlayerHUD.cs (task masker L6), UniversalTaskMarker.cs (marker masker L6), Level1_MainBroken.unity (SpawnPoint_Lvl6 y→0.05, clear 105 occludee handwheel, rebake occlusion, saved). 0 compile error. Belum commit/push.


---

### 2026-06-09 (Part 59) — Level 11 (Tailing) REWORK jadi HT-gated liquid flow + deep research urutan proses

**Konteks**: Lanjutan Level 10 (MHP) selesai (finale filter press + reset replay verified, Part TASK 9). User minta mekanik Level 11 (Tailing, enum `Level12_TailingDischarge`, controller `Level12TailingFilterController`, DCS 11) diubah: research dulu bentuk limestone (bongkahan vs cairan) + urutan proses (apakah dijemur dulu sebelum filter press).

**DEEP RESEARCH (web: BSSA, Nickel Institute, Carmeuse/National Lime Assoc, MDPI filtered tailings, UWA ACG, tailings.info, Earthworks Indonesia)**:
1. **Limestone/kapur = CAIRAN SLURRY** (milk of lime / lime slurry), bukan bongkahan. Limestone batu digiling→serbuk→slurry di-pump; atau CaO→slake→Ca(OH)2 serbuk→+air = susu kapur. Masuk tank sbg cairan di-dosing + agitator. → mekanik HARUS via HT (gaya Level 10), bukan taruh batu.
2. **Urutan user (tank→dry stack→jemur→filter press) SALAH/terbalik.** Benar: tank netralisasi → **FILTER PRESS (peras air mekanis dulu → cake ~22%)** → angkut ke DRY STACK → **baru dijemur+dipadatkan di dry stack**. Alasan fisika: keluar tank masih slurry cair (bisa dipompa); cake kering hasil filter press TIDAK bisa dialirkan/dipompa ("filtered cake no longer transported by pipeline" tailings.info; "dry stacking... cannot be pumped" UWA). Penjemuran = evaporative drying/konsolidasi SETELAH deposisi di dry stack (Level 12), bukan pra-filter-press. Disampaikan ke user dgn jelas.

**REWORK `Level12TailingFilterController.cs` (button-gated → HT-gated, mirror pola Level11MHPController)**:
- Hook `WalkieTalkieManager.OnPTTDilepas += OnTailingHtReleased` di OnEnable/OnDisable.
- State `_await`: 0 none, 1 alirkan tailing, 2 dosing kapur, 3 filter press, 4 report. Fallback keyboard SPACE/1 di Update.
- `StartFieldSequence`: DCS11 → fade → teleport depan tank netralisasi V3 (stand 32,1.5,138.5 hadap tank) → `EnsureLiquidBody` + `HideTailingLiquid` (tank KOSONG di awal) → info panel → `_await=1`.
- **HT #1 `FillTailingRoutine`**: cairan tailing asam NAIK dari dasar (silinder `Tailing_Neut_LiquidBody` r4.6 di tank V3 center 39.1,2.3,142.8; scaleY 0.02→full + posisi naik dari bottomY 1.30; `Neutralized_Surface` ikut naik). Warna coklat-asam `_colAcidTailing (0.40,0.26,0.13)`. pH 2.3.
- **HT #2 `DoseLimeRoutine`**: `ShowLimePour` (Limestone_Pour_Stream di-tint putih susu kapur, fallback bikin cylinder runtime) + `ShowBubbles` (gelembung reaksi) + warna lerp coklat-asam→abu-kehijauan netral `_colNeutralTailing (0.52,0.56,0.50)` + pH 2.3→8.0 + jarum pH. Beacon hijau ON. `_neutralizeDone=true`, `_await=3`.
- **HT #3 `RunFilterPressRoutine`**: fade → teleport ke filter press (33.5,1.5,152) → `FilterPressRoutine` existing (8 cake muncul progresif, moisture 60→22%, roller spin) → stage=2.
- Inspeksi proximity (Cake_On_Conveyor) → Compliance QC pop-up → ACCEPT → `NotifyLevel12TailingFilterComplete` → lapor HT "limbah dialirkan" (tak diubah).
- **Warna via MATERIAL INSTANCE** (`_liquidBodyMat`, `_surfMat` + `SetMatColor`), BUKAN MaterialPropertyBlock (gotcha SRP Batcher abaikan MPB utk _BaseColor — sama spt Level 10).
- `TryAction` jadi thin wrapper → OnTailingHtReleased (tombol konsol = fallback HT, disembunyikan). BeginStage/NeutralizeRoutine lama dihapus.

**BUG ditemukan & FIXED (play-test)**: gelembung tak berhenti — `EmitBubbles` loop pakai `while(_bubbles.isPlaying)` + `Emit()` manual terus bikin partikel hidup → isPlaying selalu true → infinite. FIX: flag eksplisit `_bubblesOn`; `ShowBubbles(false)` → `Stop(true, StopEmittingAndClear)`; loop `while(_bubblesOn)`. Verified particleCount=0 setelah dose.

**VERIFIED play-mode (reflection, HT gate dipicu manual)**: DCS11→await=1, liquidBody dibuat+HIDDEN (tank kosong). HT#1→cairan naik scaleY 0.02→1.0, pH 2.3, await=2. HT#2→pH 2.3→8.00, warna→(0.52,0.56,0.50) netral, NeutralizeDone, gelembung jalan lalu BERHENTI bersih, await=3. HT#3→FilterPressDone, moisture 22%, 8 cake aktif, stage=2. Inspeksi+QC+OnAccept→ComplianceAccepted, QuestComplete, GLM `_level12TailingFilterComplete`=True. Screenshot tank: cairan abu-kehijauan terisi + gelembung + filter press. Compile 0 error. Console noise baseline only.

**STATUS: Level 11 HT-gated FULLY FUNCTIONAL & VERIFIED.** Alur: DCS11 → HT(alirkan tailing naik dari dasar, coklat asam) → HT(susu kapur turun, warna→netral abu-kehijauan, pH 2.3→8, gelembung) → HT(filter press, cake 22%) → inspeksi → compliance QC → lapor HT "limbah dialirkan". TIDAK perlu save scene (liquid body/bubbles/lime pour semua runtime; controller sudah ter-attach; tak ada objek scene berubah). Belum commit/push.

**Files**: Assets/Scripts/Simulation/Level12TailingFilterController.cs (rework HT-gated + liquid rise + lime slurry + bubbles + fix). Screenshots Assets/Screenshots (harmless).


---

### 2026-06-09 (Part 60) — Tangki Netralisasi Tailing (Level 11) REBUILD industrial + fluida gaya autoclave (transparan, lihat liquid)

**User**: bangun ulang TANGKI netralisasi tailing realistis seperti industri nikel via Blender headless, agak transparan biar liquid kelihatan, fluida di-upgrade ke style autoclave; HT#1-#3 dari Part 59 dipertahankan. Tanya juga kenapa Blender MCP gagal konek di Kiro.

**Build (Blender headless, file Assets/Art/TailingNeutTankBlender/build_tailing_neut_tank.py sudah ada dari sesi sebelumnya)**: jalan via lender.exe --background --python ... -> TailingNeutTank_IndustrialUV.fbx (592KB). Authored Z-up local origin, parent ke empty rig TailingNeutTank_IndustrialRig, export pply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_up=Y, axis_forward=-Z, object_types={EMPTY,MESH}. Komponen TNT_*: pad beton + plinth + 8 anchor bolt, dished bottom + lantai, **shell silinder semi-transparan TNT_Shell_Glass** (Rsh 2.90, FLOOR_Z 1.30 -> WALL_TOP 6.60), 4 weld stiffener ring, hazard band low/high, top curb, overflow launder + downpipe, inlet nozzle bertopologi flange (sisi -X 210deg, dari arah CCD underflow), sludge outlet bawah, drive bridge A+B + grate deck + gearbox + motor blue + fan, **drive shaft + 2-tier pitched-blade impeller (TNT_Impeller_Hub_0/1, TNT_Impeller_Blade_*)** (di-reparent ke pivot agitator existing), lime lance + header + nozzle, pH probe + gauge box + green status lamp, ladder dengan safety cage 4 hoop, handrail platform grate + 10 post + top/mid rail, label plate.

**Integrasi scene (execute_code edit-mode)**: InstantiatePrefab FBX -> TailingNeutTank_IndustrialUV parent Level13_DryStack_Field/Level12_13_Tailing_IndustrialUV_BlenderRig_V3, world pos (39.08, 0, 142.83) rot identity, lossy=1 (compensate parent scale). DestroyImmediate Neutralization_Tank_Shell lama. Renderer TNT_Shell_Glass di-overlay material baru M_TailTank_GlassTransparent (URP/Lit transparent: _Surface=1, SrcBlend5/DstBlend10, ZWrite=0, queue 3000, alpha 0.20, smoothness 0.85). 11 part impeller (TNT_Impeller_* + TNT_Agitator_Shaft) di-reparent ke Polishing_Agitator_Root (controller terus memutarnya). 4 Polishing_Agitator_Blade_NN lama disembunyikan. tank bounds ctr (39.52, 4.32, 142.83) size (8.01, 8.64, 7.14). Scene saved.

**Upgrade fluida ke gaya AUTOCLAVE (Level12TailingFilterController.cs)**:
- EnsureLiquidBody di-rewrite: cylinder VOLUME PENUH (tinggi cy 4.4m menutup _fillBottom 1.30 .. _fillTop 5.15), **diameter dunia 5.0** via kompensasi parent.lossyScale (localScale = 5.0/lossy.x per axis) supaya cairan TIDAK nembus shell yang 5.8m diameter (parent rig lossy 1.28x bikin cylinder over-scale ke 6.93m kalau tak dikompensasi -- bug ditemukan & diperbaiki saat play-test).
- Material baru BuildTailingFluidMaterial: shader Olivia/L7SlurryFill (sama autoclave) dengan _SwirlSpeed 0.6, _SwirlStrength 0.30, _SwirlAxisZ 142.83, _SwirlSpacing 80, _SurfaceGlow 2.2, _DepthRange 4.0, _Alpha 0.82, _RippleStrength 0.05.
- HT#1 FillTailingRoutine: alih-alih scaling cylinder, animasi _FillY shader prop dari (_fillBottom-0.05) -> _fillTop selama 5.5s (Smooth) -> permukaan cairan naik gaya autoclave (world-Y clip + surface band glow).
- HT#2 DoseLimeRoutine: lerp 3 prop warna (_BaseColor shallow, _DeepColor, _EmissionColor) acid->neutral. Asam (0.46,0.29,0.13)/(0.26,0.15,0.06)/(0.20,0.10,0.03) -> Netral (0.55,0.60,0.52)/(0.30,0.38,0.32)/(0.10,0.16,0.08). pH 2.3 -> 8.0.
- HT#3 (filter press) tak diubah.
- Neutralized_Surface kini di-disable (shader sendiri punya surface band) -- hindari double-surface.
- _surfMat field tetap ada untuk backwards compat (tak dipakai aktif).

**VERIFIED play-mode (reflection)**: shader Olivia/L7SlurryFill aktif. HT#1 -> _FillY 5.15 (penuh), warna asam coklat, pH 2.3, _await=2. HT#2 -> warna netral abu-kehijauan (0.55,0.60,0.52), pH 8.0, NeutralizeDone=True, _await=3. liq world bounds size=(5.00, 4.40, 5.00) PAS di dalam shell 5.8m. Screenshot game-view positioned: tangki industrial bersih (drive bridge+motor+gearbox+handrail+launder+hazard band), shell transparan biru-kehijauan, **cairan netral kelihatan jelas di dalam, impeller terendam, surface glow band di permukaan**. Compile 0 error CS. Console cuma noise baseline.

**Files**:
- Assets/Art/TailingNeutTankBlender/build_tailing_neut_tank.py (sudah ada, dijalankan)
- Assets/Art/TailingNeutTankBlender/TailingNeutTank_IndustrialUV.fbx (BARU, 592KB)
- Assets/Scripts/Simulation/Level12TailingFilterController.cs (EnsureLiquidBody rewrite + BuildTailingFluidMaterial + SetFillY + SetFluidColors + FillTailingRoutine pakai _FillY + DoseLimeRoutine pakai 3 prop warna)
- Assets/Scenes/Level1_MainBroken.unity (instance tank baru, hapus shell lama, reparent impeller, hide blade lama, transparent material, saved)

**LESSON penting**:
1. Cylinder runtime di parent ber-scale != 1 -> WAJIB kompensasi localScale = wantWorldDim / parent.lossyScale.axis per axis. Memory tertulis berkali-kali tapi mudah lupa di code baru.
2. Object ambiguous (UnityEngine.Object vs object) di execute_code -> WAJIB UnityEngine.Object.DestroyImmediate(...) lengkap.
3. Shader Olivia/L7SlurryFill reusable untuk fluida tank manapun -- ganti _SwirlAxisZ ke pusat tank, _FillY untuk level, _BaseColor/_DeepColor/_EmissionColor untuk warna fasa proses.

**Blender MCP "connection failed" di Kiro -- DIAGNOSA & FIX**:
- Server side OK: uvx --python 3.12 blender-mcp start normal (test PowerShell, exit code 0, jalan terus). uvx 0.11.15 + uv terinstall di C:\Users\mp2dz\.local\bin.
- **Akar masalah: Blender side**. Server lender-mcp connect ke addon BlenderMCP via TCP socket (default 127.0.0.1:9876). Kalau Blender TIDAK running ATAU addon BlenderMCP belum di-enable + tombol "Connect to Claude" belum ditekan -> server gagal handshake -> Kiro tampil "connection failed".
- **Fix**: 
  1. Buka Blender 5.1.
  2. Edit > Preferences > Add-ons -> cari "BlenderMCP" atau install dari .py (https://github.com/ahujasid/blender-mcp).
  3. Enable centang. Save preferences.
  4. Di 3D Viewport, sidebar (N) -> tab "BlenderMCP" -> klik **"Connect to Claude"** (toggle hijau).
  5. Restart MCP server di Kiro (atau reload window) -> connection OK.
- **Workaround sementara (yang dipakai sesi ini)**: Blender headless --background --python script.py SELALU bekerja (independen MCP). Untuk build asset terjadwal/repeatable, headless lebih reliable.


---

### 2026-06-09 (Part 61) — Cairan tabung SURFACE rise-from-bottom (CCD+MHP), masker kuning fix, tailing liquid masuk tabung, Level 12 marker

**Konteks**: lanjutan request panjang user (TASK 2). Recovery setelah crash. Scene `Level1_MainBroken.unity`. Semua edit validate 0 error CS.

**Komponen kunci `TankFluidColumn.cs`** (sudah dibuat sesi sebelumnya): cairan TERANG satu volume via shader `Olivia/L7SlurryFill`. API: `Setup(renderer, shallow, deep, emis)` (hitung bottom/top + swirl center dari renderer.bounds), `SetLevel01(0..1)` (permukaan NAIK DARI DASAR via `_FillY`, BUKAN melebar dari tengah), `SetColors`, `SetSwirl(speed)` (rotor), `Hide/Show`.

**(1) CCD (Level10CCDController.cs) — cairan ungu pakai TankFluidColumn**:
- Tambah field `TankFluidColumn[] _ccdFluid[3]` + `bool _ccdRotorOn` + 6 warna konstanta ungu (turbid shallow/deep/emis -> clear shallow/deep/emis).
- `EnsureCcdFluidColumns()` BARU: attach TankFluidColumn ke tiap `CCDn_SettlingZone_XRayColumn` (volume X-ray utama, bounds ~10m diameter x 4.6m), SEMBUNYIKAN layer lama (`_clearPlsSurfaces` disc, `_feedwellCores`, `_underflowPools`) -> hilangkan DOUBLE-LIQUID + bug "melebar dari tengah".
- `PrepareCcdLiquidAtBottom` -> EnsureCcdFluidColumns + level 0 (kosong di dasar) + rotor off.
- `AnimateCcdLiquidRise` -> drive `_ccdFluid[i].SetLevel01(t)` (rise dari dasar), buang scaling settling-zone lama.
- `UpdateMudLayers` -> cuma lerp warna keruh->jernih via `SetColors` + level 1 (buang scaling underflow/feedwell yg melebar dari tengah).
- `AnimateCcdLiquidMotion` -> cuma `SetSwirl` (rotor) saat `_ccdRotorOn`, buang RotateAround transform (yg merusak bounds).
- `_ccdRotorOn=true` di-set SETELAH `AnimateCcdLiquidRise` selesai (di RunCCDSequence) -> rotor mengaduk hanya setelah cairan naik penuh.
- `SetProcessVisuals(false)` -> Hide semua fluid + swirl 0 (cairan ungu TAK tampil di awal).
- **VERIFIED edit-mode**: 3 settling zone resolve, TankFluidColumn Ready=True, `_FillY` lvl0=1.24 (dasar) -> lvl1=5.95 (atas) = NAIK DARI DASAR terbukti, shader Olivia/L7SlurryFill, swirl set 1.2 OK.

**(2) MHP (Level11MHPController.cs)** — sudah pakai TankFluidColumn (sesi sblm). VERIFIED: 3 `_tankFluid` Ready=True. Mekanisme `_FillY` sama persis CCD (proven).

**(3) MASKER KUNING Level 3 — FIXED (Level3OreSlurryController.cs `AktifkanGlowMaskerDiBaju`)**: glow masker pakai `_warnaGlowMasker=(1,0.85,0.2)` KUNING via MPB `_EmissionColor` + EnableKeyword `_EMISSION`. Saat glow OFF, kode LAMA cuma set `_EmissionColor=black` tapi MPB tetap nempel + keyword `_EMISSION` TETAP nyala -> masker kuning residual. **FIX**: saat OFF -> `_glowMpb.Clear()` + `rend.SetPropertyBlock(null)` (bersih total) + `DisableKeyword("_EMISSION")`. Material `Respirator_Material` base putih (1,1,1) -> kembali normal.

**(4) CANVAS VR Level 3** — fix Part 54 (`LevelTransitionChoicePanel.AttachXrSimpleInteractable`: collider pakai `rect.rect.width/height` setelah ForceUpdateCanvases, bukan sizeDelta~0; register colliders eksplisit) MASIH ADA & benar. Panel auto-create fresh tiap `EnsureChoicePanel` -> XR-clickable. (Kalau user masih lihat mouse-only, kemungkinan build lama; kode sudah benar.)

**(5) TAILING_NEUT_LIQUIDBODY "masih di luar tabung" — FIXED (Level12TailingFilterController.cs `EnsureLiquidBody`)**: liquid runtime pakai hardcoded `_neutTankCenter=(39.1,2.3,142.8)` + fillBottom 1.30, TAPI tangki rebuild (Part 60 TailingNeutTank_IndustrialUV) shell `TNT_Shell_Glass` bounds ctr (39.08,5.16,143.58) span y2.51-7.81 -> Z meleset 0.78m + floor 1.2m kerendahan = liquid di luar. **FIX**: `EnsureLiquidBody` sekarang DERIVE dari `TNT_Shell_Glass` renderer.bounds (center X/Z, fillBottom=min.y+0.20, fillTop=max.y-0.55, diameter=min(ext.x,ext.z)*2*0.86) + update `_SwirlAxisZ`/`_DepthRange`. **VERIFIED**: liqBounds ctr (39.08,5.16,143.58) ext 2.49 PAS di dalam shell ext 2.90 -> INSIDE_SHELL=True.

**(6) LEVEL 12 marker + kejelasan task** — `UniversalTaskMarker.ResolveLevel12TailingTarget` (phase-aware) + props `AwaitStage`/`StageNow` di controller SUDAH ada & compile. Alur: DCS11 -> HT gate await 1/2/3 (semua arah WalkieTalkie utk tahan T) -> stage2 inspeksi cake (Cake_On_Conveyor/Final_FilterPress_Unit) -> stage3 QC (null, tombol ACCEPT) -> HT akhir "limbah dialirkan".

**(7) UAP/asap** — MHP `ShowBubbles` sudah di-rework jadi thin vapor (Cone, gravityModifier -0.04 naik, colorOverLifetime fade, `_vaporTint` dinamis) — netralisasi HPAL eksotermik -> uap air+CO2 nyata, realistis putih-transparan tipis (bukan blok putih solid). Tailing `ShowBubbles` punya flag `_bubblesOn` (fix infinite loop Part 59).

**CATATAN console**: NRE `XRSocketInteractor.OnEnable line 292 'routine is null'` = bug internal XRI package (StartCoroutine saat GO transisi enable) — PRE-EXISTING (dari socket dada Part 53), non-blocking, BUKAN dari edit sesi ini. Semua socket activeInHierarchy=True di edit mode. Generators.ai.unity 'UserUnauthorized' = network benign.

**Files**: Level10CCDController.cs (TankFluidColumn CCD), Level3OreSlurryController.cs (masker glow clear), Level12TailingFilterController.cs (EnsureLiquidBody dari shell bounds). Compile 0 error. Scene TIDAK perlu save (semua cairan/fluid runtime; komponen sudah ter-attach). Belum commit/push (user belum minta).

**TODO next**: play-test VR asli (mekanik fluida sudah proven edit-mode + reflection). Opsional: bersihkan NRE XRSocketInteractor (guard GO active sebelum OnEnable, atau package issue). Commit Indonesia + push saat user minta.
