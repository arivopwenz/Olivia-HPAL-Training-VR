# 🏭 HPAL — MEKANISME DETAIL SETIAP MESIN (Deep Research)
## Untuk Proyek OLIVIA VR Simulator

> Disusun berdasarkan riset mendalam dari sumber teknis industri.
> **Sumber:** nickelinstitute.org, springer.com, klarenbv.com, 911metallurgist.com, hatch.com, roxia.com, mclanahan.com, calderaengineering.com, nobelclad.com, magnesiaspecialties.com

---

# DAFTAR ISI

1. Pre-heater (Pemanas Awal)
2. Autoclave (Reaktor Tekanan Tinggi)
3. Flash Vessel (Tangki Penurun Tekanan)
4. CCD / Thickener (Pemisahan Padat-Cair)
5. Neutralization Tank (Tangki Netralisasi/Pemurnian)
6. MHP Precipitation Tank (Tangki Pengendapan Produk)
7. Filter Press (Mesin Penyaring Tailing)
8. Dry Stack Tailings (Penyimpanan Limbah Kering)

---

# ═══════════════════════════════════════════════════════════════
# 1. PRE-HEATER (PEMANAS AWAL SLURRY)
# ═══════════════════════════════════════════════════════════════

## 1.1 Apa Itu Pre-heater?

Pre-heater adalah alat penukar panas (heat exchanger) yang memanaskan slurry SEBELUM masuk autoclave. Tujuannya agar autoclave tidak perlu bekerja terlalu keras menaikkan suhu dari dingin.

## 1.2 Jenis Pre-heater di HPAL

Di pabrik HPAL, ada **2 tipe** pre-heater yang umum digunakan:

### Tipe A: Direct Contact Heater (Splash/Spray Vessel)
- Uap panas (flash steam) dari flash vessel **langsung bersentuhan** dengan slurry
- Slurry disemprotkan ke dalam vessel, uap panas naik dan memanaskan slurry
- Lebih sederhana, tapi slurry jadi sedikit lebih encer (karena uap terkondensasi jadi air)

### Tipe B: Shell & Tube Heat Exchanger (Indirect)
- Uap panas mengalir di sisi **shell** (cangkang luar)
- Slurry mengalir di dalam **tube** (pipa-pipa kecil)
- Panas berpindah melalui dinding pipa — tidak ada pencampuran langsung
- Lebih kompleks tapi slurry tidak terencerkan

## 1.3 Mekanisme Kerja (Step by Step)

```
LANGKAH 1: Slurry dingin (~30-40°C) dipompa dari slurry tank
    ↓
LANGKAH 2: Slurry masuk ke pre-heater vessel/heat exchanger
    ↓
LANGKAH 3: Flash steam dari flash vessel dialirkan ke pre-heater
    ↓
LANGKAH 4: Panas dari steam berpindah ke slurry
            (steam mengembun menjadi air / kondensat)
    ↓
LANGKAH 5: Slurry keluar sudah panas (~150-180°C)
    ↓
LANGKAH 6: Slurry panas siap dipompa ke autoclave
```

## 1.4 Kenapa Harus Dipanaskan Dulu?

| Tanpa Pre-heater | Dengan Pre-heater |
|------------------|-------------------|
| Autoclave harus panaskan dari 30°C → 250°C | Autoclave hanya perlu dari 180°C → 250°C |
| Butuh steam boiler SANGAT besar | Hemat energi ~60-70% |
| Biaya operasional tinggi | Biaya jauh lebih rendah |
| Carbon footprint besar | Lebih ramah lingkungan |

## 1.5 Hubungan Pre-heater ↔ Flash Vessel

