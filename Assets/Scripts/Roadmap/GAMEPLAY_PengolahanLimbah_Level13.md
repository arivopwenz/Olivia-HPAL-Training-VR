# 🟢 GAMEPLAY PENGOLAHAN LIMBAH NIKEL — LEVEL 13
## Panduan Lengkap untuk Development

> **Level:** 13 — Tailing & Waste Management
> **Lokasi:** Area Tailing (outdoor, suasana industrial, label B3 di mana-mana)
> **Peran Pemain:** Field Worker
> **Durasi Target:** 8-12 menit gameplay
> **Tujuan Edukasi:** Pemain memahami dan mempraktikkan pengolahan limbah B3 HPAL

---

# OVERVIEW SINGKAT

Pemain baru saja menyelesaikan Level 12 (DCS mengalirkan limbah ke tailing).
Sekarang pemain turun ke lapangan untuk **mengolah limbah** supaya aman.

**Alur besar:**
```
Limbah asam masuk → Netralisasi (tambah kapur) → Filter Press (peras) → 
Dry Stack (tumpuk aman) → Cek air buangan di WWTP → Selesai
```

---

# SCENE LAYOUT (Area yang Harus Dibuat)

```
┌─────────────────────────────────────────────────────────────────┐
│                        AREA TAILING                              │
│                                                                  │
│  ┌──────────┐     ┌──────────┐     ┌──────────┐                │
│  │NEUTRALI- │────▶│ FILTER   │────▶│ CONVEYOR │──▶ DRY STACK   │
│  │ZATION    │     │ PRESS    │     │ BELT     │                 │
│  │TANK      │     │          │     │          │                 │
│  └────┬─────┘     └────┬─────┘     └──────────┘                │
│       │                 │                                        │
│       │ (air)           │ (air perasan)                          │
│       ▼                 ▼                                        │
│  ┌──────────────────────────────┐                               │
│  │     WWTP MINI PANEL          │                               │
│  │  (monitoring air buangan)    │                               │
│  └──────────────────────────────┘                               │
│                                                                  │
│  [SPAWN POINT]  ← Pemain muncul di sini dari teleport           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Props/Asset yang dibutuhkan:**
- 1x Tangki Netralisasi (silinder vertikal, ada pengaduk, pipa masuk/keluar)
- 1x Filter Press (rangka baja + plate berjajar)
- 1x Conveyor Belt pendek (dari filter press ke dry stack)
- 1x Area Dry Stack (tanah terbuka + tumpukan cake)
- 1x Panel WWTP (layar kecil + beberapa indikator)
- Karung/ember kapur (interactable, bisa di-grab)
- Label B3, safety sign, railing
- Pipa-pipa penghubung antar mesin

---

# ALUR GAMEPLAY DETAIL (STEP BY STEP)

---

## STEP 0: SPAWN & BRIEFING
**Durasi:** ~30 detik

```
TRIGGER: Pemain di-teleport dari Level 12 ke area tailing

APA YANG TERJADI:
├── Pemain muncul di depan area tailing
├── Voice Over (NPC DCS via speaker): 
│   "Limbah dari proses HPAL sudah dialirkan ke area tailing.
│    Lakukan netralisasi dan pastikan limbah aman sebelum disimpan."
├── UI Objective muncul di HUD:
│   "📋 TUGAS: Olah limbah B3 agar aman untuk penyimpanan"
└── Pemain bisa mulai jalan ke Tangki Netralisasi
```

---

## STEP 1: CEK pH LIMBAH MENTAH
**Durasi:** ~30 detik
**Lokasi:** Di depan Tangki Netralisasi

```
APA YANG PEMAIN LIHAT:
├── Tangki besar berisi cairan coklat keruh (slurry tailing)
├── Display digital di samping tangki menunjukkan:
│   ┌─────────────────────┐
│   │  pH: 2.3            │  ← MERAH (BAHAYA)
│   │  Status: ASAM       │
│   │  Target: 8.0 - 9.0  │
│   └─────────────────────┘
├── Label B3 besar: "⚠️ LIMBAH BERBAHAYA — KOROSIF"
└── Di samping ada rak berisi KARUNG KAPUR (Limestone/CaCO₃)

