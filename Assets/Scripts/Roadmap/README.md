# OLIVIA VR — HPAL Nickel Plant Operator Training Simulator

Simulator pelatihan operator pabrik nikel HPAL berbasis Virtual Reality. Setiap level merepresentasikan satu tahap pada flowsheet HPAL nyata, mulai dari Crusher hingga Emergency Response, dengan fokus edukasi proses, keselamatan kerja (K3), dan kepatuhan SOP.

---

## Daftar Isi

1. [Tentang Project](#tentang-project)
2. [Tech Stack](#tech-stack)
3. [Konsep Gameplay](#konsep-gameplay)
4. [Peta Dunia](#peta-dunia)
5. [Arsitektur Sistem](#arsitektur-sistem)
6. [Breakdown Per Level (1–13)](#breakdown-per-level-113)
7. [Parameter SOP Industri](#parameter-sop-industri)
8. [Catatan Penomoran Level](#catatan-penomoran-level)
9. [Struktur Folder](#struktur-folder)
10. [Cara Menjalankan](#cara-menjalankan)
11. [Kontrol dan Debug](#kontrol-dan-debug)
12. [Konvensi Pengembangan](#konvensi-pengembangan)

---

## Tentang Project

| | |
|---|---|
| Nama | OLIVIA VR — Operasi dan Pelatihan VR HPAL Nikel |
| Developer | Ari Prabowo |
| Tipe | Industrial Training Simulator (VR) |
| Domain | HPAL (High Pressure Acid Leaching) — pengolahan bijih nikel |
| Tujuan | Lomba Nasional / prototype pelatihan industri |
| Repository | github.com/arivopwenz/Olivia-HPAL-Training-VR |
| Showcase | Level 7 (Autoclave X-Ray) dan Level 12 (Dry Stack Tailing) |

OLIVIA adalah training simulator yang mengajarkan:

- Proses industri HPAL nikel secara visual dan interaktif.
- Keselamatan kerja (K3) dan kepatuhan terhadap SOP.
- Peran ganda Operator DCS (ruang kontrol) dan Operator Lapangan (field).
- Penanganan limbah B3 (tailing) yang aman bagi lingkungan.
- Respons darurat (emergency shutdown).

Target akhir: pemain menyelesaikan seluruh level, memahami alur HPAL secara penuh, dan layak menerima Sertifikat K3 Virtual Operator HPAL.

---

## Tech Stack

| Komponen | Detail |
|----------|--------|
| Engine | Unity 6 + Universal Render Pipeline (URP) |
| VR Framework | XR Interaction Toolkit 3.4.1 |
| Hand Tracking | XR Hands 1.7.3 |
| Input | Input System 1.18 (legacy + new) |
| Bahasa Kode | C# (identifier dan komentar: English) |
| Bahasa In-Game | Indonesia (HUD, voice NPC) |
| 3D Modeling | Blender (model mesin diekspor sebagai FBX ke folder Assets/Art) |
| Scene Utama | Assets/Scenes/Level1.unity (seluruh level berada dalam satu scene) |

---

## Konsep Gameplay

Pilar utama OLIVIA:

1. Sistem Level-Based — setiap level adalah satu tahap pada flowsheet HPAL nyata.
2. Dual-Role — pemain berpindah peran antara DCS Control Room dan Lapangan tiap level.
3. Walkie Talkie wajib — setiap level diakhiri laporan via HT (Push To Talk) dengan balasan suara NPC.
4. X-Ray / Invisible View — melihat proses internal mesin (slurry, agitator, reaksi kimia).
5. Sinkronisasi DCS dan Lapangan — parameter di DCS mengontrol animasi serta shader mesin secara real-time.
6. Interaksi VR-Native — gestural handwheel, grab botol sampel, dosing reagen, dan penuangan limestone.
7. Skor dan Sertifikat K3 — penilaian per level dengan syarat lulus minimal 70%.

---

## Peta Dunia

Dunia game terbagi menjadi dua zona utama:

| Zona | Nama | Isi Utama |
|------|------|-----------|
| A | DCS Control Room | Monitor utama, 14 tombol sistem, parameter live (suhu, tekanan, pH, flow, RPM), tombol ESD, dan walkie talkie. Pusat kendali operator DCS. |
| B | Lapangan (Plant Field) | Seluruh mesin proses: Crusher, Slurry Tank, Slurry Pump, Pre-Heater, Autoclave, Flash Vessel, CCD, MHP, Tailing, Filter Press, Dry Stack, beserta pipa, valve, platform, tangga, dan catwalk. |

Pemain berpindah antara kedua zona melalui sistem teleport setiap pergantian fase atau level.

---

## Arsitektur Sistem

```
GameLevelManager (Singleton — pusat state machine seluruh level)
    Menyimpan : enum CurrentLevel, data level, target SOP, parameter (suhu/tekanan/pH/RPM/flow)
    Event     : OnLevelStarted, OnDCSButtonPressed, OnVoiceReportAccepted, OnLevelComplete
                OnLevel10CCDStartAuthorized untuk gate laporan HT awal CCD sebelum animasi field
    Notify*   : memajukan flag penyelesaian per level

    PhaseManager                    Sub-state APD (8 item), pin respirator ke socket dada
    WalkieTalkieManager             Voice recognizer + PTT + balasan suara NPC
    PlayerHUD                       Quest checklist [OK]/[ ], fade transition, notifikasi
    LevelTeleportManager            Teleport XR Origin antar zona (anti snap-back)
    DCSMonitorUI                    14 tombol sinkronisasi + parameter + alarm + ESD
    UniversalTaskMarker             Panah 3D + wireframe pada target tugas aktif

    Komponen Reusable
    GesturalHandwheel               Putar valve mengikuti arah tangan VR. Dipakai sebagai mekanik
                                    level di Level 5 (steam valve) dan Level 7 (autoclave inlet valve).
                                    Level 8 (flash train) memakai pola putaran gestural yang sama,
                                    diimplementasikan langsung di Level8FlashTrainController.
    ProcessPipeFlowAnimator         Animasi gelombang aliran slurry di dalam pipa
    SlurryConditioningTankRunner    Agitator + solids tersuspensi + panel instrumen live

    Level{N}Controller (Assets/Scripts/Simulation/)
        Mendengarkan GameLevelManager.OnLevelStarted
        Menjalankan state machine fase masing-masing
        Memanggil Notify* untuk memajukan flag level
```

Pola umum tiap controller:

```csharp
private void OnEnable()  => GameLevelManager.OnLevelStarted += OnLevelStarted;
private void OnDisable() => GameLevelManager.OnLevelStarted -= OnLevelStarted;

private void OnLevelStarted(GameLevel lvl)
{
    if (lvl == GameLevel.LevelX) { AutoFindReferences(); /* aktifkan */ }
    else { /* nonaktifkan */ }
}
```

---

## Breakdown Per Level (1–13)

### Level 1 — APD Safety
Zona: Ruang Loker. Peran: Operator Lapangan.
- Pakai 8 APD wajib dari rak `Socket_Scanner_*`: Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, Respirator, Ear Protection, dan Walkie Talkie.
- `SafetyGate` terbuka otomatis saat APD lengkap.
- Lapor HT "APD lengkap", lalu teleport ke DCS Control Room.

### Level 2 — DCS Preparation
Zona: DCS Control Room. Peran: DCS Operator.
- Ray controller aktif, respirator otomatis pindah ke socket dada.
- Tekan tombol DCS 2, cek parameter awal.
- Lapor HT "siapkan area Crusher", lalu teleport ke lapangan.

### Level 3 — Ore dan Slurry
Zona: Crusher dan Slurry Tank. Peran: Field Worker.
- DCS 3, lapor awal, teleport ke lapangan, ambil respirator dari dada.
- Ore Crusher: belt menyambung crusher ke slurry tank tanpa gap, sekuens startup (sirine, hentakan mundur, eskalator naik), animasi jaw crush, flywheel berputar, dan dust FX. Ore tercacah muncul dari titik discharge lalu diangkut menuju tank.
- Slurry Tank: tangki open-top dengan agitator berputar mengaduk, solids tersuspensi, serta panel instrumen live (density, level, RPM).
- Slurry terisi 0 persen hingga 75 persen, X-Ray view aktif.
- Lapor HT akhir "ore masuk, cairan 75 persen".

### Level 4 — Slurry Pump
Zona: DCS dan Lapangan (pump). Peran: DCS Operator.
- DCS 4, atur Flow Rate menuju 450 m3/h melalui tombol `Btn_FlowPlus` / `Btn_FlowMinus` (selesai otomatis pada 450 plus minus 10).
- Pipa slurry tank ke pump lalu ke pre-heater menampilkan animasi aliran yang hanya aktif saat pump berjalan.
- Level slurry tank menurun seiring aliran.
- Lapor HT "slurry pump aktif", lalu "cairan sudah di pre-heater".

### Level 5 — Steam Valve dan Pre-Heater
Zona: DCS dan Lapangan (pre-heater). Peran: DCS ke Field.
- DCS 5, lapor awal, teleport ke handwheel pre-heater.
- Gestural Handwheel: putar mengikuti arah tangan VR dengan model handwheel industrial. Suhu naik 25 ke 200 derajat C, gauge needle bergerak proporsional, steam FX dan audio mendesir mengikuti persentase bukaan.
- Pada suhu di atas 180 derajat C, lapor HT "katup steam terbuka".

### Level 6 — Acid Injection (enam fase)
Zona: Lapangan, DCS, lalu Acid Skid. Target: 350 kg/ton, pH 1.0.
1. Tekan DCS 6.
2. Lapor "outlet pre-heater dibuka", teleport ke handwheel.
3. Putar handwheel outlet, cairan mengalir menuju autoclave.
4. Lapor "slurry masuk autoclave", kembali ke DCS.
5. DCS Acid Panel (6 tombol): ratio plus minus 10, stroke plus minus 5 persen, swap tank A/B, dan ARM. pH turun 5.0 ke 1.0.
6. Acid Skid lapangan: tombol LOCAL START (hijau) dan LEAK OK (biru), calibration column terisi.
7. Lapor HT "acid aktif".

### Level 7 — Autoclave Inspection (Showcase)
Zona: Platform Autoclave. Target: 250 derajat C, 50 atm, 60 RPM.
- DCS 7, teleport ke valve, putar handwheel underflow (lampu merah berubah hijau).
- Teleport ke top deck. X-Ray Vision (X): shell autoclave menjadi transparan biru, slurry naik 0 ke 100 persen menggunakan shader `Olivia/L7SlurryFill` (depth gradient, fresnel, ripple), agitator terlihat berputar di dalam.
- Acid drop dari nozzle disertai splash FX.
- Safety drill berjalan otomatis dalam empat langkah.
- Sekuens akhir: sirine, engine ignition, lalu agitator ramp-up 0 ke 60 RPM.
- Lapor HT, lalu Mission Complete (pilihan STAY atau KEMBALI KE DCS).

### Level 8 — Flash Train dan Letdown (tiga stage)
Zona: Lapangan (flash vessel). Target: 47 ke 12 ke 3 ke 1.05 atm.
- DCS 8, teleport ke depan handwheel.
- Tiga gestural handwheel (FV1, FV2, FV3) dengan interlock tekanan: putar mengikuti tangan (XRSimpleInteractable, objek tidak ikut tertarik), lima putaran per valve.
- Pressure cascade panel, vapor FX, dan steam audio aktif.
- Lapor HT "flash train stable, slurry siap ke CCD".

### Level 9 — CCD (Counter-Current Decantation)
Zona: Lapangan (CCD) dan Lab QC. Target: wash efficiency di atas 95 persen.
- DCS 9 ditekan di ruang kontrol, lalu player teleport ke `SpawnPoint_Lvl9`.
- Player wajib lapor HT awal terlebih dahulu: "CCD siap, alirkan cairan dari flash vessel". Laporan ini menjadi gate sebelum proses visual CCD dimulai.
- Setelah laporan awal diterima, slurry/PLS dari flash vessel masuk CCD: settling zone naik dari dasar thickener, kemudian `Rake_Arm_Root`, `Rake_Arm_Root.001`, dan `Rake_Arm_Root.002` berputar perlahan.
- Visual pemisahan dibuat bertahap: partikel coklat turun ke dasar sebagai underflow/residu, sementara lapisan overflow PLS menjadi lebih jernih.
- Setelah CCD stabil, player mengambil 3 sample PLS overflow pada station Th-1, Th-3, dan Th-5. Spawn/teleport sample diarahkan ke depan station supaya player tidak membelakangi sample.
- Animasi fill bottle lapangan dihapus; sample langsung masuk inventory saat diambil agar gameplay tidak terasa palsu atau terlalu cepat.
- Lab QC Building (`L9_LabBuilding`, permanen di scene) memakai `L9_LabInteractiveStations_Runtime` di atas meja lab. Runtime script mempertahankan posisi manual root ini dan hanya memastikan station/interaksi lab tersedia.
- Lab QC interaktif: sample login/chain-of-custody, filtrasi 0.45 um/TSS, pH dan free acid, ICP-OES metals, lalu validasi CCD. Bottle lab memiliki liquid visual, analyzer rotor beranimasi, dan screen lab menampilkan progress serta hasil.
- Hasil QC muncul sebagai world-space panel di sisi player dan mengikuti arah player, bukan menutup pandangan depan. Tombol ACCEPT memakai `XRSimpleInteractable` + collider agar bisa diklik ray/poke VR, dengan fallback Enter.
- Setelah ACCEPT, player masih wajib lapor HT final: "CCD aktif, PLS lulus QC". Baru setelah laporan final diterima level lanjut ke precipitation/MHP.
- GameObject field control lama `L9_CCD_FieldControl_FeedValve` dan `L9_CCD_FieldControl_RakeUnderflowValve` sudah dihapus permanen; mekanik handwheel/gauge Level 9 tidak lagi dipakai.

### Level 10 — MHP Precipitation (interaktif penuh)
Zona: Lapangan (MHP) dan Warehouse. Target: pH 7.0 sampai 7.5, kualitas 92 persen.
- DCS 10, teleport ke lapangan, operator station dengan tombol dosing muncul.
- Tiga tahap dosing (tombol atau Space): limestone CaCO3 (pH ke 3.5), kapur Ca(OH)2 (pH ke 5.0), MgO (pH ke 7.5, MHP terbentuk). Panel info live tiap tahap menampilkan reagen, formula, dan reaksi.
- Sampling berbasis proximity, lalu Lab QC pop-up (Ni 41 persen, Co 3.6 persen, recovery 94 persen), kemudian ACCEPT.
- Stage gudang: teleport ke warehouse untuk bagging dan dispatch produk MHP ke refinery (animasi smooth pengisian delapan export bag dengan weigh counter).
- Lapor HT "MHP terbentuk".

### Level 11 — Tailing dan Filter Press (interaktif penuh)
Zona: Lapangan (tailing). Target: pH 8.0, moisture di bawah 25 persen.
- DCS 11, teleport ke lapangan, operator station muncul.
- Tahap 1 Netralisasi: limestone menaikkan pH 2.3 ke 8.0 (jarum pH bergerak, beacon hijau), disertai limestone pour stream.
- Tahap 2 Filter Press: 16 plate merapat, moisture cake turun 60 ke 22 persen, cake muncul progresif di conveyor.
- Inspeksi cake, lalu Compliance QC (pH baku mutu, moisture, filtrat jernih), kemudian ACCEPT.
- Lapor HT "limbah dialirkan".

### Level 12 — Dry Stack Tailing (Showcase)
Zona: Dry Stack Facility (DSTF). Target: timbunan unsaturated dan stabil.
- DCS 12, teleport ke DSTF.
- Tahap 1 Stacking: cake dipadatkan dalam terraced lift di atas geomembrane liner, disertai dust FX.
- Tahap 2 Closure: safe cover (rehab grass cap), piezometer aman, polishing pond menjadi jernih.
- Inspeksi, lalu Compliance QC (geomembrane intact, empat piezometer, rembesan menuju WWTP, closure dan revegetasi), kemudian ACCEPT.
- Lapor HT "dry stack aman, pH 8.5".

### Level 13 — Emergency K3 (ESD)
Zona: DCS Control Room. Skenario realistis tanpa ledakan.
- Pemicu mendadak: kebocoran H2SO4 atau steam leak, ditandai alarm, asap, lampu merah, dan countdown.
- Acknowledge alarm, lapor HT evakuasi, lalu tekan tombol ESD. Seluruh valve asam dan steam menutup, pompa mati.
- Berhasil: sistem aman dan evakuasi sukses. Gagal: ESD terlambat ditekan sehingga shutdown dengan damage.

Status: Level 1–12 sudah berfungsi dan terverifikasi (playable). Level 13 dalam tahap design.

---

## Parameter SOP Industri

| Parameter | Target | Level |
|-----------|--------|-------|
| Flow Rate Slurry | 450 m3/h | 4 |
| Suhu Pre-Heater | 180–200 derajat C | 5 |
| Dosis Asam (H2SO4) | 350 kg/ton menuju pH 1.0 | 6 |
| Suhu Autoclave | 250–255 derajat C | 7 |
| Tekanan Autoclave | 45–50 atm | 7 |
| RPM Agitator | 60 RPM | 7 |
| Flash Train (tiga stage) | 47 ke 12 ke 3 ke 1.05 atm | 8 |
| CCD Wash Efficiency | di atas 95 persen | 9 |
| MHP Precipitation | pH 7.0–7.5, kualitas 92 persen | 10 |
| Netralisasi Tailing | pH 8.0–9.0 | 11 |
| Moisture Tailing Cake | di bawah 25 persen | 11–12 |

Sumber riset: Nickel Institute, serta studi kasus HPAL Moa Bay, Coral Bay, dan Taganito.

---

## Catatan Penomoran Level

Nomor display tidak sama dengan enum internal. Level 9 lama (Flash Vessel) telah digabung ke Level 8 sehingga nomor display bergeser. Enum internal sengaja tidak diubah demi menjaga serialisasi scene.

| Display | Enum Internal | Controller | Tombol DCS |
|---------|---------------|------------|------------|
| Level 8 | Level8_Monitoring | Level8FlashTrainController | 8 |
| Level 9 | Level10_CCD | Level10CCDController | 9 |
| Level 10 | Level11_MHP | Level11MHPController | 10 |
| Level 11 | Level12_TailingDischarge | Level12TailingFilterController | 11 |
| Level 12 | Level13_TailingWaste | Level13DryStackController | 12 |
| Level 13 | Level14_Emergency | — | ESD |

---

## Struktur Folder

```
Assets/
├── Scenes/
│   └── Level1.unity                    Scene utama (seluruh level)
│
├── Scripts/
│   ├── Simulation/                     Controller per level dan sistem inti
│   │   ├── GameLevelManager.cs
│   │   ├── PhaseManager.cs
│   │   ├── WalkieTalkieManager.cs
│   │   ├── GesturalHandwheel.cs
│   │   ├── ProcessPipeFlowAnimator.cs
│   │   ├── SlurryConditioningTankRunner.cs
│   │   ├── Level3OreSlurryController.cs
│   │   ├── Level4SlurryPumpController.cs
│   │   ├── Level5SteamValveController.cs
│   │   ├── Level6AcidInjectionController.cs
│   │   ├── Level7AutoclaveController.cs
│   │   ├── Level8FlashTrainController.cs
│   │   ├── Level10CCDController.cs
│   │   ├── Level11MHPController.cs
│   │   ├── Level12TailingFilterController.cs
│   │   └── Level13DryStackController.cs
│   ├── UI/                             PlayerHUD, UniversalTaskMarker, DCS panels
│   ├── System/                         Teleport, interactor recovery
│   └── Roadmap/                        Dokumentasi design dan README
│
├── Shaders/
│   └── L7SlurryFill.shader             Custom water shader untuk autoclave
│
├── Materials/
│   └── Color Utama/                    Slurry_Fill, Pipe_Transparent, DCS Machine, Industrial_*
│
└── Art/                                Model 3D (Blender ke FBX) per mesin
    ├── DCSControlRoom/                 DCS Control Room, panel DCS, support rig
    ├── Level1APDStationBlender/        Stasiun/rak APD (Level 1)
    ├── APDRoom/                        Ruang loker APD
    ├── Level2OreCrusherBlender/        Ore Crusher dan belt conveyor (Level 3)
    ├── Level3SlurryTankBlender/        Slurry Tank
    ├── Level3SlurryWaterTankBlender/   Slurry Water Tank
    ├── Level3SlurryWaterTanksBlender/  Set Slurry Water Tanks
    ├── Level3_WaterSteamFX/            Efek air dan uap (Level 3)
    ├── SlurryToPreheaterPipe/          Pipa Slurry Tank ke Pre-Heater
    ├── Level4SlurryPumpBlender/        Slurry Pump (Level 4)
    ├── Level5PreHeaterBlender/         Pre-Heater (Level 5)
    ├── Level5Handwheel/                Handwheel/valve Pre-Heater (Level 5)
    ├── PreheaterAutoclavePipe/         Pipa Pre-Heater ke Autoclave
    ├── AcidInjectionSystemRedesign/    Sistem injeksi asam (Level 6)
    ├── Level7AutoclaveBlender/         Autoclave reaktor (Level 7)
    ├── AutoclaveToFlashPipe/           Pipa Autoclave ke Flash Vessel
    ├── FlashVesselTrainRedesign/       Flash Vessel Train tiga stage (Level 8)
    ├── FlashCCDIndustrialBlender/      Area Flash dan CCD (industrial)
    ├── FlashCCDIndustrialUVRedesign/   UV dan tekstur Flash/CCD
    ├── CCDThickenerRedesign/           CCD Thickener (Level 9)
    ├── CCDIndustrialUVRedesign/        CCD industrial dengan UV mapping
    ├── CCDConnectionPipes/             Pipa koneksi antar thickener CCD
    ├── CCDProcessPipesBlender/         Pipa proses CCD (PLS overflow dan underflow)
    ├── Lab/                            Gedung Lab QC, CCDLab.fbx (Level 9)
    ├── Level11PurificationMHPUVRedesign/   Purification dan MHP precipitation (Level 10)
    ├── Level13TailingDryStackRedesign/     Tailing dan Dry Stack (Level 11–12)
    ├── Level13DryStackBlender/             Dry Stack Facility (Level 12)
    ├── Level13DryStackIndustrialUVRedesign/    Dry Stack industrial dengan UV
    ├── Level13DryStackStorageAreaUVRedesign/   Area penyimpanan Dry Stack
    ├── OreTanggaIndustrialAccessUVRedesign/    Tangga akses industri area Ore
    ├── GlobalIndustrialStairsCatwalksUVRedesign/   Tangga dan catwalk global
    ├── TaskHintArrowBlender/           Model panah petunjuk tugas
    └── _BlenderScripts/                Script pendukung pembuatan model
```

---

## Cara Menjalankan

1. Buka project di Unity 6 pada path `C:\Users\mp2dz\Olivia`.
2. Buka scene `Assets/Scenes/Level1.unity`.
3. Tekan Play menggunakan VR headset atau XR Device Simulator.
4. Untuk melompat ke level tertentu, klik kanan komponen `GameLevelManager` di Inspector, lalu pilih `DEBUG: Skip ke Level N`.

Untuk membuat atau mengubah model mesin: model dibuat di Blender, kemudian diekspor sebagai berkas FBX ke folder `Assets/Art/<nama-mesin>/`. Unity meng-import otomatis. Material di-assign kembali di Unity menggunakan URP/Lit karena node material Blender tidak terbawa pada FBX.

---

## Kontrol dan Debug

| Tombol | Aksi | Level |
|--------|------|-------|
| R / F | Buka / tutup valve | 5 |
| 1 / 2 / 3 | Putar handwheel FV1 / FV2 / FV3 | 8 |
| G | Grab sampel terdekat | 9 |
| L | Submit sampel ke Lab QC | 9 |
| Enter | ACCEPT hasil lab atau compliance | 9–12 |
| Space / 1 | Dosing atau aksi tahap | 10, 11, 12 |
| T (tahan) | Voice report HT (Push To Talk) | semua |
| X | Toggle X-Ray | 7 |

Debug penting pada `GameLevelManager`:
- `DEBUG: Skip ke Level 9 (CCD)` masuk ke DCS 9/area CCD dan memakai spawn `SpawnPoint_Lvl9`.
- `DEBUG: Level 9 - Masuk Lab QC PLS` melompat langsung ke fase Lab QC PLS untuk testing station lab dan panel hasil.
- `DEBUG: Skip ke Level 10 (MHP)`, `DEBUG: Skip ke Level 11 (Tailing Filter Press)`, `DEBUG: Skip ke Level 12 (Dry Stack)`, dan `DEBUG: Skip ke Level 13 (Emergency K3)` tersedia untuk validasi level lanjutan.

---

## Konvensi Pengembangan

Bahasa dan Gaya
- Kode, identifier, dan komentar menggunakan English. Teks in-game (HUD, voice NPC) menggunakan Bahasa Indonesia kasual.
- Greybox-first: utamakan fungsional terlebih dahulu, polish visual menyusul.

VR dan Interaksi
- Teleport wajib menggunakan `XROrigin.MoveCameraToWorldLocation` dan `MatchOriginUpCameraForward`. Jangan men-set `transform.position` langsung karena menyebabkan player snap-back.
- Tombol VR menggunakan `XRSimpleInteractable` dan `BoxCollider` dengan registrasi collider eksplisit, ditambah keyboard fallback. `UnityEngine.UI.Button` saja tidak dapat diklik oleh ray XR pada world-space canvas.
- World-space canvas hasil lab atau compliance sebaiknya diposisikan di samping player dan mengikuti arah kepala/player supaya tidak menutup view utama dan tetap bisa diklik dengan ray VR.
- Handwheel pada Level 5 dan Level 7 menggunakan komponen `GesturalHandwheel` agar berputar mengikuti tangan tanpa objek ikut tertarik. Level 8 memakai pola yang sama secara inline di controller-nya.

Audio
- Audio dibuat prosedural via `AudioClip.Create` dengan sample generation (sine, noise, envelope). Tidak menggunakan berkas audio eksternal.

Material dan Visual
- Material URP/Lit di-copy dari template `DCS Machine.mat` via `AssetDatabase.CopyAsset` agar shader keyword tetap terjaga.
- Slurry menggunakan `Slurry_Fill.mat`, pipa transparan menggunakan `Pipe_Transparent.mat`.
- TextMesh runtime memerlukan font built-in (`LegacyRuntime.ttf`) dan assign `font.material`.

3D Model
- Model dibuat di Blender, lalu diekspor sebagai FBX ke `Assets/Art/`.
- Material di-assign kembali di Unity karena node Blender tidak terbawa pada FBX.

Scene dan State
- Simpan scene (`MarkSceneDirty` dan `SaveScene`) hanya saat tidak dalam play mode.
- Elemen interaktif yang dibangun runtime tidak perlu disimpan; objek scene permanen wajib disimpan.
- Nilai serialized pada scene menimpa default kode, sehingga keduanya perlu di-set.

Workflow
- Setelah mengedit script: refresh/compile, periksa console error, perbaiki, lalu lakukan play-test.
- Pesan commit menggunakan Bahasa Indonesia, dan push dilakukan manual saat diminta.

---

OLIVIA VR — belajar mengoperasikan pabrik HPAL nikel dengan aman, dari Crusher hingga Dry Stack, tanpa risiko nyata.
