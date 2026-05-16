# 🏭 HPAL DEEP RESEARCH — Panduan Teknis Komprehensif
## Untuk Proyek OLIVIA VR Simulator
> Disusun berdasarkan riset mendalam dari sumber teknis industri & K3 Indonesia.
> **Sumber:** calderaengineering.com, totalmateria.com, sucofindo.co.id, kumparan.com, tbpnickel.com, earthworks.org

---

# BAGIAN 1 — APA ITU HPAL & MENGAPA PENTING?

## 1.1 Definisi
**High Pressure Acid Leaching (HPAL)** adalah metode **hidrometalurgi** (pengolahan logam menggunakan cairan kimia) yang dirancang khusus untuk mengekstrak **Nikel (Ni)** dan **Kobalt (Co)** dari bijih nikel kadar rendah jenis **laterit limonit**.

Proses ini adalah tulang punggung industri baterai kendaraan listrik (EV) dunia, karena:
- Indonesia memiliki **~25% cadangan nikel dunia** — terbesar di planet ini
- Nikel dari HPAL menghasilkan **Nickel Sulfate (NiSO₄)** — bahan utama baterai lithium-ion
- Pabrik HPAL baru terus dibangun di Sulawesi, Maluku, dan Papua

| Parameter Kunci | Nilai |
|-----------------|-------|
| Suhu Operasional | **240°C – 270°C** |
| Tekanan Operasional | **40 – 60 Bar (4–6 MPa)** |
| Bahan Kimia Utama | **Asam Sulfat (H₂SO₄)** |
| Waktu Reaksi di Reaktor | **30 – 90 menit** |
| Bahan Baku | Bijih nikel laterit limonit (kadar Ni <1.5%) |
| Produk Akhir | Mixed Hydroxide Precipitate (MHP) → NiSO₄ |

---

# BAGIAN 2 — ALUR PROSES HPAL (STEP BY STEP)

## 🪨 TAHAP 1: Persiapan Material (Benefisiasi)

### Langkah 1.1 — Penambangan & Penghancuran
- Bijih nikel laterit ditambang secara open-pit
- Batu bijih dihancurkan dengan crusher menjadi partikel halus (<2mm)
- Disaring untuk memisahkan material kasar

### Langkah 1.2 — Pembuatan Slurry
- Partikel halus **dicampur air** membentuk bubur kental (**slurry**)
- Konsentrasi slurry: **25–45% padatan**
- Slurry ini yang akan menjadi "bahan masak" di reaktor

### Langkah 1.3 — Pemanasan Awal (Pre-heating)
- Slurry dipanaskan menggunakan **uap panas (steam)** sisa dari proses Flash Vessel
- Ini adalah sistem **daur ulang energi** — uap bekas autoclave dipakai lagi untuk memanaskan bahan masuk
- Slurry masuk ke autoclave sudah dalam kondisi panas (~180°C)

```
ALUR TAHAP 1:
Tambang → Crusher → Slurry Tank → Heat Exchanger → SIAP MASUK AUTOCLAVE
```

---

## ⚙️ TAHAP 2: Reaksi di Reaktor Autoclave (INTI PROSES)

> **Inilah "jantung" dari seluruh fasilitas HPAL**

### Apa itu Autoclave?
Bukan tungku biasa. Autoclave adalah **wadah baja bertekanan ultra-tinggi** — bayangkan "panci presto" berukuran 5 lantai gedung yang terbuat dari baja setebal 10cm.

### Konstruksi Fisik:
- Berbentuk **silinder horizontal raksasa** (panjang ~25–50 meter)
- Terbuat dari **baja karbon tebal**
- Seluruh bagian DALAM dilapisi **Titanium murni** — karena asam sulfat akan langsung menghabiskan baja biasa
- Di dalamnya ada sekat-sekat (**baffles**) yang membagi ruang menjadi beberapa kompartemen
- Di tiap kompartemen ada **baling-baling pengaduk (agitator)** dari Titanium yang berputar terus-menerus