```
┌─────────────────── SIKLUS ENERGI TERTUTUP ───────────────────┐
│                                                                │
│  Flash Vessel menghasilkan uap panas (flash steam)            │
│       ↓                                                        │
│  Uap dialirkan via pipa ke Pre-heater                         │
│       ↓                                                        │
│  Pre-heater menggunakan uap untuk panaskan slurry baru        │
│       ↓                                                        │
│  Uap mengembun jadi kondensat (air panas)                     │
│       ↓                                                        │
│  Kondensat bisa digunakan lagi atau dibuang ke WWTP           │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**PENTING:** Tidak ada "tangki penampung uap" terpisah. Flash vessel langsung menghasilkan uap, dan uap itu langsung dialirkan ke pre-heater melalui pipa. Uap tidak disimpan — langsung dipakai.

## 1.6 Komponen Fisik Pre-heater

- Vessel/tangki horizontal atau vertikal
- Pipa inlet steam (dari flash vessel)
- Pipa inlet slurry (dari slurry pump)
- Pipa outlet slurry panas (ke autoclave)
- Pipa outlet kondensat (air bekas steam)
- Pressure gauge & temperature gauge
- Isolation valve di setiap pipa
- Insulation (isolasi panas) di seluruh permukaan
- Label "HOT SURFACE — DANGER"


---

# ═══════════════════════════════════════════════════════════════
# 2. AUTOCLAVE (REAKTOR TEKANAN TINGGI — JANTUNG HPAL)
# ═══════════════════════════════════════════════════════════════

## 2.1 Apa Itu Autoclave?

Autoclave HPAL adalah **bejana tekan horizontal raksasa** tempat reaksi pelindian (leaching) terjadi. Di sinilah nikel dan kobalt dilarutkan dari bijih menggunakan asam sulfat pada suhu dan tekanan ekstrem.

Bayangkan: **panci presto** sebesar gedung 5 lantai, terbuat dari baja 10cm, dilapisi titanium murni di dalamnya.

## 2.2 Konstruksi Fisik Detail

### Struktur Utama:
- **Shell (Cangkang):** Silinder horizontal, panjang 25-50 meter, diameter 4-6 meter
- **Material Shell:** Baja karbon tebal (carbon steel) — menahan tekanan
- **Lining (Lapisan Dalam):** Titanium murni atau titanium-clad — tahan asam sulfat
- **End Caps (Tutup):** Berbentuk ellipsoidal/torispherical di kedua ujung
- **Support Saddle:** Penyangga baja di bawah untuk menopang berat

### Komponen Internal:
- **Compartment Walls (Sekat):** Membagi autoclave menjadi 4-6 kompartemen
- **Baffles (Anti-swirl):** Mencegah slurry berputar tanpa tercampur
- **Agitator (Pengaduk):** 1 unit per kompartemen, bilah titanium berputar 30-50 RPM
- **Sparger (Penyuntik):** Pipa berlubang untuk injeksi asam & steam

## 2.3 Mekanisme Kerja Internal (Step by Step)

```
LANGKAH 1: Slurry panas (~180°C) dipompa masuk dari ujung KIRI
    ↓
LANGKAH 2: H₂SO₄ (asam sulfat) disuntikkan via sparger
    ↓
LANGKAH 3: Steam disuntikkan untuk menjaga suhu 250°C
    ↓
LANGKAH 4: Di KOMPARTEMEN 1 — reaksi mulai, agitator mengaduk
           Ni + H₂SO₄ → NiSO₄ (nikel larut)
           Co + H₂SO₄ → CoSO₄ (kobalt larut)
           Fe → Fe₂O₃ (besi mengendap jadi hematit)
    ↓
LANGKAH 5: Slurry mengalir ke KOMPARTEMEN 2, 3, 4... (via overflow)
           Reaksi terus berlanjut di setiap kompartemen
    ↓
LANGKAH 6: Setelah ~60 menit, slurry sampai di ujung KANAN
    ↓
LANGKAH 7: Slurry keluar sebagai PLS (Pregnant Leach Solution)
           = cairan kaya nikel + kobalt + padatan sisa
    ↓
LANGKAH 8: PLS keluar melalui LETDOWN VALVE ke Flash Vessel
```

## 2.4 Kenapa Ada Kompartemen?

| Tanpa Kompartemen | Dengan Kompartemen |
|-------------------|-------------------|
| Slurry bisa "shortcut" langsung ke outlet | Slurry HARUS melewati semua zona |
| Waktu reaksi tidak merata | Waktu reaksi terkontrol (~60 menit) |
| Recovery nikel rendah | Recovery nikel optimal (>95%) |
| Pencampuran tidak efisien | Setiap zona punya agitator sendiri |

## 2.5 Fungsi Agitator

- Menjaga padatan tetap tersuspensi (tidak mengendap)
- Memastikan asam tercampur merata dengan slurry
- Mempercepat transfer panas
- Mencegah dead zone (area tanpa reaksi)
- Material: **Titanium murni** (karena baja biasa hancur dalam hitungan jam oleh asam)

## 2.6 Masalah Utama: SCALE (Kerak)

```
Reaksi kimia HPAL menghasilkan endapan:
- Hematit (Fe₂O₃)
- Alunit
- Jarosit

Endapan ini MENEMPEL di:
- Dinding dalam autoclave
- Bilah agitator
- Sekat kompartemen
- Nozzle dan valve

AKIBAT:
- Aliran terhambat
- Tekanan naik
- Agitator macet
- SHUTDOWN DARURAT diperlukan
```

## 2.7 Sensor & Safety di Autoclave

| Komponen | Fungsi | Lokasi |
|----------|--------|--------|
| Temperature Sensor | Ukur suhu tiap kompartemen | Di dinding tiap kompartemen |
| Pressure Transmitter | Ukur tekanan internal | Di atas vessel |
| pH Probe | Ukur keasaman | Di outlet |
| PSV (Pressure Safety Valve) | Lepas tekanan darurat | Di atas vessel |
| ESD Button | Emergency Shutdown | Di panel samping |
| Quench Water Line | Air pendingin darurat | Di atas vessel |
| Isolation Valve | Putus aliran saat darurat | Di inlet & outlet |


---

# ═══════════════════════════════════════════════════════════════
# 3. FLASH VESSEL (TANGKI PENURUN TEKANAN + PENGHASIL UAP)
# ═══════════════════════════════════════════════════════════════

## 3.1 Apa Itu Flash Vessel?

Flash Vessel adalah tangki bertekanan yang berfungsi untuk **menurunkan tekanan slurry secara bertahap** setelah keluar dari autoclave. Saat tekanan turun, sebagian cairan panas **berubah menjadi uap secara spontan** — fenomena ini disebut **flash evaporation**.

## 3.2 Kenapa Disebut "Flash"?

"Flash" = kilat/sekejap. Ketika cairan bertekanan tinggi tiba-tiba masuk ke ruang bertekanan lebih rendah, sebagian cairan **langsung menguap dalam sekejap** tanpa perlu dipanaskan lagi. Ini terjadi karena titik didih cairan turun saat tekanan turun.

## 3.3 Mekanisme Kerja Detail (Step by Step)

```
═══ FLASH VESSEL 1 ═══