APA YANG PEMAIN LAKUKAN:
├── Dekati display → baca pH (2.3 = sangat asam = bahaya)
├── UI Hint muncul: "pH terlalu rendah. Tambahkan kapur untuk netralisasi."
└── Pemain mengerti: harus tambah kapur supaya pH naik

KONDISI LANJUT: Pemain bergerak ke rak kapur
```

---

## STEP 2: TAMBAH KAPUR (NETRALISASI)
**Durasi:** ~2 menit (ini INTERAKSI UTAMA)
**Lokasi:** Samping Tangki Netralisasi

```
APA YANG PEMAIN LAKUKAN:

OPSI A (Grab & Pour — lebih immersive):
├── Grab karung kapur dari rak (VR hand grab)
├── Bawa ke mulut tangki / hopper di atas tangki
├── Tuang (tilt/flip karung)
├── Animasi: bubuk putih jatuh ke dalam tangki
├── pH di display mulai NAIK perlahan:
│   2.3 → 3.1 → 4.5 → 5.8 → 6.9 → 7.5 → 8.2 → 8.7
├── Warna display berubah: MERAH → KUNING → HIJAU
└── Pemain perlu tuang 3-4 karung sampai pH mencapai 8.0-9.0

OPSI B (Valve/Tombol — lebih simpel):
├── Tekan tombol "DOSING ON" di panel samping tangki
├── Animasi: pipa kapur otomatis mengalir ke tangki
├── Pemain monitor pH naik di display
├── Tekan "DOSING OFF" saat pH sudah 8.0-9.0
└── Jika terlambat stop → pH kelewat tinggi (>10) → skor turun

FEEDBACK VISUAL:
├── Cairan di tangki berubah warna: coklat keruh → coklat muda → abu-abu
├── Pengaduk (agitator) di dalam tangki berputar (terlihat dari atas)
├── Suara: gemericik + motor pengaduk
└── Partikel endapan terlihat mengendap di dasar (jika pakai X-Ray)

KONDISI GAGAL:
├── pH < 8.0 saat pemain lanjut → "Netralisasi belum selesai!"
├── pH > 10.0 → "Terlalu banyak kapur! Pemborosan reagent." (skor -10)
└── Tidak melakukan apa-apa 60 detik → hint muncul lagi

KONDISI BERHASIL:
├── pH antara 8.0 - 9.0
├── Display berubah HIJAU ✓
├── Audio: *ding* + "Netralisasi berhasil"
├── UI Objective update: "✓ Netralisasi — Lanjut ke Filter Press"
└── Pemain bergerak ke Filter Press
```

---

## STEP 3: OPERASIKAN FILTER PRESS
**Durasi:** ~2 menit
**Lokasi:** Di depan mesin Filter Press

```
APA YANG PEMAIN LIHAT:
├── Mesin besar: rangka baja + deretan plate (lempeng) berjajar
├── Panel kontrol kecil di samping dengan 3 tombol:
│   [CLOSE]  [START]  [OPEN]
├── Gauge tekanan (0-16 Bar)
├── Pipa inlet (dari tangki netralisasi)
├── Tray penampung air (filtrate) di bawah
└── Conveyor belt di bawah plate (untuk cake jatuh)

ALUR INTERAKSI:

── FASE A: TUTUP PLATE ──
├── Pemain tekan tombol [CLOSE]
├── Animasi: plate bergerak merapat satu sama lain (hidrolik)
├── Suara: *psshh* (hidrolik) + *clank* (plate ketemu)
├── Indikator: "PLATES CLOSED ✓" menyala hijau
└── Tombol [START] sekarang bisa ditekan

