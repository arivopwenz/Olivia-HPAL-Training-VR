# 🗺️ OLIVIA — Roadmap & Tasklist
## Deadline: Akhir Mei / Awal Juni 2026
## Waktu tersedia: ~4 minggu (5 Mei — 1 Juni)

---

# ⚡ STATUS PROJECT SAAT INI

| Aspek | Status |
|-------|--------|
| Unity Project | ✅ Sudah ada |
| VR Template | ✅ Sudah ada (XR Interaction Toolkit) |
| Scene | BasicScene, GameScene, SampleScene |
| Script | Percobaan.cs (kosong) |
| 3D Model Pabrik | ❌ Belum ada |
| Gameplay | ❌ Belum ada |

---

# 📅 ROADMAP MINGGUAN

---

## MINGGU 1 (5-11 Mei) — FONDASI & GREYBOX

> Fokus: Setup project + greybox layout + core script. BELUM perlu bagus, yang penting FUNGSIONAL.

### Hari 1-2: Setup & Struktur Project
- [ ] Buat folder structure yang rapi:
  ```
  Assets/
  ├── Scenes/        (TutorialScene, ControlRoomScene, PlantFloorScene, EmergencyScene, ResultScene)
  ├── Scripts/
  │   ├── Core/      (GameManager, SceneLoader, AudioManager)
  │   ├── Phase1/    (DCSController, MonitorUI, ParameterSystem)
  │   ├── Phase2/    (XRayVision, ScaleInspector, APDManager)
  │   ├── Phase3/    (EmergencyManager, ValveInteraction, ESDButton, Timer)
  │   └── UI/        (HUDManager, ScoreSystem, CertificateGenerator)
  ├── Prefabs/
  ├── Materials/
  ├── Audio/
  ├── Models/
  └── UI/
  ```
- [ ] Buat `GameManager.cs` — singleton untuk mengatur state game & perpindahan fase
- [ ] Buat `SceneLoader.cs` — handle transisi antar scene
- [ ] Buat 5 scene kosong di Unity

### Hari 3-4: Greybox Layout (Pakai Primitif/Placeholder)
- [ ] **Ruang Kontrol DCS** — pakai Cube untuk meja, Plane untuk monitor, Cube untuk kursi
- [ ] **Lantai Pabrik** — pakai Cylinder untuk autoclave, Cylinder kecil untuk pipa, Cube untuk katup
- [ ] Pastikan skala ukuran terasa realistis di VR (test di headset!)
- [ ] Setup XR Rig bisa jalan/teleport di kedua area
- [ ] Tandai posisi objek interaktif (katup, tombol ESD, monitor) dengan warna berbeda

### Hari 5: Core Mechanic Prototype
- [ ] Test XR Grab Interactable — bisa grab objek sederhana (kubus)
- [ ] Test XR rotasi — bisa putar objek (simulasi katup)
- [ ] Buat transisi scene sederhana (Fase 1 → Fase 2 → Fase 3)
- [ ] Cari & download asset 3D (belum perlu import, kumpulkan dulu)

### ✅ Target Minggu 1:
**Layout pabrik sudah ada (walau masih kotak-kotak). Pemain bisa jalan di VR, grab objek, dan pindah antar fase. Mekanik dasar VR terbukti bekerja.**

---

## MINGGU 2 (12-18 Mei) — FASE 1 & FASE 2 (GAMEPLAY INTI)

> Fokus: Mekanisme gameplay kontrol & inspeksi

### Hari 1-2: Fase 1 — Ruang Kontrol DCS
- [ ] Buat `ParameterSystem.cs` — simulasi suhu, tekanan, pH, flow rate yang berubah real-time
- [ ] Buat UI monitor HMI dengan parameter:
  - Suhu (target: 250°C)
  - Tekanan (target: 50 Bar)
  - pH Level
  - Flow Rate
  - RPM Agitator
  - Scale Level %
- [ ] Buat `DCSController.cs` — interaksi: putar knob kalibrasi, tekan tombol
- [ ] Indikator warna: hijau (normal), kuning (warning), merah (bahaya)
- [ ] Parameter berfluktuasi otomatis, pemain harus menstabilkan

### Hari 3-4: Fase 2 — X-Ray Vision & Inspeksi
- [ ] Buat `XRayVisionController.cs`:
  - Tekan tombol → material autoclave berubah transparan (shader swap)
  - Tampilkan interior: agitator berputar (animasi rotasi), cairan, kerak