LANGKAH 1: Slurry keluar autoclave (250°C, 50 Bar)
    ↓
LANGKAH 2: Melewati LETDOWN VALVE — tekanan mulai turun
    ↓
LANGKAH 3: Masuk Flash Vessel 1 (tekanan di-set ~12 Bar)
    ↓
LANGKAH 4: Karena tekanan turun drastis (50→12 Bar),
           titik didih air turun dari 250°C ke ~190°C
    ↓
LANGKAH 5: Kelebihan panas menyebabkan sebagian air LANGSUNG MENGUAP
           (flash evaporation — terjadi spontan, bukan karena dipanaskan)
    ↓
LANGKAH 6: Uap naik ke ATAS vessel → dialirkan ke Pre-heater via pipa
           Slurry (lebih dingin) turun ke BAWAH → lanjut ke Flash Vessel 2
    ↓

═══ FLASH VESSEL 2 ═══

LANGKAH 7: Slurry dari FV1 (~190°C, 12 Bar) masuk FV2
    ↓
LANGKAH 8: Tekanan turun lagi (12→3 Bar)
           Titik didih turun ke ~120°C
    ↓
LANGKAH 9: Flash evaporation terjadi lagi
           Uap → ke pre-heater tahap 2
           Slurry → turun, lebih dingin lagi
    ↓

═══ FLASH VESSEL 3 (jika ada) ═══

LANGKAH 10: Slurry (~120°C, 3 Bar) masuk FV3
    ↓
LANGKAH 11: Tekanan turun ke ~1 Bar (atmosfer)
            Suhu turun ke ~80°C
    ↓
LANGKAH 12: Slurry sekarang AMAN untuk diproses di CCD
```

## 3.4 Kenapa Harus Bertahap? (Bukan Langsung 50 Bar → 1 Bar)

| Penurunan Mendadak | Penurunan Bertahap |
|--------------------|-------------------|
| Flash evaporation SANGAT HEBAT | Flash evaporation terkontrol |
| Erosi/kerusakan pada vessel | Vessel aman |
| Uap terlalu banyak sekaligus — sulit ditangkap | Uap bisa di-recover efisien per tahap |
| Risiko ledakan/pecah pipa | Aman dan terkendali |
| Slurry bisa "menyembur" tak terkendali | Aliran tetap stabil |

## 3.5 Dua Fungsi Utama Flash Vessel

### FUNGSI 1: Safety — Menurunkan Tekanan
- Slurry 50 Bar TIDAK BOLEH langsung ke tangki biasa (akan meledak)
- Flash vessel adalah "airlock" bertahap antara zona tekanan tinggi dan atmosfer

### FUNGSI 2: Energy Recovery — Menghasilkan Uap Daur Ulang
- Uap yang dihasilkan masih panas dan punya energi besar
- Uap ini dikirim ke pre-heater untuk memanaskan slurry baru
- Menghemat 60-70% kebutuhan energi boiler
- Menurut [Springer/Hatch](https://link.springer.com/chapter/10.1007/978-3-031-38141-6_10): recovery flash steam adalah elemen esensial desain HPAL berkelanjutan

## 3.6 Komponen Fisik Flash Vessel

```
        ┌─── Pipa Uap Keluar (ke Pre-heater) ───→
        │
   ┌────┴────┐
   │  ZONA   │  ← Uap berkumpul di atas
   │  UAP    │
   │─────────│  ← Level permukaan cairan
   │  ZONA   │
   │  CAIR   │  ← Slurry mengumpul di bawah
   │  (Slurry)│
   └────┬────┘
        │
        └─── Pipa Slurry Keluar (ke FV berikutnya / CCD) ───→