── FASE B: MULAI FILTRASI ──
├── Pemain tekan tombol [START]
├── Animasi: pompa menyala, slurry dipompa masuk ke plate
├── Suara: motor pompa + cairan mengalir
├── Gauge tekanan naik perlahan: 0 → 4 → 8 → 12 Bar
├── Air (filtrate) mulai menetes dari bawah plate ke tray
│   (visual: air keruh → makin jernih seiring waktu)
├── MONITORING: Pemain harus perhatikan gauge
│   ├── 8-12 Bar = NORMAL (hijau)
│   ├── 12-14 Bar = PERHATIAN (kuning)
│   └── >14 Bar = BAHAYA (merah) → harus stop!
├── Setelah ~30 detik, air berhenti menetes = filtrasi selesai
├── Indikator: "FILTRATION COMPLETE ✓"
└── Tombol [OPEN] sekarang bisa ditekan

── FASE C: BUKA PLATE & KELUARKAN CAKE ──
├── Pemain tekan tombol [OPEN]
├── Animasi: plate bergerak membuka satu per satu
├── CAKE (lempeng padatan coklat/abu-abu) jatuh ke conveyor
├── Suara: *thud thud thud* (cake jatuh)
├── Conveyor otomatis berjalan membawa cake ke arah Dry Stack
├── Indikator: "CYCLE COMPLETE ✓"
└── UI Objective update: "✓ Filter Press — Lanjut ke Dry Stack"

KONDISI GAGAL:
├── Tekan [START] sebelum [CLOSE] → "Plate belum tertutup!"
├── Tekanan >14 Bar dan tidak di-stop → "OVERLOAD! Cloth robek!" (skor -15)
└── Tekan [OPEN] sebelum filtrasi selesai → "Filtrasi belum selesai!"

X-RAY VIEW (opsional, jika pemain aktifkan):
├── Terlihat di dalam plate: slurry masuk → air menembus kain → padatan tertahan
└── Visual edukatif: pemain paham mekanisme penyaringan
```

---

## STEP 4: INSPEKSI DRY STACK
**Durasi:** ~1.5 menit
**Lokasi:** Area outdoor — tumpukan tanah/cake

```
APA YANG PEMAIN LIHAT:
├── Area terbuka dengan tumpukan cake (warna abu-abu/coklat)
├── Conveyor belt mengantarkan cake baru dari filter press
├── Alat monitoring:
│   ├── PIEZOMETER (tiang kecil di tanah — ukur water table)
│   └── MONITORING WELL (pipa vertikal — cek air tanah)
├── Bulldozer/compactor (statis, dekorasi)
├── Geomembrane terlihat di tepi (lapisan hitam di bawah tumpukan)
└── Sign: "DRY STACK TAILINGS FACILITY — AREA TERBATAS"

APA YANG PEMAIN LAKUKAN:
├── Jalan ke PIEZOMETER
│   ├── Interact → display muncul:
│   │   ┌─────────────────────────┐
│   │   │ Water Table: 3.2m       │ ← HIJAU (aman, >2m)
│   │   │ Status: NORMAL           │
│   │   │ Batas Aman: > 2.0m      │
│   │   └─────────────────────────┘
│   └── Pemain konfirmasi: water table aman
│
├── Jalan ke MONITORING WELL
│   ├── Interact → display muncul:
│   │   ┌─────────────────────────┐
│   │   │ pH Air Tanah: 7.1       │ ← HIJAU
│   │   │ Cr⁶⁺: 0.02 mg/L        │ ← HIJAU (batas: 0.05)
│   │   │ Status: TIDAK TERCEMAR  │
│   │   └─────────────────────────┘
│   └── Pemain konfirmasi: air tanah tidak tercemar
│
├── Cek visual tumpukan:
│   ├── Ketebalan lapisan terlihat normal
│   ├── Tidak ada genangan air di permukaan
│   └── Drainase terlihat mengalir (tidak tersumbat)
│
└── UI Objective update: "✓ Dry Stack Normal — Lanjut ke WWTP Check"

