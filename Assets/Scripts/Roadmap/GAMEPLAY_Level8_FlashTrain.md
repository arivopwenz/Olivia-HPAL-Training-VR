# GAMEPLAY Level 8 — Flash Train (3-Stage Pressure Letdown + Sampling)

## Industrial Reference (HPAL Real World)

Setelah autoclave (Level 7), slurry keluar pada **250 °C / 47-50 atm**. Tekanan dan suhu tidak boleh diturunkan langsung ke atmosfer karena:
1. **Steam flash uncontrolled** → ledakan vessel + thermal shock pipa
2. **Silica scaling** → kerak menempel keras di pipe wall jika cooling drop > 40 °C/stage
3. **Heat recovery loss** → uap panas kalau dibuang langsung = energi terbuang besar

Solusi industrial: **Flash Letdown Train 3-stage** (Moa Bay, Ramu, Coral Bay, Taganito):
- **FV1 (HP Flash)**: 47 atm → 12 atm, 250 → 195 °C — vapor di-recycle ke autoclave preheater
- **FV2 (MP Flash)**: 12 atm → 3 atm, 195 → 145 °C — vapor ke MP steam header (utilities)
- **FV3 (LP Flash / Atmospheric)**: 3 atm → 1.05 atm, 145 → 102 °C — vapor ke condenser/scrubber

Setiap stage punya:
- Interstage Letdown Valve (hydraulic-actuated, dengan **bypass handwheel** untuk manual control / startup)
- PSV (Pressure Safety Valve) — set pressure 110% MAWP
- Level Bridle (3-tap level transmitter)
- Sample point untuk analisa lab
- Cascade panel (display tekanan + suhu + level)

## Tujuan Gameplay

Player adalah operator field **dual-role**:
1. Manual startup flash train dari shutdown state (semua valve closed)
2. Operasi steady-state monitoring (tekanan stabil per stage)
3. Sampling cair per stage untuk lab QC
4. Respons upset kalau pressure spike

## Alur Gameplay (Step-by-Step)

### Phase 1 — DCS Initialization (control room)
1. Player tekan **DCS 8** dari control room.
2. DCS warning panel: "Autoclave underflow ON. Flash Train belum standby."
3. HUD: "Pergi ke Flash Train field. Buka Letdown Valve FV1 → FV2 → FV3 secara berurutan."
4. Fade teleport ke **SpawnPoint_Lvl8_FlashTrain** (depan FV1).

### Phase 2 — FV1 HP Flash Open (manual handwheel)
5. Lampu RED nyala di FV1 PSV.
6. Player putar **`FV1_To_FV2_InterstageLetdownValve_BypassHandwheel`** clockwise (10 putaran sampai full open).
7. Saat handwheel diputar:
   - Pressure FV1 turun bertahap dari 47 atm → 12 atm (live di Cascade Panel)
   - Slurry pool FV1 mulai naik visible di X-Ray ghost (`FV1_XRay_SlurryPool_Ghost`)
   - Vapor partikel muncul di top vapor outlet riser
   - Audio: hissing steam release
8. Saat valve full open + pressure 12 atm tercapai:
   - Lampu RED → GREEN
   - Cascade panel FV1 status strip: `STAGE 1 OK`
   - HUD: "FV1 stable. Lanjut FV2."

### Phase 3 — FV2 MP Flash Open (sequential gating)
9. Player pindah ke FV2 (jalan kaki / teleport short).
10. **Interlock check**: Letdown FV2 hanya bisa dibuka kalau FV1 = green (cek pressure < 13 atm).
11. Player putar **`FV2_To_FV3_InterstageLetdownValve_BypassHandwheel`** clockwise.
12. Pressure FV2 turun: 12 atm → 3 atm.
13. Vapor outlet FV2 aktif ke MP steam header.
14. Status strip FV2: `STAGE 2 OK`.

### Phase 4 — FV3 LP/Atmospheric Flash Open
15. Player pindah ke FV3 (Atmospheric Flash).
16. Putar **`FV3_SteamValve_Handwheel`** untuk open atmospheric vent.
17. Pressure FV3 turun: 3 atm → 1.05 atm.
18. Status strip FV3: `STAGE 3 OK`.
19. Slurry mulai mengalir ke CCD (`Feed_FromFlashVessel_To_CCD1_Feedwell_Head`).

### Phase 5 — Sampling 3-Stage (untuk lab QC)
20. Player ambil **3 sample bottles** dari sample port masing-masing FV (FV1 hot 195°C, FV2 mid 145°C, FV3 cool 102°C).
21. Setiap sample:
   - Player tekan tombol pada sample port → bottle terisi (visual: warna purple slurry)
   - Auto-cool indicator: bottle berubah warna (red → orange → yellow → safe)
   - Sample bottle ter-collected di sampling rack.
22. Setelah 3 bottle terisi → submit ke lab via tombol di rack.