```

Komponen:
- Vessel vertikal (silinder tegak) atau horizontal
- Inlet slurry (dari autoclave/FV sebelumnya)
- Outlet uap di ATAS (ke pre-heater)
- Outlet slurry di BAWAH (ke tahap berikutnya)
- Level indicator (mengukur ketinggian cairan)
- Pressure gauge
- Temperature gauge
- Safety valve (PSV)
- Vent (pembuangan gas berlebih)
- Drain di dasar (untuk maintenance)

## 3.7 Analogi Sederhana

Bayangkan kamu membuka tutup botol soda yang sudah dikocok:
- Di dalam botol = tekanan tinggi (CO₂ terlarut)
- Saat tutup dibuka = tekanan turun mendadak
- Gas langsung keluar dengan hebat = "flash"

Flash vessel melakukan hal yang sama, tapi **secara terkontrol dan bertahap**, agar energi uapnya bisa ditangkap dan digunakan kembali.


---

# ═══════════════════════════════════════════════════════════════
# 4. CCD / THICKENER (PEMISAHAN PADAT-CAIR)
# ═══════════════════════════════════════════════════════════════

## 4.1 Apa Itu CCD?

**CCD = Counter-Current Decantation**

Ini adalah sistem pemisahan padatan dan cairan menggunakan serangkaian **thickener** (tangki pengental). Mesin yang kamu lupa namanya — yang memisahkan "bentuk kasar dan liquid" — ini dia: **THICKENER** dalam rangkaian CCD.

## 4.2 Apa Itu Thickener?

Thickener adalah tangki silinder BESAR (diameter 15-50 meter!) yang menggunakan **gravitasi** untuk memisahkan padatan dari cairan. Padatan yang lebih berat tenggelam ke bawah, cairan bersih naik ke atas.

## 4.3 Mekanisme Kerja Thickener (Step by Step)

```
═══ BAGIAN ATAS: FEEDWELL ═══

LANGKAH 1: Slurry dari flash vessel dipompa masuk ke FEEDWELL
           (feedwell = silinder kecil di tengah-atas thickener)
    ↓
LANGKAH 2: Di feedwell, ditambahkan FLOCCULANT (bahan kimia penggumpal)
           Flocculant membuat partikel halus saling menempel jadi gumpalan besar
    ↓
LANGKAH 3: Slurry + flocculant keluar dari feedwell, menyebar ke seluruh tangki
    ↓

═══ BAGIAN TENGAH: ZONA SETTLING (PENGENDAPAN) ═══

LANGKAH 4: Gumpalan padatan (floc) perlahan TENGGELAM karena gravitasi
           Cairan bersih (overflow) perlahan NAIK ke atas
    ↓
LANGKAH 5: RAKE (lengan penggaruk) berputar pelan di dasar tangki
           Rake mendorong padatan yang sudah mengendap ke PUSAT tangki
    ↓

═══ BAGIAN BAWAH: UNDERFLOW ═══

LANGKAH 6: Padatan pekat (underflow) dikumpulkan di cone (kerucut) di dasar
    ↓
LANGKAH 7: Underflow dipompa keluar — ini adalah RESIDU/TAILING
    ↓

═══ BAGIAN ATAS: OVERFLOW ═══

LANGKAH 8: Cairan bersih (overflow) mengalir melewati weir (bendung) di tepi atas
    ↓
LANGKAH 9: Overflow = PLS (Pregnant Leach Solution) kaya Ni-Co
           → lanjut ke Neutralization/Purification
```

## 4.4 Kenapa Disebut "Counter-Current"?

Dalam CCD, ada **beberapa thickener berurutan** (biasanya 4-7 unit). Aliran padatan dan cairan bergerak **berlawanan arah**:

```
ARAH PADATAN (Underflow): ───────────────────────→
                    Thickener 1 → 2 → 3 → 4 → 5 → ke Tailing

ARAH PENCUCIAN (Wash Water): ←───────────────────
                    Thickener 5 ← 4 ← 3 ← 2 ← 1 ← Air bersih masuk

HASIL:
- Overflow Thickener 1 = PLS paling kaya Ni-Co → ke pemurnian
- Underflow Thickener 5 = Padatan paling bersih → ke tailing
```

Tujuannya: **mencuci padatan berkali-kali** agar nikel-kobalt yang masih tersisa di padatan bisa diambil semaksimal mungkin.

## 4.5 Komponen Fisik Thickener

```
         ┌──── Overflow Launder (saluran cairan bersih) ────→ ke Purification
         │
    ╔════╧════════════════════════════╗
    ║    Cairan Bersih (Overflow)     ║  ← Zona jernih
    ║─────────────────────────────────║
    ║    Zona Settling                ║  ← Partikel sedang turun
    ║    (Feedwell di tengah atas)    ║
    ║─────────────────────────────────║
    ║    Zona Kompresi                ║  ← Padatan makin pekat
    ║    ═══ RAKE berputar ═══        ║  ← Lengan penggaruk
    ╚════╤════════════════════════════╝
         │         ↘ Cone (kerucut)
         └──── Underflow Pump ────→ ke Thickener berikutnya / Tailing
