# GAMEPLAY Level 7 - Autoclave Inspection (SHOWCASE)

## Tujuan

Level 7 adalah **showcase utama** project ini. Player melakukan inspeksi penuh ke autoclave HPAL dengan kemampuan VR yang tidak mungkin dilakukan di plant nyata: melihat tembus dinding baja titanium-clad, menandai kerak, membaca gauge, mengambil sample aman, dan mengkonfirmasi safety target.

## Alur Gameplay

1. Player teleport ke platform inspeksi autoclave (lapangan).
2. Player tekan tombol DCS 7 (sebelumnya, kalau urutan dari DCS).
3. Player aktifkan **X-Ray Vision** (tombol X). Shell autoclave berubah transparan biru holografis.
4. Player **cycle X-Ray Layer** (tombol C) — ada 3 layer:
   - **Layer A: Slurry Flow** — slurry ungu mengalir dari kompartemen kiri ke kanan
   - **Layer B: Heat Map** — gradient suhu merah-jingga per kompartemen (250°C target)
   - **Layer C: Scale Buildup** — kerak hematit menempel di dinding & blade agitator
5. Player **tandai 3 spot scale** (tombol M, atau pointing controller). Spot terdekat dari sudut pandang yang ditandai. Highlight merah emissive setelah ditandai.
6. Player **baca 3 gauge analog** di body autoclave: Pressure, Temperature, RPM (klik / pointer di masing-masing gauge).
7. Player **submit logbook** (tombol L) setelah semua gauge dibaca → notif `gauges logged`.
8. Player ke **sample port**: buka isolation valve quick-open (tombol V), ambil sample bottle (tombol B). Valve auto-close setelah sample diambil (SOP).
9. Player lakukan **safety drill 4 step** (tombol S, sekali tekan per step):
   - Step 1: PSV (Pressure Safety Valve) — di atas vessel
   - Step 2: ESD button — panel samping
   - Step 3: Quench Water valve — air pendingin darurat
   - Step 4: Exit route — jalur evakuasi
10. Setelah X-Ray + Scale + Logbook + Sample + Safety semua tercentang → quest `Level7AutoclaveInspected = true`.
11. Player lapor HT: `Autoclave normal, suhu 250 derajat, tekanan 50 atm, agitator 60 RPM.`
12. Misi selesai, lanjut Level 8.

## Analisa Mekanik & Nilai Training VR

### Kenapa Level 7 Powerful?

Autoclave HPAL adalah jantung paling berbahaya di plant nyata: 250°C, 50 Bar, asam sulfat di tabung baja titanium-clad sepanjang 25-50 meter. Operator beneran tidak boleh berdiri 1 meter di samping autoclave karena risiko PSV release, kebocoran asam panas, dan paparan H2S. **Yang dilatih di VR adalah hal yang mustahil dilatih di lapangan.**

### Mekanik VR-Native

#### 1. Three-Layer X-Ray Vision
Bukan sekadar "shell jadi transparan". Tiga layer terpisah memvisualkan tiga aspek operasi:
- **Slurry Flow Layer**: gerakan slurry compartment ke compartment, residence time visualisasi
- **Heat Map Layer**: distribusi suhu real, deteksi hot spot (>270°C = warning runaway reaction)
- **Scale Buildup Layer**: ketebalan kerak per kompartemen — operator nyata harus shutdown kalau >40%

Ini menjawab "kenapa VR" dengan paling jelas: hanya VR yang bisa rendering tiga "X-ray view" simultan dari mesin yang sama. Di plant nyata, scale buildup hanya bisa dilihat **setelah autoclave dimatikan dan dibongkar berhari-hari**.

#### 2. Scale Mark & Tag (Maintenance Logging)
Player **menggunakan pointer untuk menandai spot scale** — gestur spasial mengelilingi tabung horizontal sepanjang 25 meter. Ini melatih:
- Spatial awareness lokasi kompartemen mana yang paling rentan scale
- Skill membaca pattern build-up (titanium agitator vs dinding kompartemen)
- Habit logging maintenance (tagging titik untuk follow-up)

Tidak bisa direplikasi di mouse-keyboard tutorial karena butuh head + hand tracking di sekeliling object 3D besar.