### Proses Reaksi:
1. Slurry dipompa masuk dari ujung kiri autoclave
2. **Asam Sulfat (H₂SO₄) disuntikkan** ke dalam campuran
3. Di bawah suhu 250°C dan tekanan 50 Bar, reaksi kimia terjadi:
   - Nikel (Ni) → larut menjadi **Nickel Sulfate (NiSO₄)**
   - Kobalt (Co) → larut menjadi **Cobalt Sulfate (CoSO₄)**  
   - Besi (Fe) → mengendap menjadi **Hematit (Fe₂O₃)** — limbah padat
4. Slurry bergerak pelan dari kompartemen kiri ke kanan (~60 menit)
5. Keluar dari ujung kanan sebagai **cairan kaya nikel (Pregnant Leach Solution / PLS)**

### ⚠️ Parameter KRITIS yang Harus Dijaga:

| Parameter | Target Optimal | Batas BAHAYA |
|-----------|---------------|--------------|
| Suhu | **250°C** | > 270°C → reaksi tak terkendali |
| Tekanan | **50 Bar** | > 65 Bar → risiko ledakan |
| RPM Agitator | **45 RPM** | < 30 RPM → slurry mengendap |
| pH Cairan | **< 1.0** | > 2.0 → nikel tidak terlarut |

### 🔥 Masalah Utama: KERAK (Scale)
Reaksi kimia HPAL menghasilkan endapan padat (**hematit, alunit, jarosit**) yang menempel di dinding dan agitator. Kerak ini adalah **musuh nomor 1** operator HPAL:
- Menyumbat katup dan pipa
- Merusak bilah agitator titanium (sangat mahal!)
- Memaksa shutdown darurat jika terlalu tebal (>40%)

---

## 💨 TAHAP 3: Penurunan Tekanan (Flash Letdown)

### Mengapa Perlu Flash Vessel?
Slurry keluar autoclave dalam kondisi **250°C dan 50 Bar**. Kalau langsung dikeluarkan ke udara terbuka → **MELEDAK** (seperti membuka tutup panci presto yang masih panas).

### Proses Flash:
Slurry dialirkan melalui **3–4 Flash Vessel secara berurutan**:
```
Autoclave (250°C, 50 Bar)
    ↓
Flash Vessel 1 (190°C, 12 Bar) — uap sisa → daur ulang ke pre-heater
    ↓
Flash Vessel 2 (120°C, 3 Bar) — uap sisa → daur ulang
    ↓
Flash Vessel 3 (80°C, 1 Bar) — mendekati normal
    ↓
Kondisi Normal (< 80°C, 1 atm) — AMAN untuk diproses lanjut
```

---

## 🧪 TAHAP 4: Pemisahan & Pemurnian (CCD + Neutralisasi)

### Counter-Current Decantation (CCD):
- PLS (cairan kaya nikel) **dipisahkan** dari padatan limbah melalui serangkaian thickener
- Cairan bersih → lanjut ke tahap pemurnian
- Padatan → menjadi **Limbah Tailing**

### Pemurnian Impuritas:
Cairan PLS masih mengandung pengotor: besi, aluminium, kromium hexavalen (sangat beracun!). Proses netralisasi bertahap:
1. Tambahkan **Limestone (Batu Kapur / CaCO₃)** → naikkan pH 1.0 → 3.5 (endapkan besi)
2. Tambahkan **Kapur (CaO / Lime)** → naikkan pH 3.5 → 5.5 (endapkan aluminium)
3. Cairan bersih siap untuk **Metal Recovery**

---

## ⚗️ TAHAP 5: Produksi Produk Akhir

### Mixed Hydroxide Precipitate (MHP):
- Cairan nikel-kobalt murni ditambah **Magnesium Oksida (MgO)**
- Nikel dan kobalt mengendap menjadi **MHP** — bubuk/endapan berwarna hijau
- MHP dikirim ke pabrik refinery untuk diolah menjadi **Nickel Sulfate (NiSO₄)** bahan baterai EV

---

## ☠️ TAHAP 6: Pengelolaan Limbah Tailing B3

### Apa itu Tailing HPAL?
Setelah nikel diekstraksi, **99% sisa material** menjadi limbah. Limbah ini sangat berbahaya:

