# Penjelasan Lengkap Schematic 3D Sistem HPAL Nikel

**Topik:** Teknik Metalurgi HPAL Nikel — Autoclave, K3, dan Tailing/Limbah B3  
**Konteks:** Bahan pemahaman alur mesin untuk pembuatan asset AR/VR industri  
**Catatan:** Dokumen ini bersifat edukatif dan skematik untuk pembelajaran serta pembuatan asset 3D/VR, bukan gambar detail engineering/P&ID resmi pabrik.

---

## 1. Gambaran Besar Proses HPAL

HPAL adalah singkatan dari **High Pressure Acid Leaching**.

### Bahasa keilmuan

HPAL adalah proses **hidrometalurgi** untuk melarutkan nikel (Ni) dan kobalt (Co) dari bijih nikel laterit menggunakan **asam sulfat (H₂SO₄)** pada kondisi **suhu tinggi** dan **tekanan tinggi** di dalam reaktor autoclave.

### Bahasa sederhana

HPAL adalah proses “memasak” lumpur bijih nikel dengan asam di dalam mesin tekanan tinggi agar nikel dan kobaltnya larut dan bisa dipisahkan.

---

## 2. Alur Paling Sederhana

```text
Bijih laterit
→ dihancurkan
→ dicampur air jadi slurry
→ dipompa
→ dipanaskan
→ ditambah asam sulfat
→ masuk autoclave
→ Ni & Co larut
→ tekanan diturunkan
→ dipisahkan padat-cair
→ dimurnikan
→ diendapkan jadi MHP
→ sisa padatan masuk pengelolaan tailing
```

Versi proses lengkap:

```text
Crusher
→ Slurry Tank
→ Slurry Pump
→ Pre-heater / Steam Heater
→ Acid Injection System
→ Autoclave / High Pressure Reactor
→ Letdown Valve
→ Flash Vessel 1
→ Flash Vessel 2
→ CCD / Solid-Liquid Separation
→ Neutralization / Purification
→ MHP Precipitation Tank
→ Filter Press / Tailing Neutralization
→ Dry Stack Tailings Storage
```

---

## 3. Fungsi Setiap Mesin

---

# 1. Crusher / Penghancur Bijih Laterit

## Bahasa keilmuan

Crusher digunakan untuk mereduksi ukuran bijih nikel laterit agar menjadi ukuran partikel yang lebih kecil dan lebih seragam sebelum masuk tahap **slurry preparation**.

## Bahasa sederhana

Ini mesin penghancur batu nikel. Batu yang masih besar dihancurkan supaya lebih mudah dicampur dengan air dan diproses.

## Fungsi utama

- Mengecilkan ukuran bijih.
- Membuat material lebih mudah dicampur.
- Membantu reaksi HPAL lebih efektif.
- Memperbesar luas permukaan bijih agar asam lebih mudah bereaksi.

## Kenapa penting?

Kalau bijih terlalu besar, asam sulit menjangkau bagian dalam mineral. Akibatnya, nikel dan kobalt lebih sulit larut.

---

# 2. Slurry Tank

## Bahasa keilmuan

Slurry tank berfungsi sebagai tangki pencampur bijih halus dengan air untuk membentuk slurry dengan konsentrasi padatan tertentu.

## Bahasa sederhana

Ini tangki tempat bijih halus dicampur air sampai menjadi lumpur proses.

**Slurry = bijih halus + air.**

## Fungsi utama

- Mencampur bijih dan air.
- Menjaga slurry tetap homogen.
- Mencegah padatan mengendap.
- Menyiapkan umpan untuk dipompa ke sistem HPAL.

## Catatan asset VR

Untuk asset, slurry tank sebaiknya terlihat memiliki:

- tangki silinder,
- pengaduk di tengah,
- cairan/lumpur coklat di dalam,
- pipa masuk dan pipa keluar,
- railing/tangga maintenance.

---

# 3. Slurry Pump

## Bahasa keilmuan

Slurry pump digunakan untuk memindahkan slurry dari slurry tank menuju pre-heater dan sistem reaktor dengan tekanan tertentu.

## Bahasa sederhana

Ini pompa yang mendorong lumpur bijih agar bisa mengalir ke mesin berikutnya.

