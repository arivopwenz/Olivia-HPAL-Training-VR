# 🏭 OLIVIA VR — Blueprint Final v3.0
## Sistem Level-Based HPAL Process Simulation

> **Konsep Utama:** Setiap Level = Satu Tahap dalam Flowsheet HPAL Nyata (Total 14 Level)
> **Mekanisme Unik:** X-Ray/Invisible View, Voice Walkie-Talkie Wajib (dengan balasan suara NPC), Perspective Shift DCS ↔ Lapangan, Sinkronisasi Parameter DCS dengan Realita Lapangan.
> **Patokan Industri:** Flowsheet 14-Titik (Crusher → Dry Stack Tailings → K3 Emergency)

---

## 🗺️ Peta Dunia Game (3 Zona Bisa Dijelajahi)

| Zona | Nama | Isi |
|------|------|-----|
| 🔵 A | DCS Control Room | Monitor layar besar, **14 Tombol Sistem (sesuai level)**, Walkie Talkie, Tombol ESD |
| 🟠 B | Lantai Pabrik (Atas) | Autoclave, pipa steam, platform, tangga, catwalk |
| 🟢 C | Lantai Pabrik (Bawah) | Crusher, Slurry Tank, Pump, Flash Vessel, CCD, MHP, Tailing |

---

## 📋 Sistem Level Lengkap (Level 0 - 14)

**PENTING: Di setiap level, pemain WAJIB melakukan laporan via Walkie Talkie setelah menyelesaikan tugas, dan akan selalu ada suara balasan (MP3/WAV) dari operator NPC (DCS/Field) sebagai konfirmasi.**

---

### ⚫ LEVEL 0 — Tutorial VR (Mekanik Dasar)
**Lokasi:** Ruang Briefing / Lobby  
**Tujuan:** Pemain belajar cara mengontrol game sebelum masuk area pabrik.

**Materi Tutorial:**
1. Cara berjalan (Joystick Continuous Move)
2. Cara grab/megang objek (XR Grab)
3. Cara menggunakan Walkie Talkie (Grab HT + tekan PTT + bicara) → Terdengar balasan audio instruktur.
4. Cara membaca UI Hologram/Outline.

---

### 🟢 LEVEL 1 — Persiapan APD (Safety Zone)
**Lokasi:** Ruang Loker  
**Peran:** Operator Baru  
**Checklist Quest:** Pakai 7 item APD: Helm, Rompi, Kacamata, Sepatu, Sarung Tangan, **Masker/Respirator**, dan **Walkie Talkie**.
**Selesai jika:** Lapor HT: *"DCS, APD lengkap."* Balasan: *"Copy, pintu Safety Gate terbuka."*

---

### 🔵 LEVEL 2 — DCS: Persiapan Menghidupkan Mesin
**Lokasi:** DCS Control Room  
**Peran:** DCS Operator  
**Mekanik DCS Panel:** Panel utama DCS memiliki **14 Tombol**. Pada level ini, belum ada tombol mesin yang ditekan. Pemain hanya cek parameter awal.
**Selesai jika:** Lapor HT: *"Field, siapkan area Crusher."* Balasan audio: *"Siap, menuju Crusher."*

---

### 🟠 LEVEL 3 — Lapangan: Ore Masuk ke Slurry Tank
**Lokasi:** Area Crusher & Slurry Tank  
**Peran:** Field Worker  
**Quest:** X-Ray View di Crusher & Slurry Tank. Tunggu cairan 25%.
**Selesai jika:** Lapor HT: *"Ore masuk ke Slurry Tank, cairan 25%."* Balasan DCS: *"Copy, standby untuk aktivasi Slurry Pump."*

---

### 🔵 LEVEL 4 — DCS: Aktifkan Slurry Pump + Pengaturan Flow Rate
**Lokasi:** DCS Control Room  
**Peran:** DCS Operator  
**Mekanik DCS Panel:** Tombol ke-4 (**Slurry Pump**) di panel DCS akan berkedip. Setelah mesin menyala, pemain harus menekan **tombol [+] atau [-]** di bawah monitor mini untuk mengatur laju aliran sesuai Standard Operating Procedure (SOP).
**Target SOP Pabrik:** Flow Rate Slurry harus tepat di **450 m³/h**.
**Sinkronisasi Visual:** Kecepatan animasi aliran cairan di lapangan akan **100% sinkron** dengan angka di DCS monitor mini.
**Selesai jika:** Angka di monitor mini mencapai ± 450 m³/h + Lapor HT: *"Slurry Pump aktif, flow rate diset 450 meter kubik per jam."* Balasan Field: *"Copy, memantau aliran ke Pre-heater."*

---