| Karakteristik | Detail |
|---------------|--------|
| Kategori | **B3 (Bahan Berbahaya & Beracun)** |
| pH | **2.0 – 4.0** (sangat asam) |
| Kandungan Berbahaya | Asam sulfat, Kromium Hexavalen (Cr⁶⁺), Arsen (As), logam berat lain |
| Suhu saat keluar | ~80°C |
| Volume | Sangat besar — hampir sama dengan volume bahan masuk |

### Proses Pengelolaan (Wajib sesuai regulasi KLHK Indonesia):
1. **Netralisasi:** Tambahkan kapur hingga pH mencapai **8.0 – 9.0** (netral-basa)
2. **Filtrasi:** Filter press untuk memisahkan air dari padatan
3. **WWTP (Water Treatment Plant):** Air buangan diproses di IPAL sebelum dibuang
4. **Penyimpanan:** Padatan tailing disimpan di:
   - **Wet Tailings Dam (Kolam Limbah)** — metode lama, berisiko bocor/longsor
   - **Dry Stack Tailings Facility (DSTF)** — metode modern, lebih aman

### 🌍 Risiko Lingkungan Jika Tailing Tidak Dikelola:
- Kontaminasi air tanah dan sungai oleh asam + logam berat
- Kegagalan bendungan tailing → pencemaran masif (seperti kasus Brasil 2019)
- Krisis ekosistem jangka panjang — butuh **25+ tahun** pemulihan

---

# BAGIAN 3 — K3 & SOP OPERATOR HPAL

## 3.1 Bahaya Utama di Area HPAL

| No | Bahaya | Risiko |
|----|--------|--------|
| 1 | **Asam Sulfat** | Luka bakar kimia, kerusakan paru-paru jika terhirup |
| 2 | **Suhu Ekstrem** | Luka bakar jika terjadi kebocoran pipa |
| 3 | **Tekanan Tinggi** | Ledakan katup atau pipa |
| 4 | **Kerak (Scale)** | Penyumbatan katup → tekanan melonjak dadakan |
| 5 | **Gas Beracun** | H₂S (hidrogen sulfida) dari reaksi kimia |
| 6 | **Kebisingan** | Mesin >85 dB → kerusakan pendengaran permanen |
| 7 | **Tailing Bocor** | Paparan logam berat Cr⁶⁺ dan asam |

## 3.2 APD Wajib Operator HPAL

### APD Dasar (Wajib di Semua Area):
- 🪖 **Safety Helmet** — pelindung kepala dari benturan & benda jatuh
- 👟 **Safety Boots** — pelindung kaki dari tumpahan asam & benda berat
- 🥽 **Safety Glasses** — pelindung mata dari percikan kimia
- 🦺 **Safety Vest** — visibilitas & pelindung dada ringan

### APD Khusus (Sesuai Area):
- 😷 **Respirator/Masker Gas** — area dengan uap asam atau H₂S
- 🧤 **Chemical-Resistant Gloves** — saat menyentuh pipa/peralatan basah asam
- 👂 **Ear Protection** — di dekat mesin dan pompa
- 🥼 **Chemical Suit (Apron)** — saat sampling atau maintenance autoclave

## 3.3 SOP Sebelum Masuk Area Pabrik (Pre-Entry Checklist)

```
✅ CHECKLIST WAJIB SEBELUM MASUK PLANT FLOOR:
□ 1. Briefing keselamatan harian (toolbox meeting)
□ 2. Cek status Work Permit (Izin Kerja)
□ 3. Pasang APD lengkap sesuai zona kerja
□ 4. Cek kondisi alat komunikasi (radio/HT)
□ 5. Ketahui lokasi Emergency Assembly Point (titik kumpul darurat)
□ 6. Ketahui lokasi alat pemadam terdekat
□ 7. Laporkan ke supervisor sebelum masuk
```

## 3.4 SOP Darurat (Emergency Response)