## Fungsi utama

- Mendorong slurry ke pre-heater.
- Menjaga aliran tetap stabil.
- Memastikan autoclave mendapat umpan secara kontinu.
- Mengalirkan material yang kental dan mengandung padatan.

## Kenapa tidak bisa pakai pompa biasa?

Karena slurry itu:

- berat,
- kental,
- mengandung partikel padat,
- bersifat abrasif,
- bisa mengikis bagian dalam pompa.

## Catatan asset VR

Pompa slurry bisa dibuat sebagai mesin biru/metal dengan:

- motor listrik,
- casing pompa,
- pipa inlet,
- pipa outlet,
- base plate,
- baut dan flange.

---

# 4. Pre-heater / Steam Heater

## Bahasa keilmuan

Pre-heater memanaskan slurry sebelum masuk autoclave menggunakan steam agar energi proses lebih efisien dan reaksi HPAL lebih stabil.

## Bahasa sederhana

Ini pemanas awal. Slurry dipanaskan dulu sebelum masuk autoclave supaya tidak terlalu dingin.

## Fungsi utama

- Menaikkan suhu slurry sebelum reaksi utama.
- Menghemat energi.
- Membantu autoclave mencapai suhu operasi.
- Menjaga reaksi lebih stabil.

## Kenapa perlu dipanaskan dulu?

Autoclave bekerja pada suhu tinggi. Kalau slurry masuk masih dingin, energi yang dibutuhkan akan lebih besar dan proses menjadi kurang stabil.

## Catatan asset VR

Pre-heater dapat divisualkan sebagai:

- vessel horizontal,
- pipa steam masuk,
- pipa kondensat keluar,
- valve steam,
- isolasi panas,
- label “HOT SURFACE”.

---

# 5A. Acid Injection Tank / Tangki Asam Sulfat

## Bahasa keilmuan

Acid injection tank menyimpan asam sulfat H₂SO₄ yang digunakan sebagai reagen pelindian dalam proses HPAL.

## Bahasa sederhana

Ini tangki penyimpan asam sulfat.

Asam sulfat adalah bahan kimia utama HPAL. Tugasnya adalah membantu melarutkan nikel dan kobalt dari bijih laterit.

## Fungsi utama

- Menyimpan H₂SO₄.
- Menyediakan asam untuk reaksi.
- Menjadi sumber acid injection ke jalur proses.
- Mendukung pengaturan dosis asam.

## Catatan K3

Area ini harus diberi tanda bahaya:

- **Korosif**
- **Chemical hazard**
- **Wajib APD**
- **Safety shower nearby**

---

# 5B. Pompa H₂SO₄

## Bahasa keilmuan

Pompa H₂SO₄ atau metering pump mengatur laju injeksi asam sulfat ke aliran slurry agar dosis asam sesuai kebutuhan reaksi.

## Bahasa sederhana

Ini pompa khusus untuk memasukkan asam ke proses dengan jumlah yang terkontrol.

## Fungsi utama

- Mengalirkan asam dari tangki ke pipa proses.
- Mengatur dosis asam.
- Membantu kontrol pH.
- Mendukung reaksi pelindian Ni dan Co.

## Kenapa dosis asam harus dikontrol?

Kalau asam terlalu sedikit:

- nikel tidak larut maksimal,
- recovery rendah,
- proses kurang efektif.

Kalau asam terlalu banyak:

- proses lebih korosif,
- biaya kimia naik,
- limbah lebih sulit ditangani.

---

# 6. Autoclave / High Pressure Reactor Utama

Autoclave adalah **jantung utama proses HPAL**.

## Bahasa keilmuan

Autoclave adalah bejana tekan horizontal tempat berlangsungnya reaksi **high pressure acid leaching** antara slurry laterit dan H₂SO₄ pada suhu serta tekanan tinggi untuk melarutkan Ni dan Co ke dalam larutan.

## Bahasa sederhana

Autoclave itu seperti **panci presto raksasa** untuk memasak lumpur bijih dengan asam, suhu tinggi, dan tekanan tinggi.

## Apa yang terjadi di dalam autoclave?