```

Komponen utama:
- **Tank:** Silinder besar, diameter 15-50m, kedalaman 3-10m
- **Feedwell:** Silinder kecil di tengah atas — tempat slurry masuk
- **Rake Mechanism:** Lengan baja dengan blade, berputar pelan (0.1-0.5 RPM)
- **Drive Head:** Motor + gearbox di atas tengah — memutar rake
- **Overflow Launder:** Saluran di tepi atas — menampung cairan bersih
- **Underflow Cone:** Kerucut di dasar — mengumpulkan padatan
- **Underflow Pump:** Pompa di bawah — mengeluarkan padatan pekat
- **Flocculant Dosing System:** Sistem penambahan bahan penggumpal
- **Torque Indicator:** Mengukur beban rake (jika terlalu berat = masalah)

## 4.6 Kenapa Pakai Thickener, Bukan Filter Langsung?

| Thickener (CCD) | Filter Langsung |
|------------------|-----------------|
| Bisa handle volume SANGAT besar | Kapasitas terbatas |
| Operasi kontinu 24/7 | Batch (harus stop-start) |
| Bisa cuci padatan berkali-kali | Sulit mencuci ulang |
| Recovery Ni-Co lebih tinggi | Banyak Ni-Co terbuang |
| Biaya operasi rendah | Biaya tinggi untuk volume besar |


---

# ═══════════════════════════════════════════════════════════════
# 5. NEUTRALIZATION TANK (TANGKI NETRALISASI / PEMURNIAN)
# ═══════════════════════════════════════════════════════════════

## 5.1 Apa Itu Neutralization Tank?

Setelah CCD, cairan PLS (kaya Ni-Co) masih mengandung **pengotor** (impurities): besi (Fe), aluminium (Al), kromium (Cr), mangan (Mn). Pengotor ini harus dibuang sebelum nikel bisa diendapkan.

Neutralization tank adalah tangki berpengaduk tempat **pH cairan dinaikkan secara bertahap** menggunakan bahan alkali, sehingga pengotor mengendap dan bisa dipisahkan.

## 5.2 Mekanisme Kerja (Step by Step)

```
═══ TAHAP 1: Netralisasi Primer (pH 1.0 → 3.5) ═══

LANGKAH 1: PLS asam (pH ~1.0) masuk ke Tank Netralisasi 1
    ↓
LANGKAH 2: LIMESTONE (CaCO₃ / batu kapur) ditambahkan sebagai bubur
    ↓
LANGKAH 3: Agitator mengaduk agar tercampur merata
    ↓
LANGKAH 4: pH naik dari 1.0 ke 3.5
    ↓
LANGKAH 5: Pada pH 3.5, BESI (Fe³⁺) mengendap sebagai Fe(OH)₃
           (endapan coklat kemerahan)
    ↓
LANGKAH 6: Endapan besi dipisahkan (via thickener kecil atau filter)
    ↓

═══ TAHAP 2: Netralisasi Sekunder (pH 3.5 → 5.5) ═══

LANGKAH 7: Cairan (sudah bebas besi) masuk Tank Netralisasi 2
    ↓
LANGKAH 8: LIME (CaO / kapur tohor) ditambahkan
    ↓
LANGKAH 9: pH naik dari 3.5 ke 5.5
    ↓
LANGKAH 10: Pada pH 5.5, ALUMINIUM (Al³⁺) dan KROMIUM (Cr³⁺) mengendap
    ↓
LANGKAH 11: Endapan dipisahkan lagi
    ↓
LANGKAH 12: Cairan bersih (hanya Ni²⁺ dan Co²⁺ yang tersisa)
            → SIAP masuk MHP Precipitation Tank
```

## 5.3 Kenapa pH Harus Bertahap?

Setiap logam mengendap pada pH yang BERBEDA:

| Logam | pH Pengendapan | Tahap |
|-------|---------------|-------|
| Fe³⁺ (Besi) | 3.0 - 4.0 | Tahap 1 |
| Al³⁺ (Aluminium) | 4.5 - 5.5 | Tahap 2 |
| Cr³⁺ (Kromium) | 5.0 - 6.0 | Tahap 2 |
| **Ni²⁺ (Nikel)** | **7.0 - 8.0** | **BELUM mengendap** |
| **Co²⁺ (Kobalt)** | **7.5 - 8.5** | **BELUM mengendap** |

Jadi dengan menaikkan pH bertahap, kita bisa **membuang pengotor TANPA kehilangan nikel dan kobalt** — karena Ni dan Co baru mengendap di pH lebih tinggi.

## 5.4 Komponen Fisik

- Tangki silinder vertikal (baja + lining anti-korosi)
- Agitator (pengaduk) di tengah
- Inlet PLS (dari CCD)
- Inlet reagent (limestone/lime slurry)
- pH sensor (monitoring kontinu)
- Outlet cairan bersih (ke tahap berikutnya)
- Outlet endapan/sludge (ke disposal)
- Overflow weir
- Dosing pump untuk reagent


---

# ═══════════════════════════════════════════════════════════════
# 6. MHP PRECIPITATION TANK (TANGKI PENGENDAPAN PRODUK)
# ═══════════════════════════════════════════════════════════════

## 6.1 Apa Itu MHP Precipitation?

**MHP = Mixed Hydroxide Precipitate**

Ini adalah tahap di mana nikel dan kobalt yang sudah terlarut dalam cairan **diubah kembali menjadi padatan** — dalam bentuk endapan hijau/abu-abu kehijauan yang disebut MHP. MHP adalah **produk akhir** pabrik HPAL.

## 6.2 Mekanisme Kerja (Step by Step)

```
LANGKAH 1: Cairan bersih kaya Ni-Co (dari neutralization) masuk tank
    ↓