KONDISI LANJUT: Pemain bergerak ke Panel WWTP
```

---

## STEP 5: CEK PANEL WWTP (AIR BUANGAN)
**Durasi:** ~1 menit
**Lokasi:** Panel monitoring kecil di ujung area

```
APA YANG PEMAIN LIHAT:
├── Panel/layar kecil menampilkan parameter air buangan:
│   ┌─────────────────────────────────────┐
│   │  WWTP DISCHARGE MONITORING          │
│   ├─────────────────────────────────────┤
│   │  pH          : 7.8    [✓ HIJAU]     │
│   │  TSS         : 45 mg/L [✓ HIJAU]    │
│   │  Cr⁶⁺        : 0.03 mg/L [✓ HIJAU]  │
│   │  Ni          : 0.04 mg/L [✓ HIJAU]  │
│   │  Flow Rate   : 120 m³/h             │
│   ├─────────────────────────────────────┤
│   │  STATUS: COMPLIANT ✓                │
│   │  DISCHARGE: APPROVED                │
│   └─────────────────────────────────────┘
└── Tombol [APPROVE DISCHARGE] berkedip

APA YANG PEMAIN LAKUKAN:
├── Baca semua parameter → pastikan semua HIJAU
├── Tekan tombol [APPROVE DISCHARGE]
├── Audio: "Discharge approved. Air buangan memenuhi baku mutu."
└── UI Objective update: "✓ WWTP Compliant"

CATATAN: Jika ada parameter MERAH (skenario alternatif/harder mode):
├── Pemain TIDAK BOLEH tekan approve
├── Harus tekan [RECIRCULATE] → air kembali ke treatment
└── Jika tetap approve padahal merah → "PELANGGARAN REGULASI!" (skor -20)
```

---

## STEP 6: LAPOR VIA WALKIE TALKIE & SELESAI
**Durasi:** ~30 detik

```
APA YANG PEMAIN LAKUKAN:
├── Angkat Walkie Talkie
├── Tekan tombol PTT (Push to Talk)
├── Audio pemain (atau text prompt):
│   "Netralisasi berhasil, pH 8.5. Filter Press selesai.
│    Dry Stack normal. Air buangan memenuhi baku mutu.
│    Tailing aman di Dry Stack."
│
├── Balasan audio DCS (NPC):
│   "Copy. Lingkungan aman. Proses pengolahan limbah selesai.
│    Bagus, lanjut ke standby position."
│
├── 🎉 LEVEL 13 COMPLETE!
├── Layar fade out
└── Transisi ke Level 14 (Emergency) atau Rapor Akhir
```

---

# SISTEM SCORING LEVEL 13

```
┌─────────────────────────────────────────────────────────────┐
│              RAPOR LEVEL 13 — PENGOLAHAN LIMBAH              │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. NETRALISASI (25 poin)                                    │
│     ├── pH tepat 8.0-9.0         → +25                      │
│     ├── pH 7.5-8.0 atau 9.0-9.5  → +15 (kurang presisi)    │
│     ├── pH >10 (kebanyakan kapur) → +5  (pemborosan)        │
│     └── Tidak netralisasi         → 0                       │
│                                                              │
│  2. FILTER PRESS (25 poin)                                   │
│     ├── Urutan benar (Close→Start→Open) → +15               │
│     ├── Tekanan terjaga <14 Bar         → +5                │
│     ├── Tidak ada error/overload        → +5                │
│     └── Urutan salah / overload         → -10               │
│                                                              │
│  3. INSPEKSI DRY STACK (25 poin)                             │
│     ├── Cek piezometer              → +10                   │
│     ├── Cek monitoring well          → +10                  │
│     ├── Cek visual drainase          → +5                   │
│     └── Skip inspeksi               → 0                     │
│                                                              │
│  4. WWTP & KEPATUHAN (25 poin)                               │
│     ├── Baca semua parameter         → +10                  │
│     ├── Approve saat semua hijau     → +10                  │
│     ├── Lapor HT dengan benar        → +5                   │
│     ├── Approve saat ada merah       → -20 (PELANGGARAN)    │
│     └── Recirculate saat ada merah   → +15 (BONUS)          │
│                                                              │
├─────────────────────────────────────────────────────────────┤
│  TOTAL MAKSIMAL: 100 poin                                    │
│  LULUS: ≥ 70 poin                                            │
│  PREDIKAT:                                                   │
│     90-100 = ⭐ EXCELLENT (Operator Ahli)                    │
│     70-89  = ✓  COMPETENT (Lulus)                            │
│     50-69  = ⚠️  NEEDS IMPROVEMENT (Perlu latihan ulang)     │
│     <50    = ✗  FAILED (Tidak lulus)                         │
└─────────────────────────────────────────────────────────────┘
```

---

# VARIABEL & STATE YANG PERLU DI-TRACK (untuk Programmer)

```csharp
// === STATE VARIABLES ===

