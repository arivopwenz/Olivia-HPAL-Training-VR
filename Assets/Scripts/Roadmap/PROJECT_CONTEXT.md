# PROJECT_CONTEXT.md

## 1. Identitas Project

Nama project: Olivia / OLIVIA VR HPAL Simulation  
Developer: Ari Prabowo  
Lokasi project Windows: C:\Users\mp2dz\Olivia  
Lokasi project WSL: /mnt/c/Users/mp2dz/Olivia  

Olivia adalah game/simulasi VR berbasis Unity untuk training operator industri HPAL nikel. Project ini bukan sekadar game, tetapi industrial training simulator untuk edukasi proses HPAL, SOP K3, APD, DCS control room, pengoperasian mesin, inspeksi lapangan, tailing management, dan emergency response.

---

## 2. Tujuan Utama Project

Membangun simulasi VR proses HPAL dari awal sampai emergency scenario, dengan fokus:

- Edukasi proses industri HPAL nikel
- Training keselamatan kerja/K3
- Simulasi operator DCS dan operator lapangan
- Interaksi VR dengan APD, valve, panel DCS, X-Ray view, walkie talkie, dan emergency shutdown
- Menampilkan hubungan antara parameter DCS dan kondisi mesin di lapangan
- Membuat pengalaman yang layak untuk lomba/presentasi/prototype industrial training

Target akhir project:
Pemain menyelesaikan Level 0 sampai Level 14, memahami alur HPAL, memakai APD lengkap, mengikuti SOP, mengoperasikan DCS, melakukan inspeksi lapangan, mengelola tailing B3, dan menangani skenario emergency dengan benar.

---

## 3. Tech Stack

- Unity
- C#
- VR/XR Interaction Toolkit
- GitHub
- OpenClaw / Olivia AI
- Telegram remote assistant
- MCP server Unity
- Markdown documentation

---

## 4. Konsep Gameplay Utama

Game menggunakan sistem level-based dari Level 0 sampai Level 14.

Setiap level merepresentasikan satu tahap dalam flowsheet HPAL:

- Level 0: Tutorial VR
- Level 1: APD Safety / Locker Room / The Hub
- Level 2: DCS preparation
- Level 3: Crusher dan Slurry Tank
- Level 4: Slurry Pump dan flow rate sync
- Level 5: Steam Valve dan Pre-heater
- Level 6: Acid Injection
- Level 7: Autoclave inspection
- Level 8: DCS monitoring dan koreksi parameter
- Level 9: Flash Vessel / Letdown
- Level 10: CCD Separator
- Level 11: MHP Sampling
- Level 12: Tailing Discharge
- Level 13: Tailing Waste Management
- Level 14: Emergency K3 / Leak / ESD

Di setiap level, pemain wajib melakukan laporan melalui walkie talkie/HT, lalu mendapat balasan suara NPC sebagai konfirmasi.

---

## 5. Zona Game

Project memiliki 3 zona utama:

### A. DCS Control Room
Isi:
- Monitor utama
- Panel DCS
- 14 tombol sistem
- Parameter suhu, tekanan, pH, flow rate, RPM, alarm
- Tombol ESD
- Walkie talkie

### B. Lantai Pabrik Atas
Isi:
- Autoclave
- Steam pipe
- Platform
- Catwalk
- Tangga
- Valve
- Area inspeksi X-Ray

### C. Lantai Pabrik Bawah
Isi:
- Crusher
- Slurry Tank
- Pump
- Flash Vessel
- CCD
- MHP
- Tailing
- Filter Press
- Dry Stack

---

## 6. Kondisi Progress Saat Ini

Project sudah memiliki beberapa sistem dasar:

- PhaseManager.cs untuk state machine awal
- DCSMonitorUI.cs untuk parameter, valve status, alarm, dan UI DCS
- TaskTrigger.cs untuk beberapa trigger APD, HT, Valve, dan ESD
- Sistem APD dasar sebagian sudah berjalan
- Sistem ESD awal sudah ada
- Project sudah punya GitHub
- Project sudah punya folder Unity yang aktif
- OpenClaw/Olivia AI sudah terhubung
- Telegram remote assistant sudah berhasil jalan
- MCP server Unity sedang/akan diintegrasikan

Progress perkiraan:
- Arsitektur dasar: sekitar 45%
- Level 0-1: sebagian berjalan
- Level 14: logika ESD awal ada
- Total project: sekitar 13%

---

## 7. Prioritas Development Terdekat

Prioritas utama sekarang:

