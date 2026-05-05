# 🏭 OLIVIA: HPAL Safety Training Simulator

![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?style=flat&logo=unity)
![VR](https://img.shields.io/badge/VR-Ready-green?style=flat&logo=virtual-reality)

**OLIVIA** adalah sebuah aplikasi simulasi Virtual Reality (VR) yang dirancang untuk pelatihan Keselamatan dan Kesehatan Kerja (K3) pada fasilitas industri pengolahan nikel menggunakan metode *High-Pressure Acid Leaching (HPAL)*.

Proyek ini dikembangkan untuk lomba dan berfokus pada pengalaman imersif dalam mengoperasikan reaktor Autoclave dan menangani skenario darurat.

## 🎯 Fitur Utama

- **Fase 0: Onboarding** - Pembelajaran interaktif mengenai proses HPAL.
- **Fase 1: DCS Control Room** - Simulasi kalibrasi indikator suhu (250°C) dan tekanan (50 Bar) secara real-time.
- **Fase 1.5: APD Check** - Sistem verifikasi penggunaan Alat Pelindung Diri sebelum memasuki lantai pabrik.
- **Fase 2: X-Ray Inspection** - Fitur *X-Ray Vision* untuk inspeksi kerak (scale) pada agitator di dalam reaktor.
- **Fase 3: Emergency Scenario** - Simulasi tekanan tinggi dinamis dengan alarm, berlari memutar *isolation valve*, dan mengaktifkan *Emergency Shut-Down* (ESD).
- **Scoring System** - Evaluasi performa berdasarkan waktu tanggap, kepatuhan K3, dan akurasi tindakan.

## ⚙️ Persyaratan Sistem

- Unity Editor (dengan modul Android/PC Build Support)
- XR Interaction Toolkit
- Headset VR yang mendukung PCVR atau Standalone (Meta Quest 2/3/Pro, dll)

## 📂 Struktur Proyek Utama

Semua skrip utama dapat ditemukan di `Assets/Scripts/`:
* `Core/` - Pengaturan alur permainan dan transisi (`GameManager`, `SceneLoader`, `AudioManager`).
* `Phase1/` - Interaksi di ruang kontrol DCS.
* `Phase2/` - Fitur inspeksi lantai pabrik dan X-Ray.
* `Phase3/` - Interaksi katup dan tombol darurat.
* `UI/` - Sistem skor dan tampilan antarmuka.

---
*Proyek ini dikembangkan untuk meningkatkan kesadaran operasional dan pencegahan dampak lingkungan dari limbah tailing B3 industri pengolahan nikel.*