#### 3. Cluster Gauge Reading + Logbook
Bukan klik satu-satu. Player **harus baca 3 gauge analog** lalu **submit ke logbook handheld**. Logbook seperti clipboard virtual yang hanya menerima submit setelah semua 3 gauge dibaca. Ini melatih:
- Reading analog pressure/temperature gauge (skill yang hilang di operator generasi DCS-only)
- Habit verifikasi silang DCS vs lapangan (parameter di DCS bisa salah, gauge mekanis = ground truth)
- SOP "tulis dulu baru lapor" — bukan langsung lapor dari memori

#### 4. Sample Port Operation (SOP Asam Aman)
Sample dari autoclave HPAL adalah PLS (Pregnant Leach Solution) — **80°C, pH < 1, kaya logam berat**. Mekanik VR-native:
- Buka isolation valve quick-open dulu (gerakan putar)
- Ambil bottle dengan grabber panjang (heat-resistant tool, gestur stretch lengan)
- Valve **auto-close** setelah sample diambil (interlock SOP)
- Bottle visual: PLS warna hijau-coklat = nikel terlarut OK

Ini mensimulasikan SOP "sample asam panas tanpa kontak langsung" yang sangat berbahaya kalau salah urutan — dan praktek di plant nyata sangat mahal serta riskan.

#### 5. Safety Drill 4-Target
Player **diharuskan menunjuk satu per satu lokasi 4 alat darurat**:
- PSV (Pressure Safety Valve) — di atas vessel, lepas tekanan otomatis
- ESD Button — panel samping, manual emergency shutdown
- Quench Water Valve — air pendingin darurat (cool-down dari 250°C)
- Exit Route — jalur evakuasi

Ini mensimulasikan **muscle memory spasial** yang membedakan operator terlatih dengan operator panik saat emergency real. Di Level 14 (Emergency), player akan diuji tanpa hint — kalau lokasi 4 target ini tidak hafal di Level 7, player kemungkinan gagal Level 14.

#### 6. Voice Report Granular
Tiga sub-laporan harus akurat sebelum DCS approve:
- "Suhu 250 derajat, tekanan 50 atm, agitator 60 RPM" (parameter cluster)
- "Scale di kompartemen 3, 25 persen, masih aman" (scale assessment)
- "Sample PLS diambil, kondisi normal" (sample status)

Player belajar **format laporan SOP yang sebenarnya** di industri HPAL — bukan kalimat bebas.

### Kenapa Ini Jawaban "Kenapa VR"?

Plant HPAL adalah lingkungan training paling sulit: bahaya tinggi, equipment yang tidak boleh dimatikan untuk training, dan banyak skill yang muscle-memory based. Level 7 menyatukan 5 mekanik yang **secara fundamental hanya bisa dilakukan di VR**:

1. Lihat tembus dinding baja 10cm (X-Ray triple layer)
2. Tag spot kerak dengan gesture spasial pada object 25 meter
3. Baca gauge mekanis sambil isi logbook handheld (dual-task gestural)
4. Ambil sample asam panas dengan grabber tanpa risiko nyata
5. Latihan muscle memory lokasi alat darurat

Hasilnya: trainee yang sudah lulus Level 7 punya pemahaman jauh lebih dalam dibanding training video atau simulator desktop.

## Hotkey Debug Keyboard

- `X`: Toggle X-Ray Vision
- `C`: Cycle X-Ray Layer (Slurry → Heat → Scale)
- `M`: Mark scale spot terdekat
- `L`: Submit logbook (setelah baca 3 gauge)
- `V`: Toggle sample port valve
- `B`: Take sample bottle
- `S`: Confirm safety drill step (PSV → ESD → Quench → Exit)

## Object Names yang Diharapkan di Scene (auto-find supported)

- `Autoclave_Field`, `Autoclave_Shell`, `EndCap_Left`, `EndCap_Right`, `AgitatorShaft`
- `L7_ScaleSpot_1` ... `L7_ScaleSpot_5` (atau `L7_ScaleSpot_Marker_*`)
- `L7_Logbook`, `Autoclave_Logbook`
- `L7_SamplePort_Valve`, `SamplePort_Handwheel`
- `L7_PSV`, `Autoclave_PSV`
- `L7_ESD`, `L7_Quench_Valve`, `L7_Exit_Marker`

## Parameter SOP (Target)

- Pressure: `45-50 atm`
- Temperature: `250-255°C`
- Agitator RPM: `60`
- Scale buildup max: `40%` (di atas itu wajib shutdown)
- pH outlet: `< 1.0`
- Sample warna PLS: hijau-coklat (nikel terlarut)