```text
Slurry + H₂SO₄ + suhu tinggi + tekanan tinggi
→ Ni dan Co larut ke dalam cairan
→ sebagian pengotor tetap sebagai padatan/residu
```

## Fungsi utama autoclave

- Menjadi tempat reaksi utama HPAL.
- Melarutkan Ni dan Co.
- Mengubah slurry biasa menjadi slurry hasil pelindian.
- Menghasilkan larutan kaya Ni-Co atau PLS.

## PLS itu apa?

**PLS = Pregnant Leach Solution**

Bahasa mudahnya: cairan hasil pelindian yang mengandung nikel dan kobalt terlarut.

## Kondisi operasi tipikal

| Parameter | Nilai umum |
|---|---:|
| Suhu operasi | 240–270 °C |
| Tekanan operasi | 40–60 bar |
| Waktu tinggal | 30–90 menit |
| pH operasi | < 1 |

Nilai ini bisa berbeda tergantung desain pabrik, jenis bijih, dan teknologi yang digunakan.

---

## Komponen Penting di Sekitar Autoclave

### 6.1 Sensor Suhu

**Fungsi:** mengukur temperatur proses.

Autoclave HPAL bekerja pada suhu tinggi. Kalau suhu terlalu tinggi, proses bisa menjadi berbahaya.

### 6.2 Sensor Tekanan

**Fungsi:** mengukur tekanan di dalam sistem.

Jika tekanan naik melewati batas aman, sistem harus memberi alarm atau melakukan shutdown otomatis.

### 6.3 Monitoring pH

**Fungsi:** memantau tingkat keasaman slurry/larutan.

pH sangat penting karena proses HPAL bergantung pada kondisi asam.

### 6.4 ESD Point

**ESD = Emergency Shutdown**

Fungsi ESD adalah menghentikan sistem saat kondisi darurat.

Saat ESD aktif, sistem bisa:

- menghentikan pompa,
- menutup valve penting,
- menghentikan injeksi asam,
- mengamankan autoclave.

### 6.5 PSV / Pressure Safety Valve

**PSV = Pressure Safety Valve**

Fungsi PSV adalah melindungi sistem dari tekanan berlebih.

Bahasa sederhana: PSV adalah valve pengaman jika tekanan terlalu tinggi.

### 6.6 Vent

Fungsi vent adalah membuang gas atau uap dari sistem ke jalur yang aman.

Di plant nyata, vent biasanya diarahkan ke sistem pengaman seperti scrubber atau sistem venting yang dirancang khusus.

### 6.7 Quench Water Line

Fungsi quench water line adalah memasukkan air pendingin saat suhu terlalu tinggi.

Bahasa sederhana: ini jalur air darurat untuk membantu mendinginkan sistem.

### 6.8 Isolation Valve

Isolation valve adalah valve untuk memutus atau mengisolasi aliran.

Dipakai saat:

- shutdown,
- maintenance,
- emergency,
- isolasi pipa atau alat tertentu.

### 6.9 Sampling Line

Sampling line adalah jalur kecil untuk mengambil sampel proses.

Sampel bisa diuji untuk mengetahui:

- kadar Ni,
- kadar Co,
- kadar Fe,
- pH,
- impurity,
- kondisi reaksi.

### 6.10 Drain

Drain adalah jalur pembuangan bawah.

Fungsinya:

- mengosongkan alat,
- membuang cairan sisa,
- membantu flushing,
- membantu maintenance.

---

# 7. Letdown Valve

## Bahasa keilmuan

Letdown valve berfungsi menurunkan tekanan slurry hasil leaching secara terkendali sebelum masuk ke flash vessel.

## Bahasa sederhana

Ini valve penurun tekanan.

Slurry keluar dari autoclave masih panas dan bertekanan tinggi. Tidak boleh langsung masuk ke tangki biasa. Tekanannya harus diturunkan dulu.

## Fungsi utama

- Menurunkan tekanan.
- Mengatur keluaran dari autoclave.
- Mencegah pelepasan tekanan mendadak.
- Melindungi flash vessel dan unit berikutnya.

## Kenapa penting untuk VR?

Letdown valve bisa menjadi titik skenario darurat. Misalnya:

```text
Scale / kerak menumpuk
→ letdown valve terganggu
→ aliran keluar terhambat
→ tekanan autoclave naik
→ alarm aktif
→ pemain harus menjalankan SOP darurat
```

---

# 8. Flash Vessel 1

## Bahasa keilmuan

Flash vessel tahap pertama menerima slurry panas bertekanan dari autoclave dan menurunkan tekanan awal sehingga sebagian cairan berubah menjadi uap flash.

## Bahasa sederhana

Ini tangki penurun tekanan tahap pertama.

Ketika tekanan turun, sebagian cairan panas berubah menjadi uap.

## Fungsi utama

- Menurunkan tekanan tahap pertama.
- Memisahkan sebagian uap.
- Menurunkan suhu slurry.
- Mengurangi beban ke tahap berikutnya.

---

# 9. Flash Vessel 2

## Bahasa keilmuan

Flash vessel tahap kedua melanjutkan proses penurunan tekanan dan pendinginan slurry sebelum masuk ke unit pemisahan padat-cair.

## Bahasa sederhana

Ini tangki penurun tekanan tahap kedua.

## Kenapa perlu dua tahap?

Karena penurunan tekanan yang terlalu mendadak bisa berbahaya. Lebih aman jika tekanan turun bertahap.

## Fungsi utama

- Menurunkan tekanan lanjutan.
- Menurunkan suhu lebih jauh.
- Membuat slurry lebih aman untuk diproses di CCD.

---

# 10. CCD / Solid-Liquid Separation Tanks

**CCD = Counter Current Decantation**

## Bahasa keilmuan

CCD adalah sistem pemisahan padatan dan cairan secara bertahap untuk memisahkan pregnant leach solution dari residu padat hasil leaching.

## Bahasa sederhana

Ini area pemisah cairan dan padatan.

Setelah HPAL, campurannya masih berupa slurry. Di dalamnya ada:

- cairan yang mengandung Ni dan Co,
- padatan sisa bijih yang tidak larut.

## Fungsi utama

- Memisahkan cairan dan padatan.
- Mencuci residu agar Ni-Co yang tersisa bisa diambil.
- Mengurangi kehilangan nikel dan kobalt.
- Mengirim cairan kaya logam ke purification.
- Mengirim padatan/residu ke pengolahan tailing.

## Alur di CCD

```text
Slurry hasil HPAL
→ cairan kaya Ni-Co lanjut ke pemurnian
→ padatan/residu lanjut ke pengolahan tailing
```

---

# 11. Neutralization / Purification Tank

## Bahasa keilmuan

Neutralization/purification tank digunakan untuk mengatur pH dan mengendapkan impurity seperti Fe, Al, atau unsur pengotor lain dari larutan hasil leaching.

## Bahasa sederhana

Ini tangki pembersih larutan.

Cairan dari CCD masih belum bersih. Masih ada pengotor. Maka larutan perlu dinetralkan atau dimurnikan.

## Fungsi utama

- Menaikkan pH secara terkontrol.
- Mengendapkan pengotor.
- Membersihkan larutan Ni-Co.
- Menyiapkan larutan sebelum dibuat MHP.

## Contoh pengotor

- Fe,
- Al,
- Mn,
- Mg,
- Cr,
- unsur minor lain tergantung bijih.

---

# 12. Precipitation / MHP Product Tank

## Bahasa keilmuan

Precipitation tank digunakan untuk mengendapkan Ni dan Co dari larutan menjadi **Mixed Hydroxide Precipitate** atau MHP.

## Bahasa sederhana

Ini tangki pembentukan produk MHP.

Setelah larutan Ni-Co cukup bersih, logam Ni dan Co dibuat mengendap menjadi padatan hijau/abu kehijauan yang disebut MHP.

## MHP itu apa?

**MHP = Mixed Hydroxide Precipitate**

Bahasa mudahnya: endapan campuran hidroksida nikel dan kobalt.

## Fungsi utama

- Mengubah Ni-Co dari bentuk larutan menjadi padatan.
- Menghasilkan produk antara.
- Menyiapkan bahan untuk proses lanjut seperti nickel sulfate.

---

# 13. Filter Press / Tailing Neutralization Unit

## Bahasa keilmuan