- [ ] Buat `ScaleInspector.cs`:
  - Area kerak muncul random di beberapa titik dalam autoclave
  - Pemain point → highlight area → confirm untuk menandai
  - Tracking: berapa kerak yang berhasil ditandai
- [ ] Animasi agitator (baling-baling) berputar konstan

### Hari 5: Fase 1.5 — Sistem APD
- [ ] Buat `APDManager.cs`:
  - 3 item APD: helm, kacamata, sarung tangan (XR Grabbable)
  - Pemain grab → snap ke tubuh/kepala
  - Cek: jika belum lengkap → pintu ke lantai pabrik terkunci
  - UI feedback: checklist APD ✅/❌

### ✅ Target Minggu 2:
**Pemain bisa kalibrasi monitor di ruang kontrol, pakai APD, turun ke pabrik, aktifkan X-Ray vision, dan inspeksi kerak.**

---

## MINGGU 3 (19-25 Mei) — FASE 3 (DARURAT) & SCORING

> Fokus: Klimaks gameplay + sistem penilaian

### Hari 1-3: Fase 3 — Skenario Darurat
- [ ] Buat `EmergencyManager.cs`:
  - Trigger: setelah inspeksi selesai → delay → ALARM!
  - Ubah lighting jadi merah (emergency light)
  - Aktifkan sirine & alarm audio
  - Tekanan mulai naik di UI (> 65 Bar)
  - Timer countdown 45 detik (adjustable)
- [ ] Buat `ValveInteraction.cs`:
  - Isolation valve: XR Grabbable + rotasi (grip + putar)
  - Tracking sudut putaran (harus sampai 360° atau sesuai threshold)
- [ ] Buat `ESDButton.cs`:
  - Tombol darurat di balik kaca pelindung
  - Pemain harus "pecahkan" kaca dulu (grab/punch) → baru bisa tekan
  - Tekan tombol → sistem shutdown → tekanan turun → SELAMAT
- [ ] Skenario gagal: timer habis → cutscene ledakan (particle explosion + screen shake)

### Hari 4-5: Sistem Scoring & Sertifikat
- [ ] Buat `ScoreSystem.cs`:
  - Track semua metrik: waktu tanggap, akurasi katup, kepatuhan K3, inspeksi scale
  - Hitung grade per metrik (A+, A, B+, B, C)
  - Hitung grade keseluruhan
- [ ] Buat `CertificateUI.cs`:
  - Tampilkan sertifikat K3 di world-space canvas
  - Input nama pemain
  - Tampilkan semua metrik + grade
  - Tombol: Main Lagi / Keluar

### ✅ Target Minggu 3:
**GAME BISA DIMAINKAN DARI AWAL SAMPAI AKHIR. Pemain bisa merasakan seluruh alur: kontrol → APD → inspeksi → darurat → skor.**

---

## MINGGU 4 (26 Mei - 1 Juni) — VISUAL POLISH & PRESENTASI

> Fokus: Ganti greybox → asset final, lighting, audio, dan SIAP LOMBA

### Hari 1: Ganti Greybox → Asset 3D Final
- [ ] Replace semua primitif placeholder dengan model 3D yang sudah dikumpulkan
- [ ] Pasang material & texture yang proper pada semua objek
- [ ] Tambah detail kecil: stiker K3, rambu safety, pemadam kebakaran

### Hari 2: Lighting, Particle & Visual Atmosphere
- [ ] Setup pencahayaan industri (kuning-oranye, bayangan keras)
- [ ] Tambah particle system: uap dari pipa, asap tipis
- [ ] Emergency lighting: lampu merah berkedip saat Fase 3
- [ ] Bake lightmap (jika pakai baked lighting untuk performa)

### Hari 3: Audio & Opening
- [ ] Tambah ambient sound per scene:
  - Ruang kontrol: AC, beep monitor
  - Lantai pabrik: mesin berderu, pipa bergetar
  - Darurat: sirine, detak jantung
- [ ] Setup Spatial Audio (3D sound) untuk semua audio source
- [ ] Sound effect: tombol click, katup putar, kaca pecah, ledakan
- [ ] Buat opening cinematic sederhana (Timeline + TextMeshPro)
- [ ] Buat tutorial singkat (Fase 0) — bisa di-skip