1. Buat GameLevelManager.cs
   - Mengatur 14 level
   - Mengatur unlock level
   - Mengatur status aktif level
   - Mengatur perpindahan DCS ↔ Field
   - Menggantikan sebagian peran PhaseManager untuk transisi besar

2. Buat DCS Panel UI 14 Tombol
   - 14 tombol sesuai level
   - Tombol aktif berkedip/glowing sesuai level
   - Tombol hanya bisa ditekan saat level yang benar aktif

3. Refactor PhaseManager.cs
   - PhaseManager menangani sub-state kecil
   - GameLevelManager menangani alur level utama

4. Buat WalkieTalkieManager / VoiceCommandSystem
   - Pendeteksi keyword per level
   - Trigger audio balasan NPC
   - Sistem PTT
   - Integrasi dengan GameLevelManager

5. Buat LevelHUD.cs
   - Menampilkan nomor level
   - Quest aktif
   - Timer
   - Status objective
   - Feedback berhasil/gagal

6. Lengkapi APD Level 1
   - Helm
   - Rompi
   - Kacamata
   - Sepatu
   - Sarung tangan
   - Respirator/masker
   - Ear protection
   - Walkie talkie/HT

---

## 8. Script Penting yang Perlu Dicari Olivia

Olivia harus mencari dan memahami script berikut:

- PhaseManager.cs
- DCSMonitorUI.cs
- TaskTrigger.cs
- GameLevelManager.cs
- LevelHUD.cs
- WalkieTalkieManager.cs
- VoiceCommandSystem.cs
- XRayViewController.cs
- SafetyGate.cs
- APD-related scripts
- Valve interaction scripts
- ESD logic scripts
- MCP server related scripts

Jika script belum ada, Olivia boleh mengusulkan struktur dan membuat file baru setelah menjelaskan rencana terlebih dahulu.

---

## 9. Sistem APD Level 1

APD wajib dasar:

1. Safety Helmet
2. Safety Vest / Rompi
3. Safety Glasses
4. Safety Shoes
5. Chemical-Resistant Gloves
6. Respirator / Masker
7. Ear Protection
8. Walkie Talkie / HT

Aturan:
- Pemain tidak boleh masuk plant floor jika APD belum lengkap.
- SafetyGate.cs harus terbuka hanya jika APD sesuai checklist.
- Walkie Talkie wajib dibawa karena semua level membutuhkan laporan radio.

---

## 10. Sistem Walkie Talkie

Walkie Talkie adalah mekanik wajib.

Alur:
1. Pemain grab walkie talkie
2. Pemain tekan tombol PTT
3. Pemain mengucapkan keyword level
4. Keyword dikenali
5. GameLevelManager menerima event
6. AudioSource memutar balasan NPC
7. Objective level selesai

Contoh keyword:
- Level 1: "APD lengkap"
- Level 2: "siapkan area"
- Level 3: "ore masuk"
- Level 4: "slurry pump aktif"
- Level 5: "katup steam terbuka"
- Level 6: "acid aktif"
- Level 7: "suhu 250", "tekanan 50"
- Level 8: "parameter stabil"
- Level 9: "flash vessel normal"
- Level 10: "CCD aktif"
- Level 11: "MHP terbentuk"
- Level 12: "limbah dialirkan"
- Level 13: "tailing aman", "pH 8.5"
- Level 14: "emergency", "evakuasi"

---

## 11. Parameter HPAL yang Harus Dipakai

Parameter proses yang menjadi patokan gameplay:

- Suhu autoclave target: sekitar 250°C
- Tekanan autoclave target: sekitar 45–50 atm/bar
- Batas bahaya tekanan: sekitar >65 bar
- pH acid leaching target: sekitar 1.0
- Flow rate slurry target Level 4: 450 m³/h
- Acid injection target Level 6: 350 kg/ton bijih
- Agitator target Level 7-8: 60 RPM
- Tailing neutralization target: pH 8.0–9.0
- Filter press target: moisture tailing cake <25%

Catatan:
Nilai ini adalah nilai gameplay/simulasi edukatif. Jika perlu akurasi industri lebih tinggi, cek dokumen HPAL Deep Research.

---

## 12. Sistem X-Ray / Invisible View

X-Ray View digunakan untuk melihat proses internal mesin:

- Crusher
- Slurry Tank
- Pre-heater
- Autoclave
- Flash Vessel
- MHP Tank
- Filter Press

Implementasi boleh menggunakan:
- Material swap
- Transparent shader
- Stencil buffer
- Layer-based visibility
- Hologram overlay

X-Ray View harus membantu pemain memahami proses, bukan hanya efek visual.

---

## 13. Sistem DCS dan Sinkronisasi Lapangan