// Netralisasi
float currentPH = 2.3f;           // mulai dari 2.3 (asam)
float targetPH_min = 8.0f;
float targetPH_max = 9.0f;
int limeBagsUsed = 0;             // berapa karung kapur dipakai
bool neutralizationComplete = false;

// Filter Press
enum FilterPressState { Idle, Closed, Running, Done, Open }
FilterPressState fpState = Idle;
float fpPressure = 0f;            // 0-16 Bar
bool fpOverloaded = false;
bool fpCycleComplete = false;

// Dry Stack Inspection
bool piezometerChecked = false;
bool monitoringWellChecked = false;
bool drainageChecked = false;
float waterTableLevel = 3.2f;     // meter (aman jika >2.0)

// WWTP
bool allParametersGreen = true;
bool dischargeApproved = false;
bool illegalDischarge = false;     // true jika approve saat merah

// Walkie Talkie
bool finalReportDone = false;

// Scoring
int scoreNeutralization = 0;
int scoreFilterPress = 0;
int scoreDryStack = 0;
int scoreWWTP = 0;
int totalScore = 0;
```

---

# UI ELEMENTS YANG DIBUTUHKAN

| UI Element | Lokasi | Fungsi |
|-----------|--------|--------|
| pH Display | Samping tangki netralisasi | Tampilkan angka pH + warna |
| Pressure Gauge | Samping filter press | Tampilkan tekanan 0-16 Bar |
| Filter Press Status | Panel filter press | Idle/Closed/Running/Done |
| Piezometer Display | Di tiang piezometer | Water table level |
| Monitoring Well Display | Di pipa monitoring | pH + Cr⁶⁺ air tanah |
| WWTP Panel | Panel di ujung area | Semua parameter air buangan |
| HUD Objective | Atas layar pemain | Task saat ini |
| HUD Score | Pojok kanan | Skor berjalan (opsional) |

---

# AUDIO YANG DIBUTUHKAN

| ID | Audio | Trigger |
|----|-------|---------|
| A1 | Briefing NPC: "Limbah sudah dialirkan..." | Spawn awal |
| A2 | Suara tuang kapur (bubuk jatuh) | Saat tuang karung |
| A3 | Motor pengaduk tangki | Loop saat netralisasi |
| A4 | *Ding* + "Netralisasi berhasil" | pH mencapai target |
| A5 | Hidrolik filter press *psshh* | Saat plate close |
| A6 | Motor pompa filter press | Saat filtrasi jalan |
| A7 | Cake jatuh *thud thud* | Saat plate open |
| A8 | "Discharge approved" | Saat approve WWTP |
| A9 | Balasan DCS: "Copy, lingkungan aman..." | Saat lapor HT |
| A10 | Alarm/warning buzzer | Jika ada error |

---

# ANIMASI YANG DIBUTUHKAN

| ID | Animasi | Object |
|----|---------|--------|
| AN1 | Agitator berputar di tangki | Tangki Netralisasi |
| AN2 | Cairan berubah warna (coklat → abu) | Cairan di tangki |
| AN3 | pH display naik perlahan | Display digital |
| AN4 | Plate filter press merapat | Filter Press |
| AN5 | Air menetes dari plate | Filter Press |
| AN6 | Gauge tekanan naik | Gauge analog |
| AN7 | Plate membuka satu-satu | Filter Press |
| AN8 | Cake jatuh ke conveyor | Filter Press → Conveyor |
| AN9 | Conveyor belt bergerak | Conveyor |
| AN10 | Kapur dituang (partikel putih) | Karung → Tangki |

---

# FLOWCHART LOGIC (untuk Programmer)

```
START LEVEL 13
    │
    ▼