### Hari 4: Bug Fix & Testing
- [ ] Test FULL playthrough minimal 5x
- [ ] Fix semua bug gameplay
- [ ] Test di headset VR yang akan dipakai saat lomba
- [ ] Pastikan performa smooth (target: 72 FPS minimum)
- [ ] Cek semua transisi scene lancar

### Hari 5: Persiapan Presentasi
- [ ] Rekam video gameplay untuk backup
- [ ] Siapkan slide presentasi (framing sebagai "industrial training simulator")
- [ ] Latihan demo: pastikan tahu urutan yang harus ditunjukkan ke juri
- [ ] Siapkan penjelasan teknis singkat (2-3 menit)

### ✅ Target Minggu 4:
**PROYEK FINAL, POLISHED, DAN SIAP DIPRESENTASIKAN KE JURI.**

---

# 🎯 MILESTONE SUMMARY

```
Minggu 1 ████░░░░ Greybox & Core Mechanic   → "Kotak-kotak tapi jalan"
Minggu 2 ████████ Gameplay Inti (Fase 1+2)  → "Bisa dimainkan sebagian"
Minggu 3 ████████ Klimaks & Scoring         → "Bisa dimainkan penuh"
Minggu 4 ████████ Visual Polish & Presentasi → "Cantik & siap lomba!"
```

---

# 🔧 DAFTAR SCRIPT YANG PERLU DIBUAT

| No | Script | Fungsi | Minggu |
|----|--------|--------|--------|
| 1 | `GameManager.cs` | State game, perpindahan fase | 1 |
| 2 | `SceneLoader.cs` | Transisi antar scene | 1 |
| 3 | `AudioManager.cs` | Kelola semua audio + spatial | 1 |
| 4 | `ParameterSystem.cs` | Simulasi suhu, tekanan, pH, dll | 2 |
| 5 | `DCSController.cs` | Interaksi knob & tombol di ruang kontrol | 2 |
| 6 | `MonitorUI.cs` | Tampilan dashboard HMI | 2 |
| 7 | `XRayVisionController.cs` | Toggle transparansi autoclave | 2 |
| 8 | `ScaleInspector.cs` | Deteksi & tandai kerak | 2 |
| 9 | `APDManager.cs` | Sistem pakai alat pelindung diri | 2 |
| 10 | `EmergencyManager.cs` | Kontrol skenario darurat | 3 |
| 11 | `ValveInteraction.cs` | Putar katup manual | 3 |
| 12 | `ESDButton.cs` | Tombol emergency shutdown | 3 |
| 13 | `CountdownTimer.cs` | Timer 45 detik | 3 |
| 14 | `ScoreSystem.cs` | Hitung & simpan skor | 3 |
| 15 | `CertificateUI.cs` | Tampilkan sertifikat K3 | 3 |
| 16 | `CinematicController.cs` | Opening cinematic | 4 |
| 17 | `TutorialManager.cs` | Fase 0 tutorial | 4 |

---

# 🛒 ASSET YANG PERLU DICARI

| Asset | Sumber Rekomendasi | Prioritas |
|-------|--------------------|-----------|
| Model pabrik/industrial environment | Unity Asset Store, Sketchfab | 🔴 P0 |
| Model autoclave (silinder horizontal) | Sketchfab, atau buat dari Cylinder primitif | 🔴 P0 |
| Model pipa industri + katup | Unity Asset Store "Modular Pipes" | 🔴 P0 |
| Model monitor/komputer | Unity Asset Store | 🔴 P0 |
| Model APD (helm, kacamata, sarung tangan) | Sketchfab | 🟡 P1 |
| SFX industri (mesin, alarm, ledakan) | Freesound.org, Pixabay Audio | 🟡 P1 |
| Particle effect (uap, api, ledakan) | Unity Particle Pack (gratis) | 🟡 P1 |
| Font industrial/digital | Google Fonts (Orbitron, Share Tech Mono) | 🟢 P2 |

---

# ⚠️ TIPS PENTING

1. **Commit sering ke Git** — jangan sampai kehilangan progress
2. **Test di VR headset setiap 2 hari** — jangan cuma di editor
3. **Jangan perfeksionis di Minggu 1-2** — bikin fungsional dulu, baguskan nanti
4. **Minggu 3 = game HARUS playable** — kalau belum, skip fitur bonus
5. **Siapkan Plan B** — kalau ada fitur yang terlalu susah, punya versi sederhana
