# 🏭 OLIVIA — Blueprint Final
## Simulasi VR: Pengendalian Reaktor HPAL & Limbah Tailing B3

> Dokumen lengkap yang menggabungkan **rincian teknis industri HPAL**, **alur gameplay VR**, dan **strategi untuk memenangkan lomba**. Disusun sebagai panduan utama pengembangan proyek.

---

# 📖 BAGIAN 1 — DASAR TEKNIS INDUSTRI HPAL

## 1.1 Apa Itu HPAL?

**HPAL (High-Pressure Acid Leaching)** adalah metode hidrometalurgi canggih yang menggunakan cairan kimia, suhu tinggi, dan tekanan tinggi untuk mengekstrak **nikel** serta **kobalt** dari bijih nikel kadar rendah (limonit).

| Parameter | Nilai |
|-----------|-------|
| Suhu operasional | 240°C – 270°C |
| Tekanan operasional | 40 – 60 Bar |
| Bahan kimia utama | Asam Sulfat (H₂SO₄) |
| Bahan baku | Bijih nikel laterit (limonit) |
| Waktu tinggal di reaktor | ± 60 menit |

---

## 1.2 Proses Awal: Persiapan Material (Benefisiasi)

Sebelum masuk ke reaktor utama, bahan baku harus dipersiapkan melalui 3 langkah:

### 🪨 Penghancuran Material
Proses dimulai dengan menambang bijih nikel laterit, yang kemudian **dihancurkan dan disaring** menjadi partikel halus.

### 💧 Pembuatan Slurry
Material halus dicampur dengan air untuk membentuk **bubur lumpur pekat** yang disebut **slurry**.

### 🔥 Pemanasan Awal
Sebelum dipompa masuk ke reaktor utama, slurry dipanaskan terlebih dahulu menggunakan **uap (steam) sisa** untuk menghemat energi.

```
Alur Persiapan:
Tambang Batu Nikel → Hancurkan jadi halus → Campur air → SLURRY → Panaskan dengan uap sisa
```

---

## 1.3 Alat Utama: Reaktor Autoclave

Jantung dari fasilitas HPAL adalah **Reaktor Autoclave**. Alat ini bukan tungku pembakaran dengan api, melainkan semacam **"panci presto" kimia raksasa** bertekanan sangat tinggi.

### Bentuk & Material Konstruksi
- Berbentuk **silinder horizontal raksasa**
- Terbuat dari **baja karbon tebal**
- Bagian dalam dilapisi **Titanium murni** atau **batu bata tahan api khusus** (karena campurannya sangat korosif dan panas)

### Komponen Internal
- **Kompartemen** yang dipisahkan oleh dinding penyekat (**baffles**)
- **Baling-baling pengaduk (agitator)** berbahan titanium yang berputar konstan untuk mencampur slurry secara merata

### Mekanisme Reaksi
Slurry yang sudah dipanaskan **dipompa masuk** ke dalam autoclave, kemudian **Asam Sulfat (H₂SO₄) disuntikkan**. Pada suhu dan tekanan tinggi, nikel dan kobalt **larut terpisah** dari batuan.

### Parameter Operasional (KRITIS)

| Parameter | Nilai Optimal | Batas Bahaya |
|-----------|--------------|-------------|
| Suhu | 250°C | > 270°C |
| Tekanan | 50 Bar | > 65 Bar |

### Sistem Pendinginan (Quench Water)
Reaksi pelarutan asam bersifat **eksotermik** (menghasilkan panasnya sendiri). Jika suhu terus melonjak, sistem harus **secara otomatis menyuntikkan air pendingin (quench water)** ke dalam reaktor untuk mencegah suhu melampaui batas aman.

### Tantangan Operasional Utama: KERAK (Scale)
Reaksi ini menghasilkan produk padat (seperti **hematit** dan **alunit**) yang rentan membentuk **kerak tebal (scale)** pada dinding dan bilah agitator titanium. Dampaknya:
- ❌ Mengganggu aliran cairan
- ❌ Merusak pengaduk
- ❌ Menyumbat katup tekanan

---

## 1.4 Alat Lanjutan: Flash Vessel & Tata Kelola Limbah