[Spawn + Briefing Audio]
    │
    ▼
[Pemain di depan Tangki Netralisasi]
    │
    ▼
┌─── CEK pH ───┐
│ pH < 8.0?    │──── YA ──── ▶ [Perlu tambah kapur]
│              │                      │
└──────────────┘                      ▼
                              [Pemain tuang kapur / tekan dosing]
                                      │
                                      ▼
                              ┌─── pH CHECK ───┐
                              │ pH 8.0-9.0?    │── YA ──▶ ✓ LANJUT
                              │                │
                              │ pH > 10.0?     │── YA ──▶ ⚠️ Pemborosan (skor -10)
                              │                │           tapi tetap lanjut
                              │ pH < 8.0?      │── YA ──▶ 🔄 Tambah lagi
                              └────────────────┘
                                      │
                                      ▼
                    [UI: "✓ Netralisasi — Lanjut ke Filter Press"]
                                      │
                                      ▼
                    ┌─── FILTER PRESS ───┐
                    │                    │
                    │ State: IDLE        │
                    │                    │
                    │ [CLOSE] ditekan?   │── YA ──▶ State = CLOSED
                    │                    │                │
                    │                    │                ▼
                    │                    │    [START] ditekan?
                    │                    │         │
                    │                    │         ▼ YA
                    │                    │    State = RUNNING
                    │                    │    Pressure naik 0→12 Bar
                    │                    │    Timer 30 detik
                    │                    │         │
                    │                    │         ▼
                    │                    │    ┌── Pressure >14? ──┐
                    │                    │    │ YA → OVERLOAD!    │
                    │                    │    │ TIDAK → lanjut    │
                    │                    │    └──────────────────┘
                    │                    │         │
                    │                    │         ▼ (30 detik selesai)
                    │                    │    State = DONE
                    │                    │         │
                    │                    │         ▼
                    │                    │    [OPEN] ditekan?
                    │                    │         │
                    │                    │         ▼ YA
                    │                    │    State = OPEN
                    │                    │    Animasi cake jatuh
                    │                    │    Conveyor jalan
                    └────────────────────┘
                                      │
                                      ▼
                    [UI: "✓ Filter Press — Lanjut ke Dry Stack"]
                                      │
                                      ▼
                    ┌─── DRY STACK INSPEKSI ───┐
                    │                          │
                    │ Piezometer di-interact?   │── ✓ (+10 poin)
                    │ Monitoring Well interact? │── ✓ (+10 poin)
                    │ Drainase di-cek?          │── ✓ (+5 poin)
                    │                          │
                    │ Minimal 2 dari 3 dicek    │── LANJUT
                    └──────────────────────────┘
                                      │
                                      ▼
                    ┌─── WWTP PANEL ───┐
                    │                  │
                    │ Semua hijau?     │── YA ──▶ [APPROVE] bisa ditekan
                    │                  │                │
                    │ Ada merah?       │── YA ──▶ [RECIRCULATE] harus ditekan
                    │                  │           (jika APPROVE → pelanggaran!)
                    └──────────────────┘
                                      │
                                      ▼
                    [Pemain angkat Walkie Talkie]
                    [Lapor: "Netralisasi berhasil..."]
                    [Balasan DCS: "Copy, lingkungan aman."]
                                      │
                                      ▼
                              ✅ LEVEL 13 COMPLETE
                              [Hitung skor → tampilkan rapor]
                              [Fade out → Level 14 atau End]
