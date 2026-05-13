# 📅 Roadmap Pengembangan OLIVIA VR v3.0
## Sistem Level-Based (Level 0 – Level 14)

> **Strategi:** Greybox-first — semua level dibuat fungsional dulu dengan asset primitif (Cube, Cylinder, Sphere), polish model 3D belakangan.

---

## ✅ TAHAP 0: Arsitektur Dasar [SELESAI ~45%]
- [x] `PhaseManager.cs` — State Machine + Event System
- [x] `DCSMonitorUI.cs` v2.0 — Flow Tracker, Valve Status, ESD Panel
- [x] `TaskTrigger.cs` — 7 APD + HT + Valve + ESD
- [x] `DCSMonitorUI.cs` — 10 Parameter + Alarm System
- [x] Sistem APD Fase 1 fungsional (Helm, Rompi, Kacamata, Sepatu, Sarung Tangan)
- [ ] **Tambah Masker/Respirator ke APD list** (Level 1)
- [ ] **Tambah Walkie Talkie ke APD list** (Level 1)

---

## ⏳ TAHAP 1: Level Manager & UI Structure [PRIORITAS KINI]

- [ ] **1.1** Buat `GameLevelManager.cs` — mengatur status 14 level, unlock, dan sinkronisasi DCS-Field.
- [ ] **1.2** Buat DCS Panel UI dengan **14 Tombol Sinkronisasi** (tombol akan menyala/berkedip sesuai level aktif).
- [ ] **1.3** Refactor `PhaseManager.cs` — transisi state sekarang diurus oleh `GameLevelManager`.
- [ ] **1.4** Buat sistem **Walkie Talkie Manager** — mengatur rekaman suara NPC pembalas (MP3/WAV).
- [ ] **1.5** Buat `LevelHUD.cs` — HUD pemain (Nomor Level, Quest, Timer).

---

## ❌ TAHAP 2: Level 0 & 1 — Tutorial & APD [BELUM]
- [ ] Level 0: 4 latihan dasar (Jalan, Grab, Radio, HUD).
- [ ] Level 1: Lengkapi 7 APD. Sistem `SafetyGate.cs` terbuka otomatis saat lengkap.

---

## ❌ TAHAP 3: Level 2 & 3 — Prep DCS & Slurry X-Ray [BELUM]
- [ ] Level 2: Tombol DCS belum aktif. Pemain harus tes radio ("siapkan area"). NPC membalas.
- [ ] Level 3: Sistem X-Ray (Render override). Lihat partikel Ore dihancurkan dan Slurry diaduk. Pemain harus radio lapor.

---

## ❌ TAHAP 4: Level 4 — Sinkronisasi Flow Pump [BELUM]
- [ ] Tombol DCS 4 (Slurry Pump) menyala/outline glowing.
- [ ] Pemain menekan tombol.
- [ ] Buat logika **Sinkronisasi Flow Rate**: Nilai UI Flow Rate di DCS harus mengontrol shader parameter (kecepatan aliran) di Field.

---

## ❌ TAHAP 5: Level 5 - 11 — Flow Process [BELUM]
- [ ] Level 5: Katup steam berotasi (Rotary Interactable). X-Ray Pre-Heater memanas.
- [ ] Level 6: DCS Acid Injection. pH turun di monitor.
- [ ] Level 7: Model Autoclave. X-Ray menampakkan partikel hijau, agitator, UI Floating Parameter.
- [ ] Level 8: DCS monitoring 60 detik.
- [ ] Level 9: X-Ray Flash Vessel (uap keluar).
- [ ] Level 10: DCS CCD Activation.
- [ ] Level 11: MHP Presipitasi. Grab botol sampel.

---

## ❌ TAHAP 6: Level 12 & 13 — Tailing Management (Immersive Learning) [BELUM]
- [ ] Level 12: DCS menekan tombol Tailing Discharge.
- [ ] Level 13: Area khusus dengan signage B3.
- [ ] Mekanisme netralisasi: Pemain grab karung limestone, taburkan ke tangki, pH naik 8.5.
- [ ] X-Ray Filter press (memeras lumpur). Tailing cake visual.

---

## ❌ TAHAP 7: Level 14 — DARURAT (K3 Leak/Bocor - No Explosion) [BELUM]
- [ ] Pemicu: Alarm Gas / Asap putih menyembur (H2SO4 / Steam Leak).
- [ ] Audio: Suara mendesis pipa bocor, alarm sirine pabrik.
- [ ] Tugas DCS: Radio evakuasi + Tekan tombol ESD.
- [ ] ESD Logic: Mematikan semua pompa dan katup. Asap berhenti jika berhasil. 
- [ ] Kondisi Gagal: Pemain telat tekan ESD → Parameter gagal, peringatan bahaya lingkungan.

---

## ❌ TAHAP 8: Polish & Scoring [NANTI]
- [ ] Sistem skor per level (Kecepatan, Sinkronisasi, Radio SOP, Urutan).
- [ ] Rapor akhir (Sertifikat K3 Virtual).
- [ ] Ganti aset primitif dengan 3D Model HPAL.
- [ ] Spatial Audio 3D (Suara NPC radio via PTT, mesin bergemuruh).

---

## 📊 Progress Keseluruhan

| Tahap | Estimasi % |
|-------|------------|
| Arsitektur Dasar | 45% |
| Level Manager & UI 14 Tombol | 0% |
| Voice Reply System (NPC Audio) | 0% |
| Level 0-1 (Tutorial & APD) | 30% |
| Level 2-3 (DCS Prep & Ore) | 0% |
| Level 4 (Flow Sync) | 0% |
| Level 5-11 (Proses Utama) | 0% |
| Level 12-13 (Tailing Immersive) | 0% |
| Level 14 (K3 Darurat Kebocoran) | 5% (Logika awal ESD ada) |
| Polish | 0% |
| **TOTAL** | **~13%** |

> **Prioritas Sesi Berikutnya:**
> 1. Buat `GameLevelManager.cs` (14 Level Logic).
> 2. Implementasi Sistem Radio (Voice Command pemain + Audio Reply NPC).
> 3. Buat UI DCS 14 Tombol.