### Prosedur jika Tekanan Melebihi Batas (>65 Bar):
```
ALARM BERBUNYI
    ↓
1. Segera hubungi Control Room via radio
    ↓
2. Aktifkan Emergency Shut-Down (ESD) di panel terdekat
    ↓
3. Tutup Isolation Valve secara manual (jika ESD gagal)
    ↓
4. Evakuasi semua personel dari area reactor
    ↓
5. Tunggu instruksi di Assembly Point
    ↓
6. Jangan masuk kembali sebelum ada clearance dari supervisor
```

### Prosedur jika Terjadi Tumpahan Asam:
1. Jangan panik — jauhi area tumpahan
2. Gunakan jalur evakuasi yang sudah ditentukan
3. Aktifkan safety shower jika ada kontak dengan kulit
4. Laporkan ke tim emergency response

---

# BAGIAN 4 — IMPLIKASI UNTUK GAME OLIVIA VR

## Pertanyaanmu: "Apakah Memutar Katup itu Realistis?"

**JAWABAN: YA, 100% REALISTIS!**

Berikut penjelasannya berdasarkan data teknis:

Dalam SOP HPAL nyata, operator memang **secara fisik memutar katup (valve)** dalam kondisi darurat. Ini terjadi karena:

1. **Isolation Valve** harus ditutup manual ketika sistem ESD otomatis gagal
2. **Letdown Valve** (katup penurunan tekanan Flash Vessel) bisa tersumbat kerak dan harus dibuka/tutup manual
3. **Bypass Valve** diputar untuk mengalihkan aliran ketika ada kebocoran di jalur utama

Jadi skenario darurat di Blueprint Final kalian (Fase 3) — **memutar isolation valve lalu tekan ESD** — adalah prosedur yang **benar-benar ada** di SOP pabrik HPAL nyata! Bukan rekaan.

## Revisi Alur Fase Game Berdasarkan Riset Ini:

| Fase | Nama | Aktivitas Realistis di VR |
|------|------|--------------------------|
| **Fase 0** | Tutorial | Pengenalan alur proses + kontrol VR |
| **Fase 1** | Pemakaian APD | Ambil & pakai Helm, Rompi, Kacamata, Sepatu (✅ SUDAH JALAN) |
| **Fase 2** | Ruang Kontrol DCS | Monitor parameter suhu/tekanan autoclave di layar |
| **Fase 3** | Inspeksi Lantai Pabrik | Cek visual autoclave dengan X-Ray Vision, tandai kerak |
| **Fase 4** | Darurat! | Memutar Isolation Valve + Tekan tombol ESD dalam countdown |
| **Hasil** | Rapor K3 | Skor berdasarkan kecepatan & ketepatan prosedur |

> **Catatan untuk Developer:**
> Berdasarkan riset ini, "Scanner" yang ada di kode `PhaseManager` bisa diganti menjadi sebuah **"alat inspeksi"** atau langsung dilompat ke skenario darurat Fase 3. Pilihan terbaik untuk gameplay yang realistis adalah mengikuti alur di tabel di atas.

---

# BAGIAN 5 — REFERENSI

| Sumber | URL |
|--------|-----|
| Caldera Engineering | calderaengineering.com |
| Total Materia | totalmateria.com |
| SGS Metallurgy | sgs.com |
| Xinhai Mining | xinhaimining.com |
| TBP Nickel (Indonesia) | tbpnickel.com |
| Earthworks (Tailing Risk) | earthworks.org |
| Sucofindo K3 | sucofindo.co.id |
| Kumparan (K3 Smelter) | kumparan.com |
| Kompas (Kecelakaan Smelter) | kompas.com |
| KLHK Regulasi B3 | menlhk.go.id |

---

> 💡 **Kesimpulan Utama:**
> Proses HPAL sangat kompleks, penuh bahaya, dan membutuhkan disiplin K3 yang tinggi.
> Simulasi VR OLIVIA yang kalian bangun adalah alat edukasi yang **sangat relevan dan realistis**.
> Setiap fase di game kalian memiliki **dasar ilmiah dan SOP nyata** dari industri.
> Ini bukan sekadar game — ini adalah **training simulator** yang sesungguhnya.
