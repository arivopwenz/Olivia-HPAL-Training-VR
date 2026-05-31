# GAMEPLAY Level 8 — Flash Vessel 3-Stage Pressure Letdown + PLS Sampling

> **CATATAN MERGE (2026-05-29):** Level 8 (Flash Train 3-stage) dan Level 9 lama (Flash Vessel single-stage) sudah DIGABUNG jadi satu level: **Level 8 - Flash Vessel & Letdown**. Level 9 lama dipensiunkan (controller `Level9FlashVesselController` di-disable, enum `Level9_FlashVessel` di-skip otomatis di `MulaiLevel`). Urutan display level setelahnya digeser: CCD = Level 9, MHP = Level 10, Tailing = Level 11, Dry Stack = Level 12, Darurat = Level 13. Angka enum internal TIDAK diubah (kompatibilitas serialisasi scene).


## Referensi Industrial (HPAL Real World)

Setelah autoclave (Level 7), slurry keluar pada **250°C / 47-50 atm**. Tekanan dan suhu TIDAK BOLEH diturunkan langsung ke atmosfer karena:
1. **Steam flash uncontrolled** → ledakan vessel + thermal shock pipa
2. **Silica scaling** → kerak menempel keras di pipe wall jika cooling drop > 40°C/stage
3. **Heat recovery loss** → uap panas kalau dibuang langsung = energi terbuang 40-60%

Solusi industrial standard (Moa Bay Cuba, Ramu PNG, Coral Bay Filipina, Taganito):
**Flash Letdown Train 3-stage** dengan steam recovery ke preheater + utility header.

### Parameter SOP per Stage

| Stage | P_in (atm) | P_out (atm) | T_in (°C) | T_out (°C) | Vapor Recovery | Tolerance |
|-------|-----------|-------------|-----------|-----------|----------------|-----------|
| FV1 HP Flash | 47-50 | 11-13 | 245-255 | 190-200 | Recycle ke autoclave preheater | ±1 atm |
| FV2 MP Flash | 11-13 | 2.8-3.2 | 190-200 | 140-150 | MP steam header (utilities) | ±0.5 atm |
| FV3 LP/Atmospheric | 2.8-3.2 | 1.0-1.1 | 140-150 | 100-105 | Condenser / scrubber | ±0.05 atm |

### Komponen per Flash Vessel (real plant)
- **Interstage Letdown Valve** — hydraulic-actuated, dengan bypass handwheel untuk manual startup
- **PSV (Pressure Safety Valve)** — set pressure 110% MAWP, auto-pop jika over-pressure
- **Level Bridle** — 3-tap level transmitter (LT) untuk monitor liquid level
- **Cascade Panel** — display tekanan + suhu + level per stage
- **Sample Port** — isolation valve + sample cooler + bottle collection
- **Vapor Outlet Riser** — pipa ke steam header / condenser

---

## Tujuan Gameplay

Player adalah **operator field** yang melakukan:
1. Manual startup flash train dari shutdown state (semua valve closed)
2. Sequential opening 3 letdown valve dengan pressure interlock
3. Monitoring live cascade panel (tekanan turun bertahap per stage)
4. Sampling PLS (Pregnant Leach Solution) dari 3 stage untuk lab QC
5. Verifikasi hasil lab vs SOP
6. Voice report HT untuk konfirmasi flash train stable

---

## Alur Gameplay (Step-by-Step)

### Phase 1 — DCS Initialization
1. Player tekan **DCS 8** dari control room
2. DCS warning: "Autoclave underflow ON. Flash Train belum standby."
3. HUD: "Pergi ke Flash Train field. Buka Letdown Valve FV1 → FV2 → FV3 berurutan."
4. Fade teleport ke **SpawnPoint_Lvl8_FlashTrain** (depan FV1)

