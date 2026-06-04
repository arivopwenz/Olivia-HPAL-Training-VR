# 🎮 ALUR FINAL OLIVIA VR (FIX) — Level 0 sampai Level 14

> Dokumen ini adalah **alur resmi yang sudah fix** untuk OLIVIA VR HPAL Simulator.
> Disusun dari `Olivia_Roadmap.md`, `Olivia_Blueprint_Final.md`, `BreakdownSistem.md`, `OLIVIA_HPAL_VR_SKILL.md`, dan dokumen `GAMEPLAY_Level*.md`.
> **Aturan global:** Di SETIAP level pemain WAJIB lapor via Walkie Talkie (HT), dan SELALU ada balasan suara NPC (DCS/Field) sebagai konfirmasi.

---

## 🔁 Pola Dasar Tiap Level

```
[1] Mulai di zona (DCS Room / Field via "The Hub" loker)
        ↓
[2] Aksi utama (tekan tombol DCS / putar valve / X-Ray / ambil sample / dst.)
        ↓
[3] Sinkronisasi DCS ↔ Field (parameter berubah, animasi/shader ikut)
        ↓
[4] Lapor HT (voice command kata kunci level)
        ↓
[5] Balasan suara NPC (konfirmasi) → Level selesai
        ↓
[6] Fade Out → Teleport ke zona level berikutnya
```

**Catatan peran:** 🔵 = DCS Operator (control room) · 🟠/🟢 = Field Worker (lapangan).

---

## ⚫ LEVEL 0 — Tutorial VR

**Zona:** Tutorial Zone (area lapangan bebas)
**Peran:** Pemula

**Alur:**
1. Pemain belajar 4 kontrol dasar VR: **Jalan**, **Grab** (ambil barang), **Radio/HT**, **HUD**.
2. Selesai tutorial → layar *Fade Out*.
3. Pemain di-teleport otomatis ke depan **Gerbang Gedung Loker** (masuk Level 1).

**Selesai jika:** Semua latihan dasar tuntas.

---

## 🟢 LEVEL 1 — Persiapan APD (The Hub & Safety Zone)

**Zona:** Ruang Loker (The Hub)
**Peran:** 🟠 Operator Lapangan

**Konsep "The Hub":** Loker adalah titik awal level-level operasional. Sistem mengecek SOP level lalu memunculkan APD yang dibutuhkan secara dinamis (mendukung hingga 10 APD: + Harness, Lanyard, Jas Hujan untuk level khusus).

**Alur:**
1. Pemain spawn di ruang loker.
2. Pakai **8 APD wajib** dari rak (`Socket_Scanner_*`): Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, Respirator, Ear Protection, + ambil **Walkie Talkie**.
3. Respirator diletakkan di rak (`Socket_Scanner_RespiratorMask`) — belum dipakai di mulut.
4. APD lengkap → `SafetyGate.cs` siap terbuka.
5. **Lapor HT:** *"APD lengkap."*
6. **Balasan NPC:** *"Copy, pintu Safety Gate terbuka."*
7. Buka Pintu Loker → *Fade Out* → teleport ke DCS Room (Level 2).

**Selesai jika:** 8 APD lengkap + lapor HT diterima.

---

## 🔵 LEVEL 2 — DCS: Persiapan Menghidupkan Mesin

**Zona:** DCS Control Room
**Peran:** 🔵 DCS Operator

**Alur:**
1. Pemain spawn di DCS. Ray/laser controller aktif agar bisa klik tombol DCS.
2. Respirator otomatis pindah ke socket dada (`Socket_Respirator_Baju`), bukan di mulut.
3. Pemain melihat monitor DCS (cek parameter awal — belum ada tombol mesin ditekan).
4. Tekan **Tombol DCS 2**.
5. **Lapor HT:** *"Field, siapkan area Crusher."*
6. **Balasan NPC:** *"Siap, menuju area Crusher."*
7. *Fade Out* → teleport ke field Crusher/Slurry (Level 3).