### Flash Vessels (Penurun Tekanan)
Setelah ± 60 menit di dalam autoclave, slurry nikel cair yang sangat panas dan bertekanan tinggi **tidak bisa dikeluarkan langsung** ke udara terbuka. Slurry harus dialirkan melalui serangkaian tangki yang disebut **Flash Vessels** (stasiun letdown) untuk menurunkan suhu dan tekanannya **secara bertahap** kembali ke kondisi atmosfer normal (1 atm).

```
Autoclave (250°C, 50 Bar) → Flash Vessel 1 → Flash Vessel 2 → ... → Kondisi Normal (1 atm)
```

### Limbah Tailing (B3) ☠️
Setelah nikel dan kobalt berhasil diekstraksi, **sekitar 99% sisa material** menjadi limbah bubur asam (**acid tailings**).

| Aspek | Detail |
|-------|--------|
| Kategori | Limbah Bahan Berbahaya dan Beracun (B3) |
| Kandungan | Logam berat + asam sulfat |
| Penanganan | Dinetralkan → disaring kadar air → disimpan ketat |
| Fasilitas penyimpanan | Bendungan tailing atau Dry Stack Tailings Facility (DSTF) |
| Risiko | Bencana lingkungan jika tidak ditangani dengan benar |

---

# 🎮 BAGIAN 2 — ALUR GAMEPLAY VR

Di dalam aplikasi VR, pemain ditempatkan di tengah operasional pabrik dan diuji kesigapannya dalam mengatasi malfungsi industri skala besar.

---

## Fase 0 — Tutorial & Onboarding (BARU ✨)

> Fase perkenalan sebelum simulasi utama dimulai.

### Visual
- Ruang virtual netral dengan hologram 3D interaktif

### Aktivitas
- **Narasi voice-over** menjelaskan: *"Selamat datang di pabrik HPAL..."*
- Hologram 3D menunjukkan **alur proses** dari batu → nikel
- Pemain bisa **mengangkat dan memegang** model 3D bijih nikel, autoclave mini, dll
- Pengenalan **kontrol VR** dasar (grip, trigger, teleport)

### Durasi
- 2-3 menit, **bisa di-skip** untuk pemain berpengalaman

### Tujuan
- Memastikan semua pemain (termasuk juri) **memahami konteks** sebelum masuk simulasi

---

## Fase 1 — Ruang Kontrol DCS 🖥️

> Pemain bertugas sebagai operator DCS di ruang kendali pusat.

### Visual
- Ruang DCS (Distributed Control System) yang aman
- Dipenuhi **layar komputer, monitor HMI (Human-Machine Interface), dan panel alarm**
- Suasana tenang, profesional

### Dashboard Monitor Real-Time

```
┌──────────────────────────────────────────────┐
│  🏭 HPAL REACTOR MONITORING SYSTEM v2.1      │
├──────────┬──────────┬──────────┬─────────────┤
│ SUHU     │ TEKANAN  │ pH ASAM  │ ALIRAN      │
│ 248.7°C  │ 49.2 Bar │ 1.3      │ 12.4 m³/h   │
│ [██████] │ [█████░] │ [██░░░░] │ [███████░]  │
│ ✅ NORMAL │ ✅ NORMAL│ ⚠️ LOW  │ ✅ NORMAL    │
├──────────┴──────────┴──────────┴─────────────┤
│ AGITATOR RPM: 45.2  │  QUENCH: STANDBY       │
│ SCALE LEVEL: 23%    │  UPTIME: 847h          │
└──────────────────────────────────────────────┘
```

### Parameter yang Dimonitor
| Parameter | Fungsi | Target |
|-----------|--------|--------|
| Suhu | Temperatur reaktor | 250°C |
| Tekanan | Tekanan dalam autoclave | 50 Bar |
| pH Level | Keasaman cairan | Optimal range |
| Flow Rate | Laju aliran asam sulfat | Stabil |
| RPM Agitator | Kecepatan pengaduk | 45 RPM |
| Scale Level | Tingkat kerak | < 30% |

### Aktivitas Pemain
1. Mengawasi grafik indikator suhu dan tekanan
2. **Mengkalibrasi** laju aliran asam sulfat
3. Memastikan suhu bertahan di **250°C** dan tekanan stabil di **50 Bar**
4. Merespons jika ada parameter yang keluar batas normal

---

## Fase 1.5 — Persiapan APD (BARU ✨)

> Sebelum turun ke lantai pabrik, pemain WAJIB memakai Alat Pelindung Diri.

### Aktivitas
Pemain harus **mengambil dan memasang** APD secara fisik menggunakan controller VR:

| APD | Cara Pakai VR |
|-----|--------------|
| 🪖 Helm safety | Ambil dari rak → taruh di kepala |
| 🥽 Kacamata pelindung | Ambil → pasang di wajah |
| 🧤 Sarung tangan tahan panas | Ambil → pakai di kedua tangan |
| 👢 Sepatu safety | Otomatis (visual indicator) |

### Aturan
- Jika APD **tidak lengkap** → akses ke lantai pabrik **DITOLAK**
- Muncul penjelasan: *"Mengapa APD penting di fasilitas HPAL?"*
- Ini mengajarkan **K3 secara natural** tanpa terasa menggurui

---

## Fase 2 — Lantai Pabrik & X-Ray Vision 🔍

> Pemain turun ke lantai pabrik untuk inspeksi langsung pada Autoclave.

### Visual
- **Lantai pabrik (plant floor)** dengan suara mesin bising dan getaran ringan
- Berdiri tepat di depan **tabung Autoclave raksasa**
- Uap/asap tipis keluar dari pipa, lampu industri kuning-oranye

### Aktivitas
1. Menggunakan controller VR, pemain mengaktifkan mode **"X-Ray Vision"**
2. Dinding baja pelindung autoclave berubah **transparan secara holografis**
3. Pemain melihat **agitator titanium** berputar di dalam cairan asam
4. Pemain harus **mencari area penumpukan kerak (scale)** berlebih
5. **Menandai** area rusak dalam sistem pemeliharaan pabrik

### Interaksi VR
| Aksi | Gesture |
|------|---------|
| Aktifkan X-Ray | Tekan tombol khusus di controller |
| Zoom inspeksi | Pinch gesture (cubit) |
| Tandai kerak | Point & confirm |
| Rotasi pandangan | Putar kepala (head tracking) |

---

## Fase 3 — Skenario Kebocoran & Tanggap Darurat 🚨

> **KLIMAKS FINAL** — Situasi darurat yang menguji kecepatan dan ketepatan pemain.

### Kondisi Bahaya
- Katup penurun tekanan (**letdown valve**) **macet** karena terganjal kerak tebal
- Lampu VR berubah **MERAH**
- **Sirine tanda bahaya** berbunyi kencang
- Monitor menunjukkan tekanan melonjak drastis melewati **65 Bar**

### Aktivitas Penyelamatan

```
⚠️  ALARM: KATUP MACET — KERAK MENYUMBAT
     ↓
📈  Tekanan naik > 65 Bar (ZONA BAHAYA!)
     ↓
🔴  Lampu merah, sirine, alarm blaring
     ↓
⏱️  COUNTDOWN: 45 DETIK
     ↓
🏃  Pemain harus:
     ├── 1. Berlari menyusuri jalur pipa
     ├── 2. Mencari & memutar tuas ISOLATION VALVE
     └── 3. Menekan tombol EMERGENCY SHUT-DOWN (ESD)
```

### Interaksi VR Detail
| Aksi | Implementasi |
|------|-------------|
| Putar isolation valve | **Grip + putar tangan** searah jarum jam |
| Tekan tombol ESD | **Pecahkan kaca pelindung** dulu → baru tekan tombol |
| Navigasi | Berlari virtual / teleport menyusuri jalur pipa |

### Skenario Dinamis (Bukan Scripted!)
| Aspek | Implementasi |
|-------|-------------|
| Lokasi kebocoran | **Random** — berbeda setiap kali main |
| Kecepatan tekanan naik | Tergantung **reaksi pemain** — lambat bereaksi = naik lebih cepat |
| Tingkat kerak | **Tumbuh progresif** sepanjang simulasi |
| Difficulty | **Adaptif** — makin jago, makin menantang |

---

## Hasil: Berhasil ✅

### Sistem Rapor & Sertifikat

```
╔══════════════════════════════════════╗
║   SERTIFIKAT KOMPETENSI K3          ║
║   OPERATOR REAKTOR HPAL             ║
╠══════════════════════════════════════╣
║ Nama      : [Input pemain]          ║
║ Tanggal   : [Auto-generated]        ║
║ Skor      : 87/100 ⭐⭐⭐⭐           ║
╠══════════════════════════════════════╣
║ Waktu Tanggap    : 12.3 dtk  (A)    ║
║ Akurasi Katup    : 94%       (A+)   ║
║ Kepatuhan K3     : 78%       (B+)   ║
║ Inspeksi Scale   : 100%      (A+)   ║
╠══════════════════════════════════════╣
║ GRADE KESELURUHAN: A-               ║
║ STATUS: LULUS ✅                     ║
╚══════════════════════════════════════╝
```