### Phase 2 — FV1 HP Flash Open (manual handwheel)
5. Lampu RED nyala di FV1 cascade panel
6. Player putar **bypass handwheel FV1** clockwise (10 putaran = 3600°)
7. Saat handwheel diputar:
   - Pressure FV1 turun bertahap 47 → 12 atm (live di cascade panel)
   - Slurry pool FV1 mulai naik visible (ghost mesh scale Y)
   - Vapor partikel muncul di top vapor outlet riser
   - Audio: hissing steam release (volume proporsional open%)
8. Saat valve full open + pressure 12 atm:
   - Lampu RED → GREEN
   - Cascade panel: `STAGE 1 STABLE`
   - HUD: "FV1 stable. Lanjut FV2."

### Phase 3 — FV2 MP Flash Open (sequential gating)
9. Player pindah ke FV2 (jalan kaki)
10. **INTERLOCK**: FV2 hanya bisa dibuka kalau FV1 pressure < 13 atm
11. Player putar **bypass handwheel FV2** clockwise (10 putaran)
12. Pressure FV2 turun: 12 → 3 atm
13. Vapor outlet FV2 aktif ke MP steam header
14. Cascade panel: `STAGE 2 STABLE`

### Phase 4 — FV3 LP/Atmospheric Flash Open
15. Player pindah ke FV3
16. Putar **FV3_SteamValve_Handwheel** untuk open atmospheric vent
17. Pressure FV3 turun: 3 → 1.05 atm
18. Cascade panel: `STAGE 3 STABLE`
19. Slurry mulai mengalir ke CCD (Feed_FromFlashVessel_To_CCD1)

### Phase 5 — Sampling PLS 3-Stage
20. HUD: "Ambil 3 sample bottle dari sample port masing-masing FV"
21. Player ambil sample per stage:
    - **FV1**: PLS 195°C (bottle glow merah → cooling → orange → safe)
    - **FV2**: PLS 145°C (bottle glow orange → cooling → kuning → safe)
    - **FV3**: PLS 102°C (bottle glow kuning → langsung safe purple)
22. Setiap sample: tekan tombol sample port → bottle terisi visual
23. Setelah 3 bottle → submit ke lab (tekan L atau tombol di rack)

### Phase 6 — Lab QC Analysis (pop-up canvas)
24. Pop-up canvas muncul di depan player dengan hasil analisis:
```
┌─────────────────────────────────────────────────────────┐
│ ▼ LABORATORY QC ANALYSIS — FLASH TRAIN PLS             │
├─────────────────────────────────────────────────────────┤
│ FV1 HP (195°C): Free acid 18.0 g/L | Ni 5.2 | Co 0.45 │
│ FV2 MP (145°C): Free acid 18.5 g/L | Ni 5.3 | Co 0.46 │
│ FV3 LP (102°C): Free acid 19.0 g/L | Ni 5.4 | Co 0.47 │
├─────────────────────────────────────────────────────────┤
│ VERDICT: Semua dalam SOP ✓                              │
│ Free acid 15-25 g/L | Ni > 4.5 | Fe < 1.5             │
│                                                         │
│              [ ACCEPT & LANJUT ]                        │
└─────────────────────────────────────────────────────────┘
```
25. Player tap ACCEPT → lab logbook submitted

### Phase 7 — Voice Report HT (manual, tahan T)
26. HUD: "Lapor HT: 'Flash train 3-stage stable, sampling complete, slurry siap ke CCD.'"
27. Player tahan T + bicara + lepas → voice report accepted

### Phase 8 — Mission Complete
28. Canvas muncul:
    - **STAY** (lihat steam recovery process berjalan)
    - **KEMBALI KE DCS → Level 9 (CCD)**

---

## Mekanik VR-Native (Kenapa Ini Powerful)

### 1. Multi-Stage Sequential Gating + Pressure Interlock
Player HARUS buka 3 valve **berurutan**. Kalau buka FV2 sebelum FV1 stable → pressure spike → PSV pop (gagal). Ini melatih **disiplin SOP "no rush"** yang membedakan operator terlatih vs ceroboh.

