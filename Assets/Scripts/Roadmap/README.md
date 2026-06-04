# 🏭 OLIVIA VR — Simulator Pelatihan Operator HPAL Nikel

> **AR/VR Industrial Training Simulator** berbasis Unity untuk pelatihan operator pabrik **HPAL (High Pressure Acid Leaching)** nikel.
> Pemain belajar proses, K3, SOP, dan emergency response melalui 15 level (Level 0 – Level 14) yang merepresentasikan flowsheet HPAL nyata: **Crusher → Slurry → Pre-Heater → Autoclave → Flash Train → CCD → MHP → Tailing → Dry Stack → Emergency K3**.

---

## 📌 Ringkasan Singkat

| Item | Detail |
|------|--------|
| **Nama Project** | OLIVIA VR — Operasi & Pelatihan VR HPAL Nikel |
| **Developer** | Ari Prabowo |
| **Engine** | Unity 6 + URP |
| **VR Stack** | XR Interaction Toolkit 3.4.1, XR Hands 1.7.3, Input System 1.18 |
| **Bahasa Kode** | C# (kode/komentar/identifier: **English**) |
| **Bahasa In-Game** | HUD, voice NPC, chat: **Indonesia** |
| **Scene Utama** | `Assets/Scenes/Level1.unity` |
| **Lokasi Project** | `C:\Users\mp2dz\Olivia` |
| **Total Level** | 15 (Level 0 – Level 14) |
| **Showcase** | Level 7 (Autoclave X-Ray) & Level 13 (Dry Stack Tailing) |
| **Tujuan** | Lomba Nasional / Prototype Industrial Training |

---

## 🎯 Tujuan Project

Membangun simulasi VR proses HPAL dari awal sampai skenario emergency, dengan fokus:

- Edukasi proses industri HPAL nikel secara visual dan interaktif
- Training keselamatan kerja (K3) dan kepatuhan SOP
- Simulasi peran ganda: **Operator DCS** (control room) dan **Operator Lapangan** (field)
- Interaksi VR-native dengan APD, valve, panel DCS, X-Ray view, walkie talkie, dan ESD
- Menunjukkan hubungan langsung antara parameter DCS dan kondisi mesin di lapangan
- Mengajarkan pengelolaan limbah B3 (tailing) yang aman bagi lingkungan

**Target akhir:** Pemain menyelesaikan Level 0–14, memahami alur HPAL penuh, lulus dengan skor ≥ 70%, dan menerima **Sertifikat K3 Virtual Operator HPAL**.

---

## 🗺️ Peta Dunia (3 Zona)

| Zona | Nama | Isi Utama |
|------|------|-----------|
| 🔵 A | **DCS Control Room** | Monitor besar, 14 tombol sistem, parameter (suhu/tekanan/pH/flow/RPM), tombol ESD, walkie talkie |
| 🟠 B | **Lantai Pabrik Atas** | Autoclave, pipa steam, platform, catwalk, tangga, valve, area inspeksi X-Ray |
| 🟢 C | **Lantai Pabrik Bawah** | Crusher, Slurry Tank, Pump, Flash Vessel, CCD, MHP, Tailing, Filter Press, Dry Stack |

---

## 🕹️ Mekanik Inti (Pilar Gameplay)

1. **Sistem Level-Based** — 15 level, masing-masing = satu tahap flowsheet HPAL nyata.
2. **Dual-Role (DCS ↔ Lapangan)** — pemain berpindah peran antara ruang kontrol dan lapangan tiap level.
3. **Walkie Talkie Wajib (Voice Command)** — setiap level wajib lapor via HT + ada balasan suara NPC sebagai konfirmasi.
4. **X-Ray / Invisible View** — melihat proses internal mesin (slurry, agitator, reaksi kimia, pemisahan).
5. **Sinkronisasi DCS ↔ Field** — nilai parameter DCS langsung mengontrol animasi/shader di lapangan (mis. flow rate → kecepatan aliran).
6. **APD & SafetyGate** — pemain wajib lengkap APD sebelum boleh masuk plant floor.
7. **Sistem Skor & Sertifikat K3** — penilaian per level, lulus minimal 70%.

---

## 🧱 Arsitektur Sistem

```
GameLevelManager (Singleton — Pusat Kontrol)
    │  state machine 15 level, unlock, parameter (suhu/tekanan/pH/RPM/flow), event, skor
    │
    ├── PhaseManager            → sub-state APD & operasional dalam level
    ├── WalkieTalkieManager     → voice recognizer + PTT + audio balasan NPC
    ├── PlayerHUD               → quest panel, checklist, notifikasi, fade transisi
    ├── LevelTeleportManager    → teleport XR Origin antar zona (aman dari snap-back)
    ├── DCSMonitorUI            → 14 tombol sinkronisasi + parameter + alarm + ESD
    ├── UniversalTaskMarker     → panah 3D + outline box pada target tugas aktif
    └── Level{N}Controller      → state machine + mekanik spesifik per level
```

**Script utama** ada di `Assets/Scripts/Simulation/` dan `Assets/Scripts/UI/`.
Tiap level punya controller sendiri: `Level3OreSlurryController.cs` … `Level14EmergencyController.cs`.

---

## 📂 Struktur Dokumen Roadmap

Semua dokumen ada di `Assets/Scripts/Roadmap/`. Urutan baca yang disarankan:

| # | Dokumen | Isi |
|---|---------|-----|
| 1 | `PROJECT_CONTEXT.md` | Identitas project, tujuan, zona, parameter, aturan kerja AI |
| 2 | `Olivia_Roadmap.md` | Roadmap tahap pengembangan + progress % |
| 3 | `Olivia_Blueprint_Final.md` | Master plan 15 level (cetak biru gameplay) |
| 4 | `ALUR_FINAL_OLIVIA.md` | **Alur final fix** Level 0–14 (step-by-step) ← lihat file ini |
| 5 | `BreakdownSistem.md` | Breakdown mekanik teknis ke industri nyata |
| 6 | `OLIVIA_HPAL_VR_SKILL.md` | Panduan agent AI: arsitektur, pitfalls, debug |
| 7 | `OLIVIA_AGENT_SKILL.md` | Knowledge pack handoff (versi awal) |
| 8 | `HPAL_DeepResearch.md` | Riset kimia & proses HPAL |
| 9 | `HPAL_Mekanisme_Mesin_DeepResearch.md` | Riset mekanisme per mesin |
| 10 | `GAMEPLAY_Level*.md` | Spesifikasi mekanik per level (5, 6, 7, 8, 9, 13) |
| 11 | `AUDIT_BUG_DAN_REFACTOR.md` | Daftar bug & prioritas refactor |
| 12 | `olivia_power_up_advice.md` | Saran peningkatan kualitas |

**Aset referensi visual:** `Peta dan Alur.png`, `Prototype Machine.png`, `Prototype Machine 2.png`, folder `Reference machine/`.

---

## 🦺 Sistem APD (Level 1)

Pemain tidak boleh masuk plant floor jika APD belum lengkap (`SafetyGate.cs`).

| No | APD | Fungsi |
|----|-----|--------|
| 1 | Safety Helmet | Lindungi kepala dari benda jatuh |
| 2 | Safety Vest / Rompi | Visibilitas + pelindung dada |
| 3 | Safety Glasses | Lindungi dari percikan H2SO4 |
| 4 | Safety Shoes | Lindungi kaki dari asam & benda berat |
| 5 | Chemical Gloves | Kontak pipa & peralatan berasam |
| 6 | Respirator / Masker | Wajib area asam & uap panas |
| 7 | Ear Protection | Lindungi dari bising mesin |
| 8 | Walkie Talkie / HT | Komunikasi DCS ↔ Lapangan (wajib semua level) |

---

## 📊 Parameter SOP Patokan Gameplay

| Parameter | Target | Level |
|-----------|--------|-------|
| Flow Rate Slurry | 450 m³/h | 4 |
| Suhu Pre-Heater | 180–200 °C | 5 |
| Dosis Asam (H2SO4) | 350 kg/ton bijih → pH 1.0 | 6 |
| Suhu Autoclave | 250–255 °C | 7–8 |
| Tekanan Autoclave | 45–50 atm | 7–8 |
| RPM Agitator | 60 RPM | 7–8 |
| Flash Train | 47 → 12 → 3 → 1.05 atm | 8 |
| Wash Efficiency CCD | ≥ 95% | 9 |
| Netralisasi Tailing | pH 8.0–9.0 | 13 |
| Moisture Tailing Cake | < 25% | 13 |

> Nilai bersifat edukatif/simulasi. Untuk akurasi industri lebih dalam, lihat `HPAL_DeepResearch.md`.

---

## 📈 Status Progress

| Tahap | Progress |
|-------|----------|
| Arsitektur Dasar | ~45% |
| Level Manager & UI 14 Tombol | berjalan |
| Voice Reply System (NPC Audio) | berjalan |
| Level 0–7 (Tutorial → Autoclave) | implementasi mekanik aktif |
| Level 8–9 (Flash Train, CCD) | spesifikasi + controller dibuat |
| Level 13 (Dry Stack) | dibuat & terverifikasi (showcase 2) |
| Level 14 (Emergency K3) | logika ESD awal ada |
| Polish & Scoring | berikutnya |

> **Strategi:** *Greybox-first* — semua level fungsional dulu dengan asset primitif, polish model 3D HPAL belakangan.

---

## 🛠️ Konvensi & Workflow

- **Kode**: English. **In-game text**: Indonesia (kasual).
- **Slurry**: warna **ungu** (`Slurry_Fill.mat`). Pipa transparan alpha 0.06–0.08.
- **Audio**: prosedural (`AudioClip.Create`), tanpa aset audio eksternal.
- **VR Teleport**: selalu `XROrigin.MoveCameraToWorldLocation` + `MatchOriginUpCameraForward` (jangan set `transform.position` langsung).
- **Setelah edit script**: `refresh_unity` (compile) → cek `read_console` → save scene (bukan saat play mode).
- **Greybox-first**, perubahan kecil & bertahap, jelaskan rencana sebelum edit besar.

---

## 🚀 Cara Memulai (untuk Developer / AI Agent)

1. Buka project Unity di `C:\Users\mp2dz\Olivia`, scene `Assets/Scenes/Level1.unity`.
2. Baca `PROJECT_CONTEXT.md` → `ALUR_FINAL_OLIVIA.md` → `OLIVIA_HPAL_VR_SKILL.md`.
3. Untuk test cepat level tertentu: gunakan `[ContextMenu]` debug skip di komponen `GameLevelManager` (mis. *"DEBUG: Skip ke Level 7"*).
4. Sebelum menambah mekanik baru: cek SOP nyata di dokumen DeepResearch, pilih angle VR-native, lalu dokumentasikan di `GAMEPLAY_LevelN_*.md`.

---

*OLIVIA VR — belajar mengoperasikan pabrik HPAL nikel dengan aman, dari Crusher sampai Dry Stack, tanpa risiko nyata.*