**Selesai jika:** Tombol DCS 2 ditekan + lapor HT diterima.

---

## 🟠 LEVEL 3 — Lapangan: Ore Masuk ke Slurry Tank

**Zona:** Area Crusher & Slurry Tank
**Peran:** 🟠 Field Worker
**Phase enum:** `Level3Phase`

**Alur:**
1. Pemain mulai dari DCS → tekan **Tombol DCS 3** → **lapor HT awal**.
2. Teleport ke field slurry/crusher.
3. Ambil **respirator dari dada** dan pakai (mekanik chest-grab via `TorsoChestAnchor`).
4. **X-Ray View** Crusher & Slurry Tank: lihat ore berjalan di belt (`L2_V2_Wide_Inclined_Rubber_Ore_Belt`) lalu jatuh ke slurry tank.
5. Air/liquid masuk → liquid slurry naik dari bawah sampai **75%** (`Level3OreSlurryController`, ±18 detik).
6. Saat 75% → status siap lapor akhir. Agitator berputar setelah laporan akhir diterima.
7. **Lapor HT akhir:** *"Ore masuk ke Slurry Tank, cairan 75%."*
8. **Balasan NPC:** *"Copy, standby untuk aktivasi Slurry Pump."*
9. Menu pilihan muncul: **Lanjut** atau **Lihat Proses** (`LevelTransitionChoicePanel`).

**Selesai jika:** Slurry 75% + lapor HT akhir diterima.

---

## 🔵 LEVEL 4 — DCS: Aktifkan Slurry Pump + Atur Flow Rate

**Zona:** DCS Control Room → Field Pump
**Peran:** 🔵 DCS Operator → 🟠 observasi field
**Phase enum:** `Level4Phase`

**Alur:**
1. Spawn di DCS → **Tombol DCS 4 (Slurry Pump)** berkedip/glowing → tekan.
2. Atur **Flow Rate** via tombol `Btn_FlowPlus` / `Btn_FlowMinus` di monitor mini hingga **450 m³/h** (auto-complete di 450 ±10, TANPA tombol konfirmasi).
3. **Sinkronisasi:** kecepatan shader aliran slurry di pipa 100% sinkron dengan angka DCS.
4. **Lapor HT awal:** *"Slurry Pump aktif, flow rate diset 450 meter kubik per jam."*
5. Teleport ke pump/field → lihat cairan tersedot dari slurry tank, mengalir di pipa ke Pre-Heater, level tank turun sampai habis.
6. Setelah aliran sampai Pre-Heater → **Lapor HT akhir:** *"Cairan sudah di Pre-Heater."*
7. **Balasan NPC:** *"Copy, memantau aliran ke Pre-heater."*

**Catatan:** Steam Pre-Heater TIDAK muncul di Level 4 (steam hanya dari mekanik Level 5).
**Selesai jika:** Flow 450 m³/h + slurry sampai Pre-Heater + lapor HT akhir.

---

## 🟠 LEVEL 5 — Lapangan: Buka Katup Steam & Pre-Heater

**Zona:** DCS → Field Pre-Heater
**Peran:** 🔵 → 🟠
**Controller:** `Level5SteamValveController.cs`

**Alur:**
1. Dari DCS → tekan **Tombol DCS 5 (Pre-Heater)** → **lapor HT awal:** *"aktifkan pre-heater."*
2. Teleport ke mesin Pre-Heater.
3. **Grab handwheel** `RealSteamValve_Pivot_Lvl5` (XRGrabInteractable).
4. Putar valve searah jarum jam dengan tangan VR (4 putaran penuh = 1440° = 100% open).
5. Mesh handwheel & jarum gauge (`Gauge_Needle`) ikut berputar; suhu naik **25 → 200 °C**.
6. **Steam FX** (uap putih) + audio mendesir muncul perlahan SETELAH valve mulai diputar (proporsional bukaan).
7. Suhu ≥ **180 °C** → `NotifyLevel5PreheaterReady`.
8. **Lapor HT akhir:** *"Katup steam terbuka, suhu naik."*
9. **Balasan NPC:** *"Copy, bersiap untuk injeksi asam."*