### 🟠 LEVEL 5 — Lapangan: Buka Katup Steam & Pre-heater
**Lokasi:** Area Pre-heater  
**Quest:** Putar Valve Steam 100%. X-Ray Pre-heater (suhu naik 180°C).
**Selesai jika:** Valve diputar + Lapor HT: *"Katup steam terbuka, suhu naik."* Balasan DCS: *"Copy, bersiap untuk injeksi asam."*

---

### 🔵 LEVEL 6 — DCS: Pengaturan Presisi Injeksi Asam (Acid Injection)
**Lokasi:** DCS Control Room  
**Quest:** Tekan tombol ke-6 (**Acid Injection**). Sebuah UI panel kontrol presisi (monitor mini dengan tombol [+] dan [-]) akan muncul. Pemain harus memasukkan rasio injeksi asam sulfat (H₂SO₄) sesuai patokan pabrik.
**Target SOP Pabrik:** Dosis Asam Sulfat = **350 kg/ton bijih**.
Jika dosis diset tepat dengan tombol, nilai pH akan turun perlahan dan stabil di **pH 1.0**.
**Selesai jika:** Dosis diset 350 kg/ton + pH 1.0 + Lapor HT: *"Acid Injection aktif, rasio 350 kg per ton, pH 1.0."* Balasan Field: *"Copy, aman masuk Autoclave."*

---

### 🟠 LEVEL 7 — Lapangan: Monitor Parameter Autoclave
**Lokasi:** Samping Autoclave Raksasa  
**Quest:** X-Ray Autoclave. Lihat agitator berputar, slurry, dan perubahan warna kimia. Pemain lapangan harus membaca indikator analog di mesin dan memastikan sesuai dengan patokan.
**Target SOP Pabrik (Autoclave):** 
- **Tekanan Atmosfer (Pressure):** 45 - 50 atm
- **Suhu (Temperature):** 250°C - 255°C
- **Putaran Agitator:** 60 RPM
**Selesai jika:** Lapor HT: *"Autoclave normal, Suhu 250 derajat, Tekanan 50 atm, Agitator 60 RPM."* Balasan DCS: *"Copy, parameter sesuai SOP, lanjut monitoring ketat."*

---

### 🔵 LEVEL 8 — DCS: Monitoring Ketat & Koreksi Parameter
**Lokasi:** DCS Control Room  
**Quest:** Pantau parameter selama 60 detik. Secara acak, **RPM Agitator akan drop ke 40 RPM** atau **Tekanan naik ke 53 atm**. Pemain harus cepat menekan tombol **[+] atau [-]** di monitor mini koreksi DCS untuk mengembalikan nilai ke Target SOP (Suhu 250°C, Tekanan 50 atm, RPM 60).
**Selesai jika:** Lapor HT: *"Parameter terkoreksi dan stabil di angka SOP."* Balasan: *"Copy, proses optimal."*

---

### 🟠 LEVEL 9 — Lapangan: Letdown & Flash Vessel
**Lokasi:** Area Letdown Valve & Flash Vessel  
**Quest:** X-Ray Flash Vessel, lihat tekanan turun ke 12 atm. Uap keluar.
**Selesai jika:** Lapor HT: *"Flash Vessel normal, tekanan 12 atm."* Balasan DCS: *"Copy, siap ke CCD."*

---

### 🔵 LEVEL 10 — DCS: Aktifkan CCD Separator
**Lokasi:** DCS Control Room  
**Quest:** Tekan tombol ke-10 (**CCD Separator**) yang berkedip.
**Selesai jika:** Lapor HT: *"CCD aktif, PLS mengalir."* Balasan Field: *"Copy, menuju area presipitasi."*

---

### 🟠 LEVEL 11 — Lapangan: MHP Precipitation
**Lokasi:** Area MHP Tank  
**Quest:** X-Ray MHP Tank (endapan hijau). Ambil botol sampel.
**Selesai jika:** Sampel diambil + Lapor HT: *"MHP terbentuk, produk normal."* Balasan DCS: *"Copy, proses produksi utama selesai."*

---

### 🟢 LEVEL 12 — DCS: Mengalirkan Limbah ke Tailing
**Lokasi:** DCS Control Room  
**Quest:** Tekan tombol ke-12 (**Tailing Discharge**) untuk mengalirkan sisa limbah asam ke area pengolahan. 
**Selesai jika:** Lapor HT: *"Limbah dialirkan ke area Tailing."* Balasan Field: *"Copy, siap melakukan netralisasi."*

---