Filter press memisahkan air dari padatan tailing setelah proses netralisasi, sehingga menghasilkan cake padat yang lebih mudah disimpan.

## Bahasa sederhana

Ini mesin pemeras lumpur tailing.

Tailing masih banyak air. Filter press memerasnya agar air terpisah dan padatannya menjadi lebih kering.

## Fungsi utama

- Mengurangi kadar air tailing.
- Membuat tailing menjadi bentuk cake/padatan.
- Memudahkan penyimpanan.
- Mengurangi risiko kebocoran limbah cair.
- Mendukung sistem dry stack tailings.

## Catatan penting

Sebelum masuk filter press, tailing biasanya perlu dinetralkan dulu agar tidak terlalu asam.

---

# 14. Dry Stack Tailings Storage

## Bahasa keilmuan

Dry stack tailings storage adalah area penyimpanan tailing kering hasil filtrasi, biasanya dalam bentuk padatan/cake yang ditumpuk secara terkontrol.

## Bahasa sederhana

Ini tempat penyimpanan sisa limbah padat yang sudah dikeringkan.

Bukan kolam lumpur cair, tetapi lebih seperti tumpukan padatan yang sudah diproses.

## Fungsi utama

- Menyimpan tailing kering.
- Mengurangi risiko limbah cair bocor.
- Memudahkan pengawasan.
- Mengurangi risiko pencemaran dibanding tailing basah.

---

## 4. Alur Lengkap Jika Dibaca dari Gambar

### Tahap 1 — Persiapan Slurry

```text
Crusher → Slurry Tank → Slurry Pump
```

Bijih laterit dihancurkan, lalu dicampur air menjadi slurry. Slurry kemudian dipompa ke proses berikutnya.

### Tahap 2 — Pemanasan Awal

```text
Slurry Pump → Pre-heater / Steam Heater
```

Slurry dipanaskan menggunakan steam supaya siap masuk ke reaksi suhu tinggi.

### Tahap 3 — Injeksi Asam

```text
Acid Injection Tank → Pompa H₂SO₄ → Jalur slurry
```

Asam sulfat dimasukkan ke aliran slurry. Di sinilah bahan kimia utama HPAL mulai bekerja.

### Tahap 4 — Reaksi di Autoclave

```text
Slurry panas + H₂SO₄ → Autoclave
```

Di dalam autoclave, pada suhu dan tekanan tinggi, Ni dan Co larut ke dalam cairan. Inilah proses inti HPAL.

### Tahap 5 — Penurunan Tekanan / Flash

```text
Autoclave → Letdown Valve → Flash Vessel 1 → Flash Vessel 2
```

Slurry panas bertekanan tinggi diturunkan tekanannya secara bertahap. Sebagian uap keluar melalui vent.

### Tahap 6 — Pemisahan Padat-Cair

```text
Flash Vessel → CCD Tanks
```

Cairan kaya Ni-Co dipisahkan dari padatan sisa. Cairan lanjut ke pemurnian. Padatan lanjut ke pengelolaan tailing.

### Tahap 7 — Pemurnian dan Pembentukan MHP

```text
CCD → Neutralization/Purification → Precipitation Tank → MHP
```

Larutan Ni-Co dibersihkan dari pengotor, lalu Ni dan Co diendapkan menjadi MHP.

### Tahap 8 — Penanganan Tailing

```text
Residu padat → Tailing Neutralization → Filter Press → Dry Stack Tailings
```

Sisa padatan dinetralkan, disaring, dikeringkan, lalu disimpan di dry stack tailings storage.

---

## 5. Tiga Aliran Utama di Sistem HPAL

### 5.1 Aliran Slurry / Bijih

Biasanya digambarkan dengan warna coklat.

```text
Crusher → Slurry Tank → Pump → Pre-heater → Autoclave
```

Ini adalah aliran bahan utama dari bijih.

### 5.2 Aliran Asam Sulfat

Biasanya digambarkan dengan warna merah.

```text
Acid Tank → Pompa H₂SO₄ → Jalur injeksi → Autoclave
```

Ini adalah aliran bahan kimia untuk melarutkan Ni dan Co.

### 5.3 Aliran Produk dan Limbah

Setelah autoclave, alirannya terbagi:

```text
Cairan kaya Ni-Co → Purification → MHP
Padatan/residu → Tailing treatment → Dry stack
```

| Arah | Hasil |
|---|---|
| Jalur produk | MHP kaya Ni-Co |
| Jalur limbah | Tailing/residu yang harus dikelola |

---

## 6. Fungsi Instrumentasi dan Safety

| Komponen | Bahasa keilmuan | Bahasa sederhana |
|---|---|---|
| Sensor suhu | Mengukur temperatur proses | Mengecek apakah suhu aman |
| Sensor tekanan | Mengukur tekanan sistem | Mengecek tekanan autoclave |
| pH monitor | Mengukur keasaman larutan | Mengecek seberapa asam proses |
| ESD point | Emergency shutdown system | Tombol/sistem stop darurat |
| PSV | Pressure safety valve | Valve pengaman tekanan |
| Vent | Jalur pelepasan gas/uap | Tempat gas/uap keluar aman |
| Drain | Jalur pembuangan bawah | Saluran buang cairan |
| Sampling line | Jalur pengambilan sampel | Tempat ambil contoh larutan |
| Quench water line | Jalur air pendingin darurat | Air untuk menurunkan suhu |
| Isolation valve | Katup isolasi aliran | Valve pemutus aliran |
| Letdown valve | Katup penurun tekanan | Valve untuk mengurangi tekanan |

---

## 7. Mekanisme Paling Penting yang Harus Dipahami

```text
Bijih nikel tidak langsung jadi nikel.
Bijih harus dibuat slurry dulu.
Slurry dipanaskan.
Lalu dicampur asam sulfat.
Kemudian masuk autoclave.
Di autoclave, Ni dan Co larut.
Setelah keluar, tekanan diturunkan.
Lalu cairan dan padatan dipisah.
Cairan Ni-Co dibuat jadi MHP.
Sisa padatan dikelola sebagai tailing.
```

---

## 8. Kalimat Presentasi untuk Lomba

> “Sistem HPAL bekerja dengan mengubah bijih nikel laterit menjadi slurry, kemudian slurry tersebut dipanaskan dan direaksikan dengan asam sulfat di dalam autoclave bertekanan tinggi. Nikel dan kobalt larut ke dalam larutan, lalu setelah tekanan diturunkan melalui flash vessel, larutan dipisahkan dari residu padat. Larutan kaya Ni-Co kemudian dimurnikan dan diendapkan menjadi MHP, sedangkan residu padat dikelola sebagai tailing.”

Versi lebih singkat:

> “Proyek kami mensimulasikan proses HPAL nikel dalam bentuk VR training simulator. Fokusnya adalah memahami alur mesin, bahaya autoclave bertekanan tinggi, prosedur K3, serta dampak pengelolaan tailing.”

---

## 9. Prioritas Asset VR

| Prioritas | Asset |
|---|---|
| P0 | Autoclave besar |
| P0 | Slurry pump |
| P0 | Acid injection tank + pump |
| P0 | Letdown valve |
| P0 | Flash vessel |
| P0 | ESD panel |
| P1 | Slurry tank |
| P1 | Pre-heater |
| P1 | CCD tanks |
| P1 | Neutralization tank |
| P1 | MHP tank/product container |
| P1 | Filter press |
| P1 | Dry stack tailing area |
| P2 | Crusher |
| P2 | Piping detail, sampling line, drain, vent |
| P2 | Safety railing, ladder, platform, signage |

Asset paling penting untuk ditonjolkan:

```text
Autoclave
+ acid injection
+ letdown valve
+ flash vessel
+ ESD
+ tailing area
```

Karena bagian ini langsung menunjukkan unsur:

- metalurgi HPAL,
- mesin utama,
- K3,
- emergency response,
- limbah B3/tailing.

---

## 10. Catatan Istilah Penting