**Debug:** `R` = buka valve, `F` = tutup.
**Selesai jika:** Suhu ≥ 180 °C + lapor HT akhir.

---

## 🔵 LEVEL 6 — Acid Injection (PALING KOMPLEKS — 6 Fase)

**Zona:** Field Pre-Heater → DCS → Acid Skid (field)
**Peran:** 🟠 → 🔵 → 🟠
**Controller:** `Level6AcidInjectionController.cs`
**Target SOP:** Dosis Asam **350 kg/ton**, Stroke **70%**, **pH 1.0**

**Alur 6 fase:**
1. Tekan **Tombol DCS 6**.
2. **Lapor HT:** *"outlet preheater dibuka"* → teleport ke handwheel Pre-Heater.
3. **Putar handwheel outlet Pre-Heater** → cairan ungu mengalir di pipa (`Pipe_PreheaterToAutoclave`, 4 segmen transparan) + audio flow → Autoclave terisi (`AnimateAutoclaveFill`).
4. **Lapor HT:** *"slurry masuk autoclave"* → teleport balik ke DCS.
5. **DCS Acid Setup** (panel `L6_DCS_AcidControlPanel_Runtime`, 6 tombol):
   - `Btn_AcidPlus/Minus` → ±10 kg/ton (target **350**)
   - `Btn_AcidStrokePlus/Minus` → ±5% stroke (target **70%**)
   - `Btn_AcidTankSelect` → swap Tank A/B
   - `Btn_AcidArm` → ARM toggle
   - pH turun **5.0 → 1.0**, beaker hologram berubah warna (hijau→kuning→oranye→merah).
   - Ratio + stroke + ARM lengkap → `NotifyLevel6DcsAcidRatioReady` → teleport ke acid skid.
6. **Acid Skid (field)** — 2 tombol mushroom:
   - `L6_AcidSkid_BtnLocalStart_Runtime` (hijau): tekan → pump nyala.
   - `L6_AcidSkid_BtnLeakOk_Runtime` (biru): tekan setelah 8 detik inspeksi kebocoran → cairan amber naik di calibration column → Autoclave penuh → `NotifyLevel6AcidInjectionComplete`.
7. **Lapor HT:** *"acid aktif, rasio 350 kg per ton, pH 1.0."*
8. **Balasan NPC:** *"Copy, aman masuk Autoclave."*

**Debug:** `+/-`, `[/]`, `T`, `A`, `G` (local start), `H` (leak ok).
**Selesai jika:** Ratio 340–360 + pH ≤ 1.1 + acid skid lengkap + lapor HT.

---

## 🟠 LEVEL 7 — Lapangan: Inspeksi Autoclave + X-Ray (SHOWCASE #1)

**Zona:** Platform inspeksi Autoclave
**Peran:** 🟠 Field Worker
**Controller:** `Level7AutoclaveController.cs`
**Target SOP:** Tekanan **45–50 atm**, Suhu **250–255 °C**, Agitator **60 RPM**

**6 Mekanik VR-Native:**
1. **X-Ray Vision** (`X`) + **3 layer** (`C`): Slurry Flow / Heat Map / Scale Buildup. Shell jadi transparan biru, inner fluid ungu & agitator 60 RPM terlihat.
2. **Scale Mark** (`M`) — tandai 3 spot scale buildup di kompartemen.
3. **Cluster Gauge Reading + Logbook** (`L`) — baca 3 gauge analog (pressure/temperature/RPM), submit logbook.
4. **Sample Port** (`V` toggle valve, `B` ambil sample) — valve auto-close setelah sampling.
5. **Safety Drill** (`S` 4×) — konfirmasi PSV → ESD → Quench → Exit.
6. **Voice Report.**