LANGKAH 2: MgO (Magnesium Oksida) ditambahkan sebagai bubur/slurry
    ↓
LANGKAH 3: Agitator mengaduk agar MgO tercampur merata
    ↓
LANGKAH 4: pH naik ke 7.0 - 8.0
    ↓
LANGKAH 5: Reaksi kimia terjadi:
           NiSO₄ + MgO + H₂O → Ni(OH)₂↓ + MgSO₄
           CoSO₄ + MgO + H₂O → Co(OH)₂↓ + MgSO₄
    ↓
LANGKAH 6: Ni(OH)₂ dan Co(OH)₂ mengendap sebagai padatan hijau
           = MHP (Mixed Hydroxide Precipitate)
    ↓
LANGKAH 7: MHP dipisahkan dari cairan (via filter atau thickener)
    ↓
LANGKAH 8: MHP dikemas dan dikirim ke refinery
           → diolah menjadi NiSO₄ (bahan baterai EV)
```

## 6.3 Kenapa Pakai MgO (Bukan NaOH atau Lime)?

| Reagent | Kelebihan | Kekurangan |
|---------|-----------|------------|
| **MgO** | Selektif untuk Ni-Co, MHP berkualitas tinggi | Lebih mahal |
| NaOH | Murah, cepat bereaksi | Kurang selektif, banyak pengotor ikut |
| Lime (CaO) | Sangat murah | Kalsium ikut mengendap, kualitas rendah |

MgO dipilih karena menghasilkan MHP dengan **kadar Ni-Co tinggi dan pengotor rendah** — penting untuk bahan baterai.

Menurut [Martin Marietta Magnesia](https://magnesiaspecialties.com/blogs/mixed-hydroxide-precipitate): MgO reaktivitas tinggi semakin banyak digunakan di sirkuit HPAL untuk presipitasi selektif nikel dan kobalt.

## 6.4 Kontrol pH Kritis

- pH terlalu rendah (<7.0): Ni-Co tidak mengendap sempurna → recovery rendah
- pH terlalu tinggi (>8.5): Mangan (Mn) dan Magnesium (Mg) ikut mengendap → MHP kotor
- **Sweet spot: pH 7.0 - 7.5** untuk MHP berkualitas tinggi

Menurut [Endress+Hauser](https://www.us.endress.com/en/endress-hauser-group/Case-studies-application-notes/automation-efficiency-nickel-cobalt-extraction-meta-nickel-kobalt): pengukuran pH kontinu sangat krusial karena berdampak signifikan pada efisiensi dan selektivitas presipitasi.

## 6.5 Komponen Fisik

- Tangki silinder berpengaduk (agitated tank)
- Inlet cairan Ni-Co (dari neutralization)
- Inlet MgO slurry (dari MgO mixing tank)
- pH sensor (kontrol sangat ketat)
- Agitator (kecepatan sedang, mencegah endapan terlalu kasar)
- Outlet MHP slurry (ke filter/thickener)
- Overflow (cairan sisa / barren solution)
- Temperature sensor (suhu mempengaruhi kualitas endapan)


---

# ═══════════════════════════════════════════════════════════════
# 7. FILTER PRESS (MESIN PENYARING / PEMERAS TAILING)
# ═══════════════════════════════════════════════════════════════

## 7.1 Apa Itu Filter Press?

Filter press adalah mesin yang **memisahkan air dari padatan** dengan cara menekan/memeras slurry melalui kain filter. Hasilnya: padatan kering berbentuk "cake" (lempeng) dan air yang bisa didaur ulang.

Di HPAL, filter press digunakan untuk:
- Mengeringkan tailing (limbah) sebelum disimpan di dry stack
- Memisahkan MHP dari cairannya

## 7.2 Mekanisme Kerja Detail (Step by Step)

```
═══ FASE 1: PERSIAPAN ═══

LANGKAH 1: Plate (lempeng-lempeng filter) disusun berjajar
           Setiap plate dilapisi FILTER CLOTH (kain filter)
    ↓
LANGKAH 2: Sistem hidrolik MENEKAN semua plate rapat-rapat
           Membentuk ruang-ruang tertutup (chambers) antar plate
    ↓

═══ FASE 2: PENGISIAN (FILLING) ═══

LANGKAH 3: Slurry/tailing dipompa masuk ke chambers dengan tekanan tinggi
           (melalui lubang di tengah setiap plate)
    ↓
LANGKAH 4: Slurry mengisi semua chambers secara bersamaan
    ↓

═══ FASE 3: FILTRASI (PENYARINGAN) ═══

LANGKAH 5: Tekanan pompa mendorong CAIRAN menembus kain filter
           PADATAN tertahan di dalam chamber (tidak bisa lewat kain)
    ↓
LANGKAH 6: Cairan (filtrate) mengalir keluar melalui saluran di plate
           → ditampung dan dikirim ke WWTP atau didaur ulang
    ↓
LANGKAH 7: Padatan menumpuk di dalam chamber, makin padat
           Membentuk "CAKE" (lempeng padatan kering)
    ↓

═══ FASE 4: PENGERINGAN LANJUT (opsional) ═══