| Istilah | Bahasa sederhana |
|---|---|
| HPAL | Proses mengambil Ni-Co dengan asam, panas, dan tekanan tinggi |
| Hidrometalurgi | Pengolahan logam memakai cairan kimia |
| Laterit | Jenis bijih nikel |
| Slurry | Lumpur bijih + air |
| Autoclave | Reaktor tekanan tinggi |
| H₂SO₄ | Asam sulfat |
| Leaching / Pelindian | Melarutkan logam dari bijih |
| PLS | Cairan kaya Ni-Co hasil leaching |
| CCD | Pemisahan padat-cair bertahap |
| MHP | Endapan campuran hidroksida nikel-kobalt |
| Tailing | Sisa padatan/lumpur proses |
| B3 | Bahan berbahaya dan beracun |
| ESD | Sistem stop darurat |
| PSV | Valve pengaman tekanan |
| Isolation valve | Valve pemutus aliran |
| Letdown valve | Valve penurun tekanan |
| Scale / kerak | Endapan yang menempel dan bisa menyumbat |
| Abrasif | Bisa mengikis seperti amplas |
| Korosif | Bisa merusak logam/kulit karena reaksi kimia |

---

## 11. Logika Skenario VR yang Cocok

### Skenario normal

```text
Pemain masuk ruang DCS
→ membaca suhu, tekanan, pH, flow
→ turun ke area plant dengan APD
→ inspeksi autoclave dan valve
→ memahami alur slurry, acid, dan steam
```

### Skenario gangguan

```text
Scale / kerak meningkat
→ letdown valve mulai terganggu
→ tekanan autoclave naik
→ alarm aktif
→ pemain menjalankan SOP
```

### Skenario emergency

```text
Alarm tekanan tinggi
→ acknowledge alarm
→ aktifkan ESD
→ pastikan acid injection berhenti
→ tutup/isolate valve sesuai instruksi
→ evakuasi ke assembly point
```

### Output evaluasi

Pemain dinilai berdasarkan:

- kelengkapan APD,
- kecepatan membaca alarm,
- ketepatan menekan ESD,
- ketepatan memilih valve,
- pemahaman alur proses,
- kepatuhan evakuasi.

---

## 12. Checklist Pembuatan Asset 3D

### Autoclave

- [ ] Shell silinder horizontal
- [ ] End cap / head
- [ ] Flange besar
- [ ] Baut flange
- [ ] Support saddle
- [ ] Nozzle inlet slurry
- [ ] Nozzle acid injection
- [ ] Nozzle steam
- [ ] Discharge nozzle
- [ ] Vent nozzle
- [ ] Drain nozzle
- [ ] Sampling nozzle
- [ ] Pressure gauge
- [ ] Temperature gauge
- [ ] pH monitor
- [ ] Isolation valve
- [ ] Letdown valve
- [ ] ESD panel
- [ ] Platform dan ladder
- [ ] Safety signage

### Slurry Area

- [ ] Crusher
- [ ] Conveyor
- [ ] Slurry tank
- [ ] Agitator slurry tank
- [ ] Slurry pump
- [ ] Piping coklat untuk slurry

### Acid Area

- [ ] Acid tank H₂SO₄
- [ ] Acid pump
- [ ] Red piping
- [ ] Corrosive hazard sign
- [ ] Safety shower
- [ ] Bund wall / containment area

### Flash Area

- [ ] Flash vessel 1
- [ ] Flash vessel 2
- [ ] Vent pipe
- [ ] Drain pipe
- [ ] Level indicator
- [ ] Letdown valve
- [ ] Platform dan ladder

### Downstream Area

- [ ] CCD tanks
- [ ] Neutralization tank
- [ ] MHP precipitation tank
- [ ] Product container / MHP bag
- [ ] Filter press
- [ ] Dry stack tailing area

---

## 13. Ringkasan Super Singkat

HPAL adalah proses mengambil nikel dan kobalt dari bijih laterit dengan cara:

```text
bijih dibuat slurry
→ dipanaskan
→ diberi asam sulfat
→ direaksikan di autoclave
→ tekanan diturunkan
→ padatan dan cairan dipisah
→ cairan jadi MHP
→ sisa padatan jadi tailing yang harus dikelola
```

Bagian paling penting untuk simulasi AR/VR kamu:

```text
Autoclave + K3 + Emergency + Tailing
```

Karena ini adalah bagian yang paling kuat untuk menunjukkan:

- teknik metalurgi,
- mesin industri,
- risiko tekanan tinggi,
- keselamatan kerja,
- pengelolaan limbah B3,
- nilai edukasi VR.