**Notify:** `NotifyLevel7XrayActivated`, `…ScaleMarked`, `…GaugesLogged`, `…SampleTaken`, `…SafetyDrillDone` → semua lengkap = `_level7AutoclaveInspected = true`.

**Lapor HT:** *"Autoclave normal, suhu 250 derajat, tekanan 50 atm, agitator 60 RPM."*
**Balasan NPC:** *"Copy, parameter sesuai SOP, lanjut monitoring ketat."*
**Selesai jika:** 5 inspeksi lengkap + lapor HT → teleport DCS (Level 8).

---

## 🔵 LEVEL 8 — DCS: Flash Train 3-Stage Letdown + Monitoring

**Zona:** DCS → Field Flash Train
**Peran:** 🔵 → 🟠
**Controller:** `Level8MonitoringController.cs`
**Referensi:** `GAMEPLAY_Level8_FlashTrain.md`

**Alur:**
1. Tekan **Tombol DCS 8** → warning "Flash Train belum standby" → teleport ke `SpawnPoint_Lvl8_FlashTrain`.
2. **FV1 (HP Flash):** putar `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel` (10 putaran) → tekanan **47 → 12 atm**, suhu 250→195 °C. Lampu RED→GREEN, `STAGE 1 OK`.
3. **FV2 (MP Flash):** interlock (FV1 harus green) → putar `FV2_To_FV3_…BypassHandwheel` → **12 → 3 atm**, 195→145 °C. `STAGE 2 OK`.
4. **FV3 (LP/Atmospheric):** putar `FV3_SteamValve_Handwheel` → **3 → 1.05 atm**, 145→102 °C. `STAGE 3 OK`. Slurry mengalir ke CCD.
5. **Sampling 3-stage:** ambil 3 sample bottle (FV1 195°C, FV2 145°C, FV3 102°C); bottle berubah warna sesuai pendinginan; collected di rack.
6. **Lab QC mini-game:** submit ke lab → verifikasi (Free acid 15–25 g/L, Ni > 5 g/L, Temp < 110°C) → tap **ACCEPT**.
7. Monitoring: stabilkan parameter dalam SOP (`ParameterAutoklaveSesuaiSOP()`). Jika RPM drop 40 / tekanan naik 53 → koreksi via tombol [+]/[-].
8. **Lapor HT:** *"Flash train 3-stage stable, sampling complete, parameter stabil di angka SOP."*
9. **Balasan NPC:** *"Copy, proses optimal, siap ke CCD."*

**Debug:** `1/2/3` buka FV1/2/3, `Q/W/E` ambil sample, `L` submit lab.
**Selesai jika:** 3 stage stabil + sampling + lab QC + monitoring stabil + lapor HT.

---

## 🟠 LEVEL 9 — Lapangan: CCD Activation + PLS Sampling + Lab QC

**Zona:** DCS → Field CCD → Lab QC
**Peran:** 🔵 → 🟠
**Controller:** `Level10CCDController.cs` (display "Level 9 - CCD", tombol DCS 9)
**Referensi:** `GAMEPLAY_Level9_CCD_PLSSampling.md`

**Alur:**
1. Tekan **Tombol DCS 9** → teleport ke field CCD.
2. **Observasi CCD** (~14 detik): rake arms muter (4 RPM), slurry settle, overflow naik jernih, particle FX di interface padat-cair → `NotifyLevel10CCDComplete`.
3. **Ambil 3 sample PLS** (proximity-based, dekati < 2.8 m):
   - Th-1 (PLS pekat, ungu) · Th-3 (PLS mid, ungu pucat) · Th-5 (wash, biru-abu).
   - Botol terisi animasi (progress 1/3 → 3/3).
4. **Lab QC** (gedung `CCDLab.fbx`): tekan `L` → animasi spectrometer + titration (5 detik) → pop-up hasil (Ni/Co/Fe/free acid per stage) → **ACCEPT** (`NotifyLevel10SamplePLSAccepted`).
5. **Lapor HT:** *"CCD aktif, PLS lulus QC."*
6. **Balasan NPC:** *"Copy, menuju area presipitasi."*