```

---

# TIPS DEVELOPMENT (Urutan Bikin)

## Fase 1: Core Loop (Bikin ini dulu, bisa demo)
1. ✅ Scene kosong + spawn point
2. ✅ Tangki netralisasi + pH display (naik saat interact)
3. ✅ Karung kapur (grab & tuang ATAU tombol dosing)
4. ✅ Kondisi berhasil: pH 8-9 → lanjut

## Fase 2: Filter Press
5. ✅ Model filter press (bisa sederhana: box + plate)
6. ✅ 3 tombol (Close, Start, Open) + state machine
7. ✅ Gauge tekanan (UI atau analog)
8. ✅ Animasi cake jatuh (bisa pakai rigidbody sederhana)

## Fase 3: Monitoring
9. ✅ Area dry stack (terrain + tumpukan)
10. ✅ Piezometer & monitoring well (interact → show data)
11. ✅ Panel WWTP (UI panel + tombol approve/recirculate)

## Fase 4: Polish
12. ✅ Walkie Talkie interaction + audio balasan
13. ✅ Scoring system + rapor akhir
14. ✅ Audio SFX (motor, hidrolik, ding, alarm)
15. ✅ X-Ray view (opsional)
16. ✅ Animasi detail (conveyor, agitator, air menetes)

---

# CATATAN PENTING

## Untuk Lomba (Demo Singkat ~5 menit):
Kalau waktu terbatas, fokus ke **Step 1 + Step 2 + Step 6** saja:
- Netralisasi (tuang kapur, pH naik) ← paling visual & interaktif
- Filter Press (3 tombol, cake jatuh) ← paling "wow" secara mekanik
- Lapor HT + Rapor ← penutup yang rapi

## Untuk Full Game:
Semua 6 step + scoring + skenario alternatif (parameter merah di WWTP)

## Koneksi ke Level 14 (Emergency):
Setelah Level 13 selesai, bisa langsung trigger Level 14 di mana:
- Alarm berbunyi saat pemain masih di area tailing
- Skenario: kebocoran pipa asam / water table naik mendadak
- Pemain harus respons darurat

---

# REFERENSI VISUAL (Untuk Modeler 3D)

## Tangki Netralisasi:
- Silinder vertikal, diameter ~3m, tinggi ~5m
- Warna: abu-abu metalik
- Ada pengaduk (shaft + blade) terlihat dari atas
- Pipa inlet di samping atas, pipa outlet di samping bawah
- Display digital kecil di samping (pH meter)
- Rak kapur di dekatnya (3-5 karung putih)

## Filter Press:
- Rangka baja horizontal, panjang ~6m
- 10-20 plate berjajar (warna biru/hijau polypropylene)
- Silinder hidrolik di satu ujung
- Panel kontrol kecil di samping
- Tray penampung air di bawah
- Conveyor belt pendek di bawah plate

## Dry Stack Area:
- Area outdoor, tanah terbuka
- Tumpukan material abu-abu/coklat (tinggi ~2-3m)
- Geomembrane hitam terlihat di tepi
- Tiang piezometer (pipa kecil vertikal dari tanah)
- Pipa monitoring well (lebih besar, ada tutup)
- Safety fence / railing di sekeliling

## Panel WWTP:
- Kotak panel outdoor (stainless steel)
- Layar LCD kecil di dalamnya
- 2 tombol: [APPROVE] hijau, [RECIRCULATE] kuning
- Lampu indikator di atas

---

*Dokumen ini adalah panduan lengkap untuk development Level 13 OLIVIA VR.*
*Dibuat 25 Mei 2026.*