LANGKAH 8: Udara bertekanan ditiupkan melalui cake
           Mengusir sisa air yang masih terperangkap
    ↓

═══ FASE 5: PEMBONGKARAN (CAKE DISCHARGE) ═══

LANGKAH 9: Sistem hidrolik MEMBUKA plate satu per satu
    ↓
LANGKAH 10: Cake (padatan kering) JATUH ke conveyor di bawah
            (karena gravitasi, atau dibantu scraper)
    ↓
LANGKAH 11: Cake dibawa conveyor ke area Dry Stack Tailings
    ↓
LANGKAH 12: Plate ditutup kembali → siklus berulang
```

## 7.3 Visualisasi Sederhana

```
    PLATE   PLATE   PLATE   PLATE   PLATE
    ┌──┐    ┌──┐    ┌──┐    ┌──┐    ┌──┐
    │▓▓│    │▓▓│    │▓▓│    │▓▓│    │▓▓│   ← Cake (padatan)
    │▓▓│    │▓▓│    │▓▓│    │▓▓│    │▓▓│
    └──┘    └──┘    └──┘    └──┘    └──┘
     ↓↓      ↓↓      ↓↓      ↓↓      ↓↓    ← Filtrate (air) keluar
    ════════════════════════════════════════
              CONVEYOR (bawa cake keluar)
```

## 7.4 Spesifikasi Umum

| Parameter | Nilai Tipikal |
|-----------|--------------|
| Jumlah plate | 50 - 200 plate per unit |
| Ukuran plate | 1.5m x 1.5m atau 2m x 2m |
| Tekanan operasi | 6 - 16 Bar |
| Kadar air cake | 15 - 25% (cukup kering untuk ditumpuk) |
| Siklus waktu | 20 - 60 menit per batch |
| Material plate | Polypropylene (PP) atau baja |
| Filter cloth | Polypropylene woven fabric |

## 7.5 Komponen Fisik

- Frame (rangka baja besar)
- Plate pack (susunan lempeng filter)
- Filter cloth (kain filter di setiap plate)
- Hydraulic cylinder (penekan plate)
- Feed pump (pompa slurry masuk)
- Filtrate collection tray (penampung air)
- Cake discharge conveyor (ban berjalan di bawah)
- Plate shifter (mekanisme buka-tutup plate otomatis)
- Drip tray (penampung tetesan)
- Control panel


---

# ═══════════════════════════════════════════════════════════════
# 8. DRY STACK TAILINGS (PENYIMPANAN LIMBAH KERING)
# ═══════════════════════════════════════════════════════════════

## 8.1 Apa Itu Dry Stack Tailings?

Dry Stack Tailings Facility (DSTF) adalah area penyimpanan limbah padat (tailing) yang sudah dikeringkan oleh filter press. Berbeda dengan kolam tailing basah (wet tailings dam), dry stack lebih aman karena tidak ada risiko jebolnya bendungan lumpur.

## 8.2 Mekanisme Penyimpanan

```
LANGKAH 1: Cake dari filter press (kadar air ~20%) dibawa conveyor
    ↓
LANGKAH 2: Cake ditumpuk di area yang sudah disiapkan
           (area berlapis geomembrane untuk mencegah rembesan)
    ↓
LANGKAH 3: Alat berat (bulldozer/compactor) meratakan dan memadatkan
    ↓
LANGKAH 4: Setiap lapisan dipadatkan sebelum lapisan berikutnya ditambah
    ↓
LANGKAH 5: Sistem drainase di bawah menangkap air yang masih merembes
           → air dikirim ke WWTP (Water Treatment Plant)
    ↓
LANGKAH 6: Setelah area penuh, ditutup dengan tanah dan ditanami vegetasi
           (rehabilitasi lahan)
```

## 8.3 Kenapa Dry Stack Lebih Aman?

| Wet Tailings Dam | Dry Stack |
|------------------|-----------|
| Lumpur cair ditampung di kolam besar | Padatan kering ditumpuk |
| Risiko jebol/longsor TINGGI | Risiko longsor RENDAH |
| Bisa mencemari sungai jika bocor | Rembesan minimal |
| Butuh bendungan yang mahal | Tidak perlu bendungan |
| Contoh bencana: Brumadinho, Brasil 2019 | Metode modern yang lebih aman |

---

# ═══════════════════════════════════════════════════════════════
# 9. RINGKASAN ALUR LENGKAP + MEKANISME
# ═══════════════════════════════════════════════════════════════

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ALUR PROSES HPAL LENGKAP                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  [CRUSHER] → Hancurkan bijih jadi partikel halus                        │
│      ↓                                                                   │
│  [SLURRY TANK] → Campur dengan air jadi lumpur                          │
│      ↓                                                                   │
│  [SLURRY PUMP] → Pompa lumpur ke tahap berikutnya                       │
│      ↓                                                                   │
│  [PRE-HEATER] → Panaskan slurry pakai uap dari flash vessel            │
│      ↓                          ↑                                        │
│      ↓                          │ (uap daur ulang)                       │
│      ↓                          │                                        │
│  [AUTOCLAVE] → Reaksi inti: Ni+Co larut oleh H₂SO₄ (250°C, 50 Bar)   │
│      ↓                                                                   │
│  [LETDOWN VALVE] → Mulai turunkan tekanan                               │
│      ↓                                                                   │
│  [FLASH VESSEL 1,2,3] → Turunkan tekanan bertahap + hasilkan uap ──────┘
│      ↓                                                                   │
│  [CCD / THICKENER] → Pisahkan padatan (tailing) dari cairan (PLS)      │
│      ↓                              ↓                                    │
│      ↓ (cairan Ni-Co)              ↓ (padatan/residu)                   │
│      ↓                              ↓                                    │
│  [NEUTRALIZATION] → Buang pengotor  [TAILING NEUTRALIZATION]            │
│      ↓              (Fe, Al, Cr)         ↓                               │
│      ↓                              [FILTER PRESS] → Keringkan          │
│  [MHP PRECIPITATION] → Endapkan         ↓                               │
│      ↓                Ni-Co         [DRY STACK] → Simpan aman           │
│      ↓                                                                   │
│  [MHP PRODUCT] → Dikirim ke refinery → NiSO₄ → BATERAI EV             │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```