### Metrik Penilaian
| Metrik | Bobot | Penjelasan |
|--------|-------|-----------|
| Waktu tanggap darurat | 30% | Seberapa cepat pemain merespons alarm |
| Ketepatan memutar katup | 25% | Akurasi gerakan putar valve |
| Kepatuhan K3 | 25% | APD lengkap, prosedur benar |
| Inspeksi scale | 20% | Ketepatan menandai area kerak |

### Fitur Tambahan
- Bisa di-**screenshot** atau **export** sebagai gambar
- Ada **ranking/leaderboard** untuk beberapa pemain
- **Replay value** — pemain ingin main lagi untuk skor lebih tinggi

---

## Hasil: Gagal ❌

### Konsekuensi Kegagalan
Jika pemain gagal menekan tombol ESD tepat waktu:
- Pipa **meledak**
- Limbah tailing asam bersuhu **250°C menyembur** ke area pabrik

### Visualisasi Dampak Lingkungan (Cutscene 15 detik)
```
Pipa meledak → Tailing menyembur →
   ↓
Cutscene:
   - Limbah mengalir ke sungai terdekat
   - Air sungai berubah warna
   - Ikan-ikan mati mengambang
   - Warga desa terlihat panik
   - Teks: "2,400 hektar lahan pertanian tercemar"
   - Teks: "Butuh 25 tahun untuk pemulihan"
```

### Tujuan
Bukan untuk menakuti, tapi untuk **menunjukkan stakes nyata** dari kelalaian operasional. Poin edukasi lingkungan yang sangat kuat.

---

# 🎬 BAGIAN 3 — ELEMEN PENDUKUNG

## 3.1 Opening Cinematic (30 detik)

```
[Layar hitam]
Teks: "Indonesia adalah produsen nikel terbesar dunia."

[Aerial shot pabrik HPAL di tengah hutan tropis]
Teks: "Setiap tahun, ribuan pekerja menghadapi risiko di fasilitas HPAL."

[Close-up autoclave, uap mengepul]
Teks: "Satu kesalahan kecil bisa mengubah segalanya."

[Fade to white]
Teks: "Apakah kamu siap?"

[OLIVIA — HPAL Safety Training Simulator]
[Tekan trigger untuk mulai]
```

---

## 3.2 Narasi Suara & Subtitle

| Bahasa | Fungsi |
|--------|--------|
| 🇮🇩 Indonesia | Narasi utama (voice-over) |
| 🇬🇧 English | Subtitle (toggle on/off) |

- Gunakan AI TTS jika belum ada voice actor
- Suara serius, profesional, gaya narrator dokumenter industri
- Menunjukkan proyek **siap untuk audiens internasional**

---

## 3.3 Sound Design

| Situasi | Sound Design |
|---------|-------------|
| Ruang Kontrol (Fase 1) | Dengungan AC, beep monitor, keyboard |
| Lantai Pabrik (Fase 2) | Mesin berderu, pipa bergetar, langkah kaki di metal grating |
| X-Ray Mode | Suara sci-fi futuristik, hum energi |
| ALARM! (Fase 3) | Sirine industri, detak jantung makin kencang |
| Berhasil | Sound relief, musik heroik singkat |
| Gagal | Ledakan, gas mendesis, silence → dampak |

> **PENTING:** Gunakan **Spatial Audio (3D Audio)** — suara datang dari arah sumber. Mesin di kiri, terdengar dari kiri. Ini standar VR modern.

---

## 3.4 Visual Atmosphere

- **Pencahayaan:** Lampu industri kuning-oranye, bayangan keras, area gelap di sudut
- **Partikel:** Uap/asap tipis keluar dari pipa, percikan api las di kejauhan
- **Cuaca:** Langit mendung, awan bergerak (area outdoor)
- **Detail kecil:** Stiker peringatan K3, pemadam kebakaran, rambu-rambu safety

---

## 3.5 Mode Studi Kasus (Menu Ekstra)

