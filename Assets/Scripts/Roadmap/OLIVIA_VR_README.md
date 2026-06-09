# 🥽 OLIVIA VR — Simulator Pelatihan Operator HPAL Nikel

> **Belajar mengoperasikan pabrik nikel HPAL dengan aman — dari Crusher hingga Dry Stack — tanpa risiko nyata.**

OLIVIA VR adalah *industrial training simulator* berbasis Virtual Reality yang merepresentasikan keseluruhan alur proses **HPAL (High Pressure Acid Leaching)** pengolahan bijih nikel laterit. Pemain berperan sebagai operator (DCS Control Room dan Lapangan) dan menjalani setiap tahap proses secara interaktif, aman, dan sesuai SOP industri nyata.

---

## 📑 Daftar Isi

1. [Tentang Aplikasi](#1-tentang-aplikasi)
2. [Fitur Utama](#2-fitur-utama)
3. [Teknologi yang Digunakan](#3-teknologi-yang-digunakan)
4. [Implementasi Teknologi Immersive](#4-implementasi-teknologi-immersive)
5. [Alur Aplikasi (Gameplay Flow)](#5-alur-aplikasi-gameplay-flow)
6. [Cara Menjalankan Aplikasi](#6-cara-menjalankan-aplikasi)
7. [Solusi yang Dikembangkan](#7-solusi-yang-dikembangkan)

---

## 1. Tentang Aplikasi

| | |
|---|---|
| **Nama** | OLIVIA VR — Operasi & Pelatihan VR HPAL Nikel |
| **Domain** | HPAL (High Pressure Acid Leaching) — hidrometalurgi nikel-kobalt |
| **Tipe** | Industrial Training Simulator (Virtual Reality) |
| **Platform** | VR Headset (XR Origin) + Desktop XR Device Simulator |
| **Tujuan** | Edukasi proses industri, keselamatan kerja (K3), dan kepatuhan SOP |

**Masalah yang dijawab:** Pelatihan operator pabrik HPAL nyata berisiko tinggi (asam sulfat pekat, suhu 250 °C, tekanan 50 atm) dan mahal. OLIVIA VR memindahkan pelatihan itu ke ruang virtual yang **aman, berulang, dan terukur**, sehingga calon operator memahami alur proses dan prosedur keselamatan sebelum masuk lapangan sungguhan.

**Output akhir:** Pemain menyelesaikan seluruh level, memahami flowsheet HPAL secara penuh, dan layak menerima **Sertifikat K3 Virtual Operator HPAL**.

---

## 2. Fitur Utama

### 🏭 Simulasi Proses HPAL End-to-End
Setiap level merepresentasikan satu tahap nyata pada flowsheet HPAL:

```
Crusher → Slurry Tank → Slurry Pump → Pre-Heater → Acid Injection →
Autoclave → Flash Letdown → CCD → Purification/MHP → Tailing → Dry Stack → Emergency
```

### 🎮 Dual-Role Operator
Pemain berpindah peran tiap level antara **Operator DCS** (ruang kontrol, menekan tombol & memantau parameter) dan **Operator Lapangan** (memutar valve, dosing reagen, inspeksi mesin).

### 📻 Walkie Talkie (Push-To-Talk) Wajib
Setiap level diakhiri laporan via HT dengan balasan suara NPC sebagai konfirmasi — melatih komunikasi radio operasional.

### 🔍 X-Ray / Invisible View
Melihat proses internal mesin (slurry naik, agitator berputar, reaksi kimia) yang mustahil dilihat di pabrik nyata.

### 🧪 Mekanik Kimia Realistis
Parameter dan warna larutan mengikuti kimia nyata: pH berubah saat dosing reagen, warna cairan bertransisi sesuai reaksi (mis. coklat karat → olive PLS → hijau MHP).

### ✅ Sistem Quest & Penilaian
HUD checklist per langkah, penanda tugas 3D (panah + outline), skor per level, dan sertifikat K3.

### 🛡️ Pelatihan Keselamatan (K3)
APD wajib (8 item), gerbang keselamatan, dan skenario darurat *Emergency Shutdown* (ESD).

---

## 3. Teknologi yang Digunakan

| Komponen | Teknologi |
|----------|-----------|
| **Game Engine** | Unity 6 |
| **Render Pipeline** | Universal Render Pipeline (URP) |
| **VR Framework** | XR Interaction Toolkit 3.4.x |
| **Hand Tracking** | XR Hands 1.7.x |
| **Input** | Unity Input System (action-based) + XR Device Simulator |
| **Bahasa Pemrograman** | C# |
| **3D Modeling** | Blender 5.1 (headless / `--background`), ekspor FBX |
| **Shader Kustom** | HLSL (URP) — shader cairan/liquid `Olivia/L7SlurryFill` |
| **Audio** | Prosedural (`AudioClip.Create`) — sirine, mesin, ambient |
| **Version Control** | Git + GitHub |

**Catatan arsitektur:** Seluruh level hidup berdampingan dalam satu scene utama, dikoordinasi oleh state machine `GameLevelManager`. Tiap level punya controller sendiri (`Level{N}Controller`) yang mendengarkan event dan menjalankan animasi/logikanya.

---

## 4. Implementasi Teknologi Immersive

Bagian ini menjelaskan *bagaimana* rasa immersive dibangun secara teknis.

### 🤚 Interaksi VR-Native
- **Gestural Handwheel** (`GesturalHandwheel.cs`): valve/handwheel diputar mengikuti **arah gerak tangan** pemain (twist controller diproyeksikan ke bidang piringan roda), bukan tombol. Dipakai di Pre-Heater, Autoclave, dan Flash Train.
- **Grab & Socket**: APD (helm, masker, sarung tangan, walkie talkie) diambil dengan `XRGrabInteractable` dan dipasang ke socket tubuh (`XRSocketInteractor`).
- **Tombol world-space VR**: memakai `XRSimpleInteractable` + `BoxCollider` (bisa diklik ray/poke), dengan *fallback* keyboard untuk mode simulator.

### 🧍 Kenyamanan & Anti-Motion-Sickness
- **Teleport halus** antar zona via `XROrigin.MoveCameraToWorldLocation` + `MatchOriginUpCameraForward` (anti snap-back), dengan transisi *fade*.
- **Socket dada mengikuti badan** secara halus lewat `Application.onBeforeRender` (mengikuti pose kamera terbaru saat render) sehingga masker/HT tidak "patah-patah".

### 💧 Visual Cairan Realistis
- Shader kustom `Olivia/L7SlurryFill`: permukaan cairan **naik dari dasar** (world-Y clip), riak gelombang, fresnel, kilau spekular, dan **pusaran (swirl) yang mengikuti putaran rotor agitator** secara real-time.
- Transisi warna larutan **chemistry-accurate** (di-render via material instance agar kompatibel dengan URP SRP Batcher).
- Efek pendukung: gelembung reaksi (ParticleSystem), uap (steam FX), aliran slurry di pipa (X-ray).

### 🔄 Sinkronisasi DCS ↔ Lapangan
Parameter yang diatur di panel DCS (flow rate, dosis asam, suhu) langsung menggerakkan animasi/shader mesin di lapangan secara real-time — pemain melihat sebab-akibat aksinya.

### 🗣️ Audio Spasial & Voice Report
Audio mesin/uap/sirine dibangkitkan secara prosedural; laporan HT memakai Push-To-Talk dengan balasan suara NPC, memperkuat keterlibatan.

---

## 5. Alur Aplikasi (Gameplay Flow)

### Pola Dasar Tiap Level
```
[1] Mulai di zona (DCS Room / Lapangan)
        ↓
[2] Aksi utama (tekan tombol DCS / putar valve / X-Ray / dosing)
        ↓
[3] Sinkronisasi DCS ↔ Lapangan (parameter, animasi, shader berubah)
        ↓
[4] Lapor via Walkie Talkie (Push-To-Talk)
        ↓
[5] Balasan suara NPC → Level selesai
        ↓
[6] Fade Out → Teleport ke level berikutnya
```

### Ringkasan Level
| Level | Zona | Aksi Inti |
|-------|------|-----------|
| 1 | Loker | Pakai 8 APD + lapor HT |
| 2 | DCS | Persiapan & tekan tombol DCS |
| 3 | Lapangan | Ore crusher → slurry tank (X-Ray) |
| 4 | DCS→Field | Slurry pump + atur flow rate |
| 5 | Field | Putar steam valve (gestural) — pre-heater |
| 6 | Field→DCS | Acid injection (dosis 350 kg/ton, pH 1.0) |
| 7 | Field | **Autoclave X-Ray** — cairan realistis + agitator (Showcase) |
| 8 | Field | Flash Train 3-stage letdown (gestural handwheel) |
| 9 | Field | CCD — pemisahan padat-cair + Lab QC |
| 10 | Field | **Purification & MHP** — netralisasi bertahap (HT-gated) |
| 11 | Field | Tailing & Filter Press |
| 12 | Field | **Dry Stack Tailing** (Showcase) |
| 13 | DCS | Emergency K3 / ESD |

---

## 6. Cara Menjalankan Aplikasi

### Prasyarat
- **Unity 6** (dengan modul URP & XR).
- **VR Headset** yang mendukung OpenXR (Meta Quest, dll.) — atau gunakan **XR Device Simulator** untuk desktop.
- Git (untuk meng-clone repository).

### Langkah Menjalankan
1. **Clone & buka project**
   ```
   git clone https://github.com/arivopwenz/Olivia-HPAL-Training-VR.git
   ```
   Buka folder project di Unity 6.
2. **Buka scene utama**: `Assets/Scenes/Level1.unity` (atau scene aktif yang sedang dikembangkan).
3. **Hubungkan VR Headset** (mode Play) **atau** aktifkan **XR Device Simulator** untuk kontrol mouse/keyboard.
4. Tekan **Play**.

### Kontrol Utama
| Input | Aksi |
|-------|------|
| Grip / Trigger | Grab / putar handwheel |
| Tahan **T** | Voice report Walkie Talkie (Push-To-Talk) |
| **X** | Toggle X-Ray (Level 7) |
| **Space / 1** | Dosing / aksi tahap |
| **Enter** | ACCEPT hasil lab / compliance |
| Ray controller | Klik tombol DCS world-space |

### Debug / Loncat Level
Klik kanan komponen **`GameLevelManager`** di Inspector → pilih **`DEBUG: Skip ke Level N`** untuk menguji level tertentu tanpa bermain dari awal.

---

## 7. Solusi yang Dikembangkan

Beberapa tantangan teknis utama dan solusinya:

### 🎯 Pelatihan Berisiko Tinggi → Lingkungan Virtual Aman
Memindahkan prosedur berbahaya (asam, suhu/tekanan ekstrem) ke VR yang aman, dapat diulang, dan terukur — lengkap dengan skenario darurat tanpa konsekuensi nyata.

### 🤚 Interaksi Mesin yang Intuitif
Mekanisme **Gestural Handwheel** membuat pemutaran valve terasa natural (mengikuti tangan), bukan klik tombol — meniru aksi operator sungguhan.

### 💧 Visualisasi Proses Tak Terlihat
Kombinasi **X-Ray View + shader cairan kustom** menampilkan proses internal (cairan naik, agitator berputar, reaksi kimia) yang tak pernah bisa dilihat di pabrik nyata, mempercepat pemahaman.

### 🧪 Akurasi Kimia & Warna Real
Riset proses HPAL diterapkan ke parameter dan **transisi warna larutan** sesuai kimia nyata (mis. limonit coklat → PLS olive → MHP hijau-kebiruan), menjaga nilai edukatif.

### 🥽 Kenyamanan VR (Comfort)
Teleport berbasis `XROrigin` + fade (anti motion sickness) dan item dada yang mengikuti pose render terbaru (`onBeforeRender`) menghilangkan jitter — pengalaman yang nyaman dipakai lama.

### 🏗️ Konten 3D Skala Besar via Blender Headless
Model mesin industri (autoclave, flash vessel, thickener, pipa) dibuat lewat **Blender headless** terotomasi dan diekspor sebagai FBX, memungkinkan iterasi cepat aset berskala pabrik.

### 🧩 Arsitektur Modular
State machine `GameLevelManager` + controller per level membuat 13 level kompleks tetap terkelola, mudah di-debug, dan dapat dikembangkan bertahap.

---

> **OLIVIA VR** — menghadirkan pabrik HPAL nikel ke dalam genggaman, agar operator masa depan belajar dengan aman, paham prosesnya, dan siap di lapangan nyata.