### Phase 6 — Lab QC (mini-game baca data)
23. Pop-up canvas dengan 3 hasil lab analysis:
   - **FV1 Sample**: Free acid 18 g/L, Ni 5.2 g/L, Co 0.45 g/L, Fe 0.8 g/L
   - **FV2 Sample**: Free acid 18.5 g/L, Ni 5.3 g/L (sedikit naik karena flash concentration)
   - **FV3 Sample**: Free acid 19 g/L, Ni 5.4 g/L, Temp 102°C (siap CCD)
24. Player verify semua dalam batas SOP (Free acid 15-25 g/L, Ni > 5 g/L, Temp < 110°C).
25. Player tap "ACCEPT" di canvas → ke lab logbook submitted.

### Phase 7 — Voice Report HT
26. HUD: "Lapor HT (tahan T): 'Flash train 3-stage stable, sampling complete, slurry siap ke CCD.'"
27. Voice report accepted → Mission Complete Canvas.
28. Pilihan: **STAY** (lihat proses) atau **KEMBALI KE DCS** → Level 9 (CCD).

## Mekanik VR-Native Powerful

### 1. Multi-Stage Sequential Gating
Player harus buka 3 valve secara berurutan, dengan **interlock pressure-based**. Salah urutan = pressure spike di stage berikutnya. Mensimulasikan SOP "no rush": kalau buka FV2 sebelum FV1 stable, FV2 bisa over-pressure → PSV pop.

### 2. Gestural Handwheel (10-turn full stroke)
Setiap valve butuh **10 putaran clockwise** untuk full open (bukan satu klik). Rotation gesture spasial kanan-kiri tangan VR. Mensimulasikan handwheel industri yang berat (gear ratio 50:1) — operator nyata butuh 30 detik per valve.

### 3. Live Cascade Panel (visual feedback per stage)
3 panel cascade dengan strip indikator (red/yellow/green) + numerical display tekanan & suhu live. Player bisa cross-check antara field gauge mekanis vs DCS digital.

### 4. Sample Bottle Collection (3-stage temperature)
Player ambil sample dari 3 stage dengan **tool grabber heat-resistant**. Bottle visual berubah warna sesuai cooling rate (radiative cooling simulation). Mensimulasikan SOP "no direct contact" dengan slurry asam panas 195°C.

### 5. Lab QC Decision Pop-up
Setelah 3 sample submitted, lab balikkan analisis dalam 5 detik (simulasi). Player verify nilai dalam SOP — ini melatih **interpretasi data lab vs SOP** yang biasanya operator outsource ke laboran.

### 6. Steam Recovery Visualisation
3 vapor outlet riser dengan partikel + animasi flow kuning-putih. Player bisa lihat secara visual bagaimana steam di-recycle ke autoclave preheater (FV1) dan utility header (FV2). Ini menjawab "kenapa flash train" secara visual: heat recovery ~40-60% energy.

## Hotkey Debug Keyboard

- `1`: Open FV1 letdown +10° (skip handwheel grab)
- `2`: Open FV2 letdown +10°
- `3`: Open FV3 atmospheric +10°
- `Q`: Take FV1 sample
- `W`: Take FV2 sample
- `E`: Take FV3 sample
- `L`: Submit samples to lab

## Object Names di Scene (auto-find)

- 3 Flash Vessels: `FV1_*`, `FV2_*`, `FV3_*` (sudah ada lengkap)
- Letdown handwheels: `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel`, `FV2_To_FV3_InterstageLetdownValve_BypassHandwheel`, `FV3_SteamValve_Handwheel`
- Cascade panels: `FV{1,2,3}_PressureCascadePanel_StatusStrip`, `_Text`, `_Backplate`
- Slurry visualization: `FV{1,2,3}_XRay_SlurryPool_Ghost`, `FV{1,2,3}_PurpleSlurry_LowerPool_Visible`
- Vapor outlet: `FV{1,2,3}_TopVaporOutlet_Riser`
- Spawn: `SpawnPoint_Lvl8_FlashTrain` (akan dibuat runtime kalau belum ada)

## Parameter SOP per Stage

| Stage | P_in (atm) | P_out (atm) | T_in (°C) | T_out (°C) | Vapor Flow (t/h) | Tolerance |
|-------|-----------|-------------|-----------|-----------|------------------|-----------|
| FV1   | 47-50     | 11-13       | 245-255   | 190-200   | 25-35            | ±1 atm    |
| FV2   | 11-13     | 2.8-3.2     | 190-200   | 140-150   | 18-25            | ±0.5 atm  |
| FV3   | 2.8-3.2   | 1.0-1.1     | 140-150   | 100-105   | 12-18            | ±0.05 atm |

## Lab QC Sample Targets

- **Free acid (H2SO4)**: 15-25 g/L
- **Ni concentration**: 4.5-5.5 g/L
- **Co concentration**: 0.4-0.5 g/L
- **Fe (residual)**: < 1.5 g/L (lebih = leach incomplete)
- **Temperature exit**: 100-110 °C (siap CCD feed)