**Debug:** `L` submit lab, `T` voice report.
**Selesai jika:** CCD stabil + 3 sample + lab QC ACCEPT + lapor HT.

---

## 🟠 LEVEL 10 — Lapangan/DCS: MHP Precipitation

**Zona:** DCS → Field MHP Tank
**Peran:** 🔵 → 🟠
**Controller:** `Level11MHPController.cs` (display "Level 10 - MHP")

**Alur:**
1. Tekan **Tombol DCS 10** → teleport ke MHP plant.
2. **X-Ray MHP Tank**: lihat endapan hijau (presipitasi MgO).
3. Grab `MgO_Sack` → tuang ke `MHP_NeutralizationTank` → pH naik ke ~5.5, warna berubah hijau.
4. Ambil **botol sampel** dari `Sample_Port_Handwheel`.
5. **Lapor HT:** *"MHP terbentuk, produk normal."*
6. **Balasan NPC:** *"Copy, proses produksi utama selesai."*

**Selesai jika:** Presipitasi terbentuk + sampel diambil + lapor HT.

---

## 🟢 LEVEL 11 — DCS: Mengalirkan Limbah ke Tailing (Discharge)

**Zona:** DCS Control Room
**Peran:** 🔵 DCS Operator
**Controller:** `Level12TailingFilterController.cs` (display "Level 11 - Tailing Discharge")

**Alur:**
1. Tekan **Tombol DCS 11 (Tailing Discharge)** → buka `Letdown_Discharge_Valve` ke tangki netralisasi.
2. Sisa limbah asam dialirkan ke area pengolahan tailing.
3. **Lapor HT:** *"Limbah dialirkan ke area Tailing."*
4. **Balasan NPC:** *"Copy, siap melakukan netralisasi."*

**Selesai jika:** Tombol discharge ditekan + lapor HT.

> ℹ️ **Catatan penomoran:** Blueprint awal memetakan Tailing Discharge di Level 12 dan Dry Stack di Level 13. Implementasi controller menggunakan urutan flowsheet (MHP → Discharge → Dry Stack → Emergency). Yang penting alur logikanya: **discharge dulu, baru netralisasi & dry stack**.

---

## 🟢 LEVEL 12 / 13 — Lapangan: Tailing Waste Management & Dry Stack (SHOWCASE #2)

**Zona:** Area Tailing (suasana B3, signage bahaya)
**Peran:** 🟢 Field Worker
**Controller:** `Level13DryStackController.cs` (area `Level13_DryStack_Field`, spawn `SpawnPoint_Lvl13`)
**Fokus:** Edukasi pengolahan limbah B3 nikel agar aman lingkungan.
**Target SOP:** pH **8.0–9.0**, Moisture cake **< 25%**

**Alur:**
1. Saat masuk `Level13_TailingWaste` → teleport ke `SpawnPoint_Lvl13`.
2. Cek indikator **pH Tailing** (awal asam < 3.0). Tekan DCS/local **Button 13**.
3. **Grab karung/ember Limestone (kapur)** → tuang ke tangki netralisasi → pH naik **7.5 → 8.5**. Limestone dust FX. Setelah aman, dosing limestone stop.
4. **Filter Press aktif** (16 press plate merapat) → filtrate keluar → **X-Ray Filter Press**: pisahkan cairan dari lumpur → moisture cake turun **34% → 22%**.
5. Cake bergerak via conveyor (8 cake block) ke **Dry Stack Storage** (6 pile) → safe cover aktif → beacon lingkungan berubah **hijau** → `NotifyLevel13DryStackComplete`.
6. **Lapor HT:** *"DCS, netralisasi berhasil. pH delapan koma lima dan tailing aman di dry stack."*
7. **Balasan NPC:** *"Copy, lingkungan aman."*

**Selesai jika:** pH 8.5 + filter press jalan + cake di dry stack + beacon hijau + lapor HT.

---