Tampilkan 2-3 **kasus kecelakaan HPAL/smelter dunia nyata**:
- Lokasi, tahun, penyebab, dampak
- Analisis: apa yang salah, bagaimana seharusnya
- Hubungkan ke skenario gameplay: *"Di simulasi ini, kamu baru saja mencegah insiden serupa!"*

> Menunjukkan riset mendalam, bukan cuma bikin game.

---

# 🏆 BAGIAN 4 — STRATEGI PRESENTASI & LOMBA

## 4.1 Framing yang Kuat

**JANGAN** presentasikan sebagai: *"Kami membuat game VR"*

**PRESENTASIKAN** sebagai:
> *"Kami mengembangkan industrial training simulator berbasis VR yang mensimulasikan operasional reaktor HPAL untuk melatih kesiapan tanggap darurat operator pabrik nikel, sekaligus meningkatkan kesadaran dampak lingkungan dari limbah B3."*

---

## 4.2 Poin Highlight untuk Juri

1. ✅ **Relevansi nasional** — Indonesia penghasil nikel terbesar, banyak pabrik HPAL baru dibangun
2. ✅ **Solusi nyata** — Bisa dipakai beneran untuk training operator baru
3. ✅ **Keselamatan** — Operator bisa latihan di VR tanpa risiko cedera
4. ✅ **Edukasi lingkungan** — Menunjukkan dampak nyata limbah tailing
5. ✅ **Teknologi canggih** — VR + fisika dinamis + spatial audio + X-Ray vision

---

## 4.3 Kata Kunci Power untuk Proposal

- *"Immersive industrial training"*
- *"Hazard awareness through experiential learning"*
- *"Dynamic scenario generation"*
- *"Real-time monitoring simulation"*
- *"Environmental impact visualization"*
- *"Occupational safety compliance training"*

---

# 📋 BAGIAN 5 — PRIORITAS IMPLEMENTASI

Kerjakan sesuai urutan prioritas:

| Prioritas | Fitur | Estimasi |
|-----------|-------|----------|
| 🔴 **P0 — WAJIB** | 3 Fase utama berjalan (Kontrol → Inspeksi → Darurat) | Inti proyek |
| 🔴 **P0 — WAJIB** | Dashboard monitor real-time | 2-3 hari |
| 🔴 **P0 — WAJIB** | Sound design & alarm system | 1-2 hari |
| 🟡 **P1 — PENTING** | Tutorial/Onboarding (Fase 0) | 1-2 hari |
| 🟡 **P1 — PENTING** | Sistem skor + sertifikat K3 | 1 hari |
| 🟡 **P1 — PENTING** | Wajib pakai APD sebelum masuk pabrik | 1 hari |
| 🟡 **P1 — PENTING** | Opening cinematic | 1 hari |
| 🟢 **P2 — NILAI TAMBAH** | Skenario dinamis (random kebocoran) | 2-3 hari |
| 🟢 **P2 — NILAI TAMBAH** | Visualisasi dampak lingkungan (cutscene gagal) | 2 hari |
| 🟢 **P2 — NILAI TAMBAH** | Narasi voice-over + subtitle bilingual | 1-2 hari |
| 🔵 **P3 — BONUS** | Mode studi kasus kejadian nyata | 1 hari |
| 🔵 **P3 — BONUS** | Interaksi tangan VR advanced | 2-3 hari |

---

# 📊 RINGKASAN PROYEK

| Aspek | Detail |
|-------|--------|
| **Nama** | OLIVIA |
| **Tema** | Industri HPAL (pengolahan nikel) |
| **Platform** | VR (Virtual Reality) |
| **Peran pemain** | Operator pabrik / DCS Operator |
| **Tujuan utama** | Edukasi K3 & pengendalian reaktor |
| **Jumlah fase** | 5 (Tutorial → Kontrol → APD → Inspeksi → Darurat) |
| **Klimaks** | Skenario darurat kebocoran reaktor |
| **Output akhir** | Skor evaluasi & Sertifikat K3 |
| **Diferensiasi** | Skenario dinamis, X-Ray Vision, dampak lingkungan |

---

> 💡 *Yang membedakan proyek **bagus** dengan proyek **juara** bukan jumlah fitur, tapi **kedalaman dan polish**. Lebih baik punya 3 fase yang sangat polished daripada 10 fitur setengah jadi. Buat juri lupa mereka pakai VR headset.*
>
> *"Simplicity is the ultimate sophistication."* — Leonardo da Vinci