### 2. 10-Turn Gestural Handwheel (Endurance Rotation)
Setiap valve butuh **10 putaran penuh** (3600°) — bukan satu klik. Mensimulasikan handwheel industri gear ratio 50:1 yang operator nyata butuh 30 detik per valve. VR gesture rotation kanan-kiri tangan.

### 3. Live Cascade Panel (Real-Time Feedback)
3 panel cascade dengan:
- Strip indikator RED/YELLOW/GREEN
- Numerical display P (atm) + T (°C) live
- Status text per stage
Player bisa cross-check field gauge vs DCS digital.

### 4. 3-Stage Sample Collection (Temperature-Graded)
Player ambil sample dari 3 stage dengan **suhu berbeda** (195°C, 145°C, 102°C). Bottle visual berubah warna sesuai cooling rate. Mensimulasikan SOP "no direct contact" dengan PLS asam panas.

### 5. Lab QC Decision Pop-up (Data Interpretation)
Setelah 3 sample submitted, lab return analisis dalam 5 detik. Player verify:
- Free acid (H2SO4): 15-25 g/L
- Ni concentration: > 4.5 g/L
- Co: 0.4-0.5 g/L
- Fe residual: < 1.5 g/L
Ini melatih **interpretasi data lab vs SOP** — skill yang biasanya operator outsource ke laboran.

### 6. Steam Recovery Visualization
3 vapor outlet riser dengan partikel putih-kuning. Player bisa lihat secara visual bagaimana steam di-recycle:
- FV1 vapor → autoclave preheater (heat recovery ~25%)
- FV2 vapor → MP steam header (utilities ~15%)
- FV3 vapor → condenser (waste heat ~5%)
Total heat recovery ~40-60% energy.

---

## Kenapa Level 8 Jawab "Kenapa VR"?

Flash letdown train adalah **operasi paling time-critical** di HPAL:
- Salah urutan = PSV pop = shutdown 48 jam
- Terlalu cepat buka = thermal shock = pipe crack
- Terlalu lambat = autoclave back-pressure = agitator trip

Di plant nyata, operator baru **tidak pernah diizinkan** startup flash train sendiri — selalu supervised senior 6+ bulan. VR memungkinkan:
1. Latihan sequential gating tanpa risiko ($2M per PSV pop event)
2. Muscle memory 10-turn handwheel (endurance + precision)
3. Interpretasi cascade panel real-time (skill DCS operator)
4. Sampling asam panas tanpa risiko luka bakar
5. Lab QC decision-making (biasanya hanya laboran yang baca)

---

## Object Names di Scene (auto-find)

### Flash Vessels
- `FV1_*`, `FV2_*`, `FV3_*` (semua mesh sudah ada lengkap)
- `FlashVessel_02` (parent FV2)

### Letdown Handwheels
- `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel` → pos (-22.7, 2.8, 113.2)
- `FV2_To_FV3_InterstageLetdownValve_BypassHandwheel` → pos (-26.6, 2.7, 113.2)
- `FV3_SteamValve_Handwheel` → pos (-67.7, 15.6, 112.8)

### Slurry Ghost (X-Ray pool)
- `FV1_XRay_SlurryPool_Ghost`
- `FV2_XRay_SlurryPool_Ghost` (kalau ada)
- `FV3_XRay_SlurryPool_Ghost` (kalau ada)

### Vapor Outlet
- `FV1_VaporBranch_To_SteamHeader`
- `FV2_ReliefTailPipe_ToVentHeader`
- `FV2_TopVaporOutlet_Riser`

### Cascade Panel (akan dibuat runtime kalau belum ada)
- `FV{1,2,3}_PressureCascadePanel_StatusStrip`
- `FV{1,2,3}_PressureCascadePanel_Text`

### Sample System
- Sample port per FV (akan dibuat runtime)
- Lab rack (akan dibuat runtime)

### Spawn
- `SpawnPoint_Lvl8_FlashTrain` (akan dibuat runtime di depan FV1)

---

## Hotkey Debug Keyboard