## 🔴 LEVEL 14 — DARURAT: K3 Kebocoran & ESD (REALISTIS, NO EXPLOSION)

**Zona:** DCS Control Room
**Peran:** 🔵 DCS Operator
**Controller:** `Level14EmergencyController.cs`

**Pemicu:** Tiba-tiba saat operasi normal — **kebocoran pipa H2SO4** atau **steam leak (overpressure)**.

**Situasi:**
- Suara mendesis keras, asap putih/kuning menyebar di lantai pabrik.
- Alarm K3 berbunyi, lampu merah berkedip.
- Countdown ~45 detik. Safety field worker terancam jika tanpa respirator.

**Quest Darurat (SOP K3):**
1. **[DCS]** Acknowledge alarm kebocoran.
2. **[DCS]** Ambil HT → **Lapor:** *"EMERGENCY! Kebocoran asam di Sektor 2! Semua personel evakuasi!"*
3. **Balasan NPC (panik):** *"Copy, kami evakuasi sekarang!"*
4. **[DCS]** (opsional sesuai skenario) tutup isolation valve manual.
5. **[DCS]** Cari & tekan **tombol ESD (merah)** → semua valve asam & steam menutup otomatis, pompa mati.

**Ending BERHASIL:** Kebocoran berhenti, uap menghilang. *"SISTEM AMAN. Evakuasi berhasil, tidak ada korban."*
**Ending GAGAL:** Telat tekan ESD → shutdown dengan damage tinggi. *"KEGAGALAN SISTEM. Paparan kimia melewati batas aman."* (tanpa gore/kematian).

**Selesai jika:** Lapor emergency + tekan ESD tepat waktu → sistem aman.

---

## 🏁 PENUTUP — Skor & Sertifikat

Setelah Level 14, sistem skor dihitung per level:

```
Skor Level = (Kecepatan × 0.25) + (Ketepatan Aksi/Flow × 0.25)
           + (Laporan Walkie Talkie × 0.25) + (Urutan SOP K3 × 0.25)

Nilai Akhir = Rata-rata 15 level (Level 0–14)
Syarat Lulus: ≥ 70%
Output: Sertifikat K3 Virtual Operator HPAL OLIVIA
```

---

## 📋 Ringkasan Alur Cepat (Flowsheet)

| Lvl | Zona | Aksi Inti | Kata Kunci HT |
|-----|------|-----------|---------------|
| 0 | Tutorial | Jalan, Grab, Radio, HUD | — |
| 1 | Loker | Pakai 8 APD + HT | "APD lengkap" |
| 2 | DCS | Tombol DCS 2 | "siapkan area" |
| 3 | Field | X-Ray ore → slurry 75% | "ore masuk", "cairan 75%" |
| 4 | DCS→Field | Pump + flow 450 m³/h | "slurry pump aktif" |
| 5 | Field | Putar steam valve, suhu 180°C | "katup steam terbuka" |
| 6 | Field→DCS→Field | Slurry ke autoclave + acid 350 kg/ton, pH 1.0 | "acid aktif" |
| 7 | Field | X-Ray autoclave + 5 inspeksi (250°C/50atm/60RPM) | "autoclave normal" |
| 8 | DCS→Field | Flash train 3-stage + sampling + lab QC | "parameter stabil" |
| 9 | Field | CCD aktif + 3 sample PLS + lab QC | "CCD aktif" |
| 10 | Field | MHP presipitasi + ambil sampel | "MHP terbentuk" |
| 11 | DCS | Tailing discharge | "limbah dialirkan" |
| 12/13 | Field | Netralisasi pH 8.5 + filter press + dry stack | "tailing aman" |
| 14 | DCS | EMERGENCY: lapor evakuasi + tekan ESD | "emergency", "evakuasi" |

---

*Alur ini adalah versi FIX. Jika ada perubahan mekanik, update dokumen ini + `GAMEPLAY_LevelN_*.md` terkait agar tetap satu sumber kebenaran.*