---

# ═══════════════════════════════════════════════════════════════
# 10. TABEL PERBANDINGAN SEMUA MESIN
# ═══════════════════════════════════════════════════════════════

| No | Mesin | Fungsi Utama | Mekanisme Inti | Input | Output |
|----|-------|-------------|----------------|-------|--------|
| 1 | **Pre-heater** | Panaskan slurry | Transfer panas dari steam ke slurry | Slurry dingin + Flash steam | Slurry panas (~180°C) |
| 2 | **Autoclave** | Reaksi pelindian | Asam + panas + tekanan melarutkan Ni-Co | Slurry panas + H₂SO₄ | PLS (cairan kaya Ni-Co) |
| 3 | **Flash Vessel** | Turunkan tekanan + hasilkan uap | Flash evaporation saat tekanan turun | Slurry 250°C/50Bar | Slurry dingin + Uap ke pre-heater |
| 4 | **CCD/Thickener** | Pisahkan padat-cair | Gravitasi + flocculant + rake | Slurry hasil leaching | Overflow (PLS) + Underflow (tailing) |
| 5 | **Neutralization** | Buang pengotor | Naikkan pH bertahap → pengotor mengendap | PLS + Limestone/Lime | Cairan bersih Ni-Co |
| 6 | **MHP Precipitation** | Buat produk akhir | MgO naikkan pH → Ni-Co mengendap | Cairan Ni-Co + MgO | MHP (endapan hijau) |
| 7 | **Filter Press** | Keringkan tailing | Tekanan dorong air lewat kain filter | Slurry tailing | Cake kering + Filtrate |
| 8 | **Dry Stack** | Simpan limbah aman | Tumpuk + padatkan di area berlapis | Cake dari filter press | Tailing tersimpan aman |

---

# ═══════════════════════════════════════════════════════════════
# 11. REFERENSI DEEP RESEARCH
# ═══════════════════════════════════════════════════════════════

| Sumber | Topik | URL |
|--------|-------|-----|
| Nickel Institute | Proses HPAL laterit, flash steam recovery | nickelinstitute.org |
| Springer/Hatch | Optimasi heat recovery di autoclave circuit | link.springer.com |
| Klaren BV | Fouling & scaling di HPAL heat exchanger | klarenbv.com |
| Nobel Clad | Titanium lining & internal autoclave | nobelclad.com |
| BC Campus Hydrometallurgy | Agitated autoclaves & CCD washing | pressbooks.bccampus.ca |
| 911 Metallurgist | Thickener design & CCD flowsheet | 911metallurgist.com |
| Roxia | Filter press & thickener di mining | roxia.com |
| McLanahan | Filter press mechanism | mclanahan.com |
| Martin Marietta Magnesia | MgO untuk MHP precipitation | magnesiaspecialties.com |
| Endress+Hauser | pH control di Ni-Co extraction | endress.com |
| Caldera Engineering | HPAL process overview | calderaengineering.com |
| Hatch Engineering | Pressure leaching & flash vessel | hatch.com |
| Valmet | Flow control for nickel autoclave | valmet.com |
| US Patent 4,287,019 | Multi-stage flash evaporation | patents.justia.com |

---

> 💡 **Catatan untuk Tim OLIVIA:**
> Dokumen ini menjelaskan mekanisme INTERNAL setiap mesin.
> Gunakan sebagai referensi saat:
> - Membuat animasi mesin di VR
> - Mendesain interaksi pemain dengan mesin
> - Membuat narasi/voice-over penjelasan di game
> - Menentukan parameter yang bisa dimonitor pemain di DCS
>
> Mesin yang paling penting untuk gameplay darurat:
> **Autoclave → Letdown Valve → Flash Vessel → ESD**

---

*Content was rephrased for compliance with licensing restrictions.*
*Dokumen ini disusun 25 Mei 2026 untuk keperluan edukasi proyek OLIVIA VR Simulator.*