| Key | Action |
|-----|--------|
| `1` | Open FV1 letdown (+720°/sec, ~5 sec full) |
| `2` | Open FV2 letdown |
| `3` | Open FV3 atmospheric |
| `Q` | Take FV1 sample |
| `W` | Take FV2 sample |
| `E` | Take FV3 sample |
| `L` | Submit samples to lab |
| `T` | Voice report HT (tahan) |

---

## Lab QC Sample Targets (SOP)

| Parameter | Target | Unit | Keterangan |
|-----------|--------|------|------------|
| Free acid (H2SO4) | 15-25 | g/L | Terlalu rendah = leach incomplete, terlalu tinggi = waste acid |
| Ni concentration | > 4.5 | g/L | Target recovery 95%+ |
| Co concentration | 0.4-0.5 | g/L | By-product valuable |
| Fe residual | < 1.5 | g/L | Lebih = iron precipitation incomplete |
| Temperature exit FV3 | 100-110 | °C | Siap feed CCD (max 110°C) |
| pH | < 1.0 | - | Asam kuat, normal untuk PLS |

---

## Controller File

`Assets/Scripts/Simulation/Level8FlashTrainController.cs` — sudah lengkap dengan:
- Sequential gating + pressure interlock
- 10-turn handwheel rotation (world-axis stable)
- Live cascade panel update
- Slurry ghost animation
- 3-stage sampling system
- Lab QC pop-up canvas
- Mission complete canvas (STAY / KEMBALI KE DCS)
- Voice report gating (WaitForVoiceReport pattern)
- Audio: steam release hissing + alarm

---

## Catatan Implementasi

1. **Handwheel sudah dipersiapkan user** (screenshot: 3 handwheel orange di depan 3 flash vessel putih)
2. **Level 8 di GLM** = `GameLevel.Level8_Monitoring` (nama enum lama, tapi controller baru = FlashTrain)
3. **Transisi dari Level 7**: setelah Mission Complete Level 7, player pilih "KEMBALI KE DCS" → `MulaiLevel(Level8_Monitoring)`
4. **Transisi ke Level 9**: setelah Mission Complete Level 8 → `MulaiLevel(Level9_FlashVessel)` (CCD)


---

## REVISI BESAR (2026-05-29 Part 7) — Feedback User: Grab Bug, Sample Mechanic, Lab Building

### Bug & Permintaan dari User
1. **Grab bug**: saat grab handwheel, "bunderan tengah gauge" malah ikut ketarik ke mana-mana (XRGrabInteractable secara fisik memindahkan objek mengikuti tangan). HARUS: handwheel cuma BERPUTAR di tempat, tidak pindah posisi.
2. **Setelah putar pertama**: tekanan turun + uap panas keluar. Sound effect harus DIKERASKAN. Tambah uap (vapor FX) di lokasi bebas.
3. **Putaran gauge/handwheel diperlambat**: jangan kencang. Cukup **5 putaran** (1800°), pelan.
4. **Sample mechanic SALAH**: sekarang cuma tekan Q/W/E. HARUS: player **mendekat ke 3 tabung flash vessel**, ambil sample pakai **botol/wadah**, dengan **animasi ambil sample** (botol gerak ke port → terisi liquid → berubah warna).
5. **Lingkaran tengah gauge ikut ke mana-mana** = sama dengan bug #1.
6. **Lab uji belum ada**: minta dibangun **gedung lab lengkap + ruangan** via Blender MCP, plus mekanisme uji sample + animasi menarik.

### Solusi Teknis (Plan)
- **Grab fix**: ganti `XRGrabInteractable` → `XRSimpleInteractable` (deteksi select tanpa memindahkan objek). Rotasi 100% dikontrol manual oleh controller (ApplyHandwheelRotation). Pastikan part grouping HANYA hub+ring+spoke handwheel itu sendiri (exclude gauge/needle).
- **Rotasi**: `_handwheelFullOpenDegrees` 3600 → **1800** (5 putaran). `autoSpeed` saat grab diperlambat (mis. 220°/dtk ≈ 8 detik full).
- **Vapor + sound**: vapor FX di top vapor riser tiap FV + saat valve mulai kebuka. `_steamReleaseVolume` dinaikkan, ditambah whoosh saat stage stabil.
- **Sample mechanic baru**: setelah 3 valve stabil → muncul **3 sample station** di dekat tiap flash vessel (sample port + botol). Player dekati → grab botol / tekan → animasi botol naik ke port, terisi liquid (warna per stage: FV1 merah panas → FV2 oranye → FV3 kuning, lalu cooling → biru-teal aman). 3 botol terkumpul → bawa ke lab.
- **Lab building (Blender MCP)**: gedung lab QC — ruangan tertutup, meja analisa, rak sample, layar hasil, pintu. Player masuk → taruh 3 botol di rak analyzer → animasi mesin analisa (spin, lampu, progress bar) → hasil QC muncul di layar → ACCEPT → lapor HT.

### Status Asset Handwheel
- PAKAI handwheel asli `L5_Condensate_Drain_Handwheel (1)/(2)/(3)` di (-54.74, 1.48, 102/105/108). Sudah di-map FV1/FV2/FV3.

### Catatan Blender
- Blender MCP addon HARUS running untuk bangun gedung lab. Kalau belum konek, lab dibangun via ProBuilder/primitive Unity sebagai fallback.


---

## REVISI FINAL Opsi A (2026-05-30 Part 9)

### Keputusan Redesign (research-based)
Berdasarkan research flowsheet HPAL (Nickel Institute, Moa Bay, Coral Bay, Taganito, BC Campus hydrometallurgy textbook): sample PLS untuk lab QC final SEBENARNYA diambil di **OVERFLOW CCD** (Level 9), bukan flash vessel. Flash vessel discharge masih slurry padat+cair, suhu 100-195°C, tidak representatif untuk Ni/Co assay.

### Level 8 SEKARANG (post-redesign)
Fokus: **operasi 3 handwheel valve letdown** (gestural rotation) + monitoring P/T turun + lapor HT.

**Yang dihapus**:
- Sample station fisik di flash vessel (BeginSamplingStations diset tidak terpanggil)
- Lab building (sekarang ada di Level 9)
- Phase Sampling, LabSubmit (skip)

**Flow baru**:
1. DCS 8 → teleport ke handwheel
2. **Putar handwheel FV1 (gestural)**: arahkan tangan VR ke handwheel, putar pergelangan searah jarum jam → handwheel ikut. P 47→12 atm.
3. Putar handwheel FV2 → P 12→3 atm (interlock FV1 stable).
4. Putar handwheel FV3 → P 3→1.05 atm (atmospheric).
5. FV3 stable → otomatis ke phase MenungguLapor.
6. Lapor HT (tahan T): "flash train stable, slurry siap ke CCD".
7. Mission Complete canvas → lanjut ke Level 9 (CCD).

### Mekanik Handwheel Gestural (UpdateHandwheel)
- Track delta yaw tangan player (interactor.forward) saat hover/grab handwheel.
- `dYaw = Mathf.DeltaAngle(yawLast, yawNow)`. Inverse jadi `deltaDeg = -dYaw` supaya CW di sumbu = membuka valve.
- Filter outlier > 35°/frame (anti-glitch).
- Reset baseline (yawValid=false) saat lepas hover/grab.
- Tidak ada auto-rotate. Player bener-bener ngontrol arah & kecepatan.
- Total full open = 1800° (5 putaran).
- Keyboard fallback: tahan 1/2/3 untuk rotasi simulator (360°/dtk).

### Voice Report
- kataKunciVoice: "flash train stable" (alias: "flash letdown selesai", "slurry siap ke ccd")
- laporanVoiceLengkap: "DCS, flash train stable. Tekanan turun bertahap dari empat puluh tujuh menjadi satu koma nol lima atmosfer. Slurry siap dialirkan ke CCD."