### 🟢 LEVEL 13 — Lapangan: Tailing & Waste Management (Immersive Learning Limbah)
**Lokasi:** Area Tailing (Suasana berbeda, label B3)  
**Peran:** Field Worker  
**Fokus Immersive:** Edukasi cara mengolah limbah B3 nikel agar aman bagi lingkungan.
**Quest:**
1. Cek indikator pH Tailing (awalnya asam, < 3.0).
2. Grab karung/ember **Kapur (Limestone)** dan tuang ke tangki netralisasi.
3. Tunggu pH naik menjadi **8.0 - 9.0** (Aman).
4. Tekan tombol lokal untuk mengaktifkan **Filter Press**. X-Ray Filter Press: cairan dipisahkan dari lumpur padat (Tailing Cake).
5. Konfirmasi tailing cake jatuh ke area Dry Stack Storage yang aman.
**Selesai jika:** pH netral + Filter Press jalan + Lapor HT: *"Netralisasi berhasil, pH 8.5. Tailing aman di Dry Stack."* Balasan DCS: *"Copy, lingkungan aman."*

---

### 🔴 LEVEL 14 — DARURAT: Situasi K3 & Kebocoran Sistem (REALISTIS)
**Lokasi:** DCS Control Room  
**Pemicu:** Tiba-tiba saat operasional normal.  
**Situasi Realistis (TIDAK ADA LEDAKAN):**
- Terjadi **Kebocoran Pipa Asam Sulfat (H2SO4)** atau **Overpressure Uap Panas (Steam Leak)**.
- Suara mendesis keras, asap putih pekat / uap kimia menyebar di lantai pabrik.
- Alarm K3 berbunyi. Lampu merah berkedip.
- Bar kesehatan/safety field worker (jika ada di area itu) akan terancam jika tidak pakai Respirator.
**Quest Darurat (SOP K3 Nasional/Internasional):**
1. **[DCS]** Acknowledge alarm kebocoran.
2. **[DCS]** Ambil Walkie Talkie: *"EMERGENCY! Kebocoran Asam di Sektor 2! Semua personel evakuasi!"* Balasan audio panik dari lapangan: *"Copy, kami evakuasi sekarang!"*
3. **[DCS]** Cari dan tekan tombol **ESD (Emergency Shutdown)** di DCS panel.
4. Semua valve input asam dan steam akan menutup otomatis. Pompa mati.
**Ending BERHASIL:** Kebocoran berhenti, uap menghilang perlahan. *"SISTEM AMAN. Evakuasi berhasil, tidak ada korban."*  
**Ending GAGAL:** Terlambat menekan ESD. Sistem shutdown otomatis namun dengan damage tinggi. *"KEGAGALAN SISTEM. Paparan kimia melewati batas aman."*

---

## ⚙️ Breakdown Mekanisme Mesin Utama

1. **Slurry Pump (Pompa Lumpur):** Menghisap campuran bijih nikel dan air. Menggunakan variabel **Flow Rate (m³/h)**. Laju aliran di DCS mengontrol kecepatan putaran baling-baling animasi dan kecepatan mengalirnya partikel cairan di pipa secara real-time.
2. **Pre-Heater (Pemanas Awal):** Memanfaatkan injeksi uap panas (Steam). Dioperasikan dengan memutar **Rotary Valve** fisik menggunakan tangan VR. Variabel utamanya adalah **Temperature (°C)**.
5. **Autoclave (Reaktor Jantung HPAL):** Silinder bertekanan tinggi berbahan Titanium. Menggunakan agitator berputar cepat. Variabel: **Tekanan Atmosfer (atm)**, **Suhu (°C)**, dan **RPM Agitator**. Reaksi kimia (pelindian) terjadi di sini mengubah nikel padat menjadi larutan cair (PLS).
6. **Flash Vessel:** Penurun tekanan cepat. Mengubah tekanan ekstrem Autoclave (50 atm) kembali mendekati normal (1-12 atm) dengan cara melepaskan sebagian besar cairan menjadi uap (Flash Steam).
5. **CCD (Counter Current Decantation):** Tangki pemisah gravitasi raksasa. Memisahkan cairan kaya nikel (PLS) yang jernih di atas, dari sisa lumpur padat yang mengendap di bawah.
6. **Filter Press (Pengolah Limbah):** Mesin pres hidrolik berlapis-lapis. Memeras sisa lumpur asam (setelah dinetralkan kapur) menjadi "kue" kering (Tailing Cake) agar airnya bisa didaur ulang dan padatannya aman ditumpuk (Dry Stack).

---

## 🏆 Sistem Skor Per Level
| Aspek | Bobot |
|-------|-------|
| Kecepatan selesaikan quest | 25% |
| Ketepatan Voice Command | 25% |
| Kelengkapan inspeksi (X-Ray) | 25% |
| Kesesuaian Urutan SOP | 25% |
**Nilai Akhir:** Rata-rata semua level. Syarat lulus ≥ 70% (Mendapat Sertifikat K3 Virtual).