DCS Control Room harus bisa mengontrol kondisi lapangan.

Contoh:
- Flow rate di DCS mengontrol speed shader aliran slurry di pipa
- Acid injection menurunkan pH
- Steam valve menaikkan suhu pre-heater
- ESD menutup valve dan mematikan pompa
- Parameter DCS berubah sesuai aksi pemain

DCS harus memiliki 14 tombol level.
Tombol aktif harus glowing/berkedip sesuai level aktif.

---

## 14. Emergency Level 14

Emergency harus realistis dan fokus ke K3.

Tidak perlu ledakan berlebihan.
Skenario utama:
- Kebocoran asam sulfat atau steam leak
- Suara mendesis
- Uap/asap putih atau kuning
- Alarm pabrik
- Lampu merah
- Pemain DCS harus radio evakuasi
- Pemain menekan tombol ESD
- Valve asam dan steam tertutup
- Pompa mati
- Asap berhenti
- Skenario berhasil

Jika gagal:
- Paparan kimia melewati batas aman
- Sistem shutdown terlambat
- Skor turun
- Tidak perlu menampilkan gore/kematian

---

## 15. Sistem Skor

Skor per level dihitung dari:

- Kecepatan menyelesaikan quest
- Ketepatan voice command
- Kelengkapan inspeksi X-Ray
- Kesesuaian urutan SOP
- Ketepatan parameter DCS
- Kepatuhan APD/K3

Syarat lulus akhir: minimal 70%.

Output akhir:
- Rapor K3
- Grade
- Sertifikat virtual operator HPAL

---

## 16. Aturan Kerja untuk Olivia AI

Olivia wajib mengikuti aturan ini:

1. Jawab dalam Bahasa Indonesia.
2. Jangan menghapus file tanpa izin.
3. Jangan mengubah ProjectSettings tanpa izin.
4. Jangan rename folder besar tanpa izin.
5. Jangan mengubah banyak script sekaligus.
6. Sebelum edit file, jelaskan rencana perubahan.
7. Setelah edit file, tampilkan daftar file yang berubah.
8. Prioritaskan perubahan kecil, aman, dan bertahap.
9. Jika menemukan error, jelaskan penyebab dan solusi.
10. Jika butuh membuat script baru, jelaskan nama script, fungsi, dan lokasi file.
11. Fokus pada Unity, C#, VR, HPAL, DCS, APD, K3, MCP server, GitHub, dan dokumentasi.
12. Jangan membuat sistem terlalu kompleks jika versi sederhana sudah cukup.
13. Utamakan greybox-first: fungsional dulu, visual polish belakangan.

---

## 17. Dokumen Referensi Project

Olivia harus membaca dokumen berikut jika tersedia:

- Olivia_Roadmap.md
- Olivia_Blueprint_Final.md
- BreakdownSistem.md
- HPAL_DeepResearch.md
- olivia_power_up_advice.md
- PROJECT_CONTEXT.md

Urutan baca yang disarankan:
1. PROJECT_CONTEXT.md
2. Olivia_Roadmap.md
3. Olivia_Blueprint_Final.md
4. BreakdownSistem.md
5. HPAL_DeepResearch.md
6. olivia_power_up_advice.md

---

## 18. Instruksi Audit Awal untuk Olivia

Saat pertama kali membaca project ini, Olivia harus melakukan audit, bukan langsung edit.

Audit awal:
1. Baca PROJECT_CONTEXT.md.
2. Baca README jika ada.
3. Cek struktur folder root.
4. Cek folder Assets.
5. Cek folder Packages.
6. Cek ProjectSettings.
7. Cari semua file .cs.
8. Cari semua scene .unity.
9. Identifikasi script yang sudah ada.
10. Identifikasi sistem yang belum ada.
11. Berikan ringkasan kondisi project.
12. Buat rekomendasi langkah lanjutan.

Command aman yang boleh digunakan:
- pwd
- ls
- find Assets -maxdepth 2 -type d
- find Assets -name "*.cs"
- find Assets -name "*.unity"
- cat Packages/manifest.json
- git status

---

## 19. Prompt Awal untuk Olivia

Gunakan prompt ini setelah membuka project:

"Olivia, kamu sekarang berada di folder project Unity VR HPAL saya. Baca PROJECT_CONTEXT.md dan dokumen referensi project. Jangan edit file dulu. Audit struktur project, scene, script, package, dan status Git. Setelah itu berikan ringkasan kondisi project, sistem yang sudah ada, sistem yang belum ada, risiko teknis, dan rekomendasi prioritas 7 hari ke depan."