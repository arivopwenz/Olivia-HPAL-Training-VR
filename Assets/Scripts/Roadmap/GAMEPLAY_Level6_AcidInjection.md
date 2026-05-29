# GAMEPLAY Level 6 - Acid Injection + Autoclave Feed

## Tujuan

Level 6 melatih operator untuk membuka jalur slurry panas dari pre-heater ke autoclave, lalu mengaktifkan sistem injeksi asam sulfat dengan rasio SOP, stroke pompa metering yang benar, tank yang tepat, dan verifikasi field sebelum izinkan H2SO4 mengalir.

## Alur Gameplay

1. Player spawn di DCS.
2. Player menekan tombol DCS 6.
3. Player lapor HT: `Outlet pre-heater dibuka, segera salurkan ke autoclave.`
4. Player teleport ke field valve jalur pre-heater/autoclave.
5. Lampu merah menyala selama valve belum terbuka.
6. Player memutar handwheel/valve seperti mekanik Level 5.
7. Saat valve penuh terbuka, lampu berubah hijau.
8. Setelah jeda singkat, slurry ungu mengalir dan volume cairan naik di autoclave dari bawah ke atas.
9. Player lapor HT: `Slurry masuk autoclave.`
10. Player kembali ke DCS.
11. Player menyiapkan acid system di DCS dengan empat input wajib:
    - Set acid ratio ke `350 kg/ton` (tombol +/-).
    - Set metering pump stroke ke `70%` (tombol [ / ]).
    - Pilih acid storage tank `A` atau `B` (tombol T).
    - Tekan `ARM` (tombol A) untuk konfirmasi interlock.
    Status DCS hanya berubah `DCS ARMED - GO TO FIELD` jika ratio + stroke + ARM lengkap.
12. Player teleport ke field acid injection skid.
13. Player membuka isolation valve H2SO4 sampai 100%; gauge naik dan lampu jalur jadi hijau.
14. Player menekan tombol mushroom hijau `LOCAL START` di skid. Pompa metering H2SO4 mulai berputar dan lampu pump-running hijau menyala. Acid belum dialirkan ke autoclave.
15. Player melakukan leak inspection minimal `8 detik` (lihat flange, sparger, sambungan) lalu menekan tombol mushroom biru `LEAK INSPECTION OK`. Tombol ini hanya menerima setelah 8 detik berlalu.
16. Acid mengalir ke autoclave.
17. Player lapor HT: `Acid aktif, rasio 350 kilo, pH 1.0.`
18. Misi selesai dan lanjut Level 7 Autoclave.

## Koreksi Istilah

Ucapan `tutup preheater dibuka` diterima sebagai alias, tapi istilah SOP yang lebih benar adalah `outlet pre-heater dibuka`. Yang dibuka adalah jalur outlet/valve dari pre-heater menuju autoclave, bukan tutup mesin.

## Analisa Mekanik & Nilai Training VR

Di plant HPAL nyata, operator DCS tidak boleh langsung menyalakan pompa H2SO4 dari ruang kontrol untuk alasan keselamatan: pompa metering asam sulfat selalu memerlukan local start di lapangan setelah verifikasi visual. Karena itu Level 6 sekarang dipisah jadi tiga lapis konfirmasi — sesuai SOP HPAL umum dan permintaan analisa "kenapa VR":

### Lapis 1 - DCS Setup (Operator DCS)
Empat input wajib di DCS, bukan satu:
- `Acid Ratio 350 kg/ton`: dosis target leaching agar pH turun ke 1.0.
- `Metering Pump Stroke 70%`: stroke pompa diaphragm/plunger; nilai stroke menentukan flow rate aktual.
- `Tank A / Tank B`: dua tank H2SO4 redundant; operator memilih yang penuh agar tidak run-dry mid-batch.
- `ARM`: interlock konfirmasi DCS sudah ready dan acid line akan berenergi. Tanpa ARM, field tidak diizinkan start.

### Lapis 2 - Field Isolation Valve (Operator Field)
Isolation valve H2SO4 harus dibuka manual via handwheel dengan mekanik gauge yang sama dengan Level 5. Lampu jalur merah → hijau ketika valve 100% terbuka. Ini memastikan player benar-benar tahu lokasi valve isolasi (kalau emergency, valve ini yang harus ditutup pertama).

### Lapis 3 - Local Start + Leak Inspection (Operator Field)
- `LOCAL START` mushroom hijau: hanya bisa ditekan dari lapangan, bukan DCS. Setelah ditekan, pompa metering berputar tapi acid masih tertahan oleh check valve di sparger - belum benar-benar masuk autoclave.
- `8 detik leak inspection`: jeda paksa supaya player melihat sambungan flange, gland packing, dan sparger - mendeteksi bocor sebelum H2SO4 berenergi penuh. Tombol `LEAK INSPECTION OK` di-disable sebelum 8 detik selesai.
- Setelah `LEAK INSPECTION OK`, acid akhirnya mengalir ke autoclave.

### Kenapa Mekanik Ini VR-Native
1. **Spasial valve & tombol**: player harus berpindah fisik antara wheel valve, tombol mushroom hijau, dan tombol mushroom biru. Tidak bisa direplikasi di mouse-keyboard tutorial.
2. **Tunggu 8 detik di lapangan**: melatih kebiasaan ergonomi inspeksi visual (lihat sambungan dengan kepala, bukan klik tombol).
3. **Dual operator workflow**: DCS dan field memang dipisah. VR memaksa player merasakan perpindahan tanggung jawab dan komunikasi via HT.
4. **Konsekuensi keputusan**: kalau player skip leak inspection (di future iteration bisa di-extend dengan random leak event), dampak visual H2SO4 bocor bisa diperlihatkan. Materi training yang sangat sulit dijual di video atau slide PowerPoint.

## Parameter SOP

- Acid ratio: `350 kg/ton bijih` (toleransi ±10).
- Stroke metering pump: `70%` (toleransi ±5).
- Target pH leaching: `1.0`.
- Tank operasi: `A` atau `B` (boleh salah satu, harus dipilih eksplisit).
- Durasi leak inspection minimum: `8 detik`.
- Visual slurry: ungu.
- Visual acid: amber/kuning transparan.
- Lampu merah: belum aman / valve belum terbuka.
- Lampu hijau: jalur/valve berhasil dibuka.
- Lampu pump-running: hijau saat metering pump menyala (setelah LOCAL START).

## Hotkey Debug Keyboard

Untuk testing tanpa headset XR, controller juga menerima keyboard:
- `R / F`: putar handwheel (slurry & acid) buka / tutup.
- `+ / -`: naik / turun acid ratio.
- `[ / ]`: turun / naik stroke pompa.
- `T`: ganti tank A / B.
- `A`: toggle ARM.
- `G`: tekan LOCAL START (saat fase TekanLocalStart).
- `H`: tekan LEAK INSPECTION OK (saat fase LeakInspection, setelah 8 detik).

## Object Names yang Diharapkan di Scene (auto-find, ada fallback runtime)

- `SpawnPoint_DCS`, `SpawnPoint_Lvl6`, `SpawnPoint_Lvl6_AcidSkid`
- `Btn_AcidPlus`, `Btn_AcidMinus`, `Btn_AcidStrokePlus`, `Btn_AcidStrokeMinus`, `Btn_AcidTankSelect`, `Btn_AcidArm`
- `Btn_AcidLocalStart`, `Btn_AcidLeakOk` (auto-create runtime mushroom button kalau tidak ada)
- `AcidInjection_Pump_Rotor` / `Pump_Rotor` untuk visual rotor
- `LetdownValve_Handwheel` untuk valve slurry
- `Dosing_Handwheel` / `IsolationValve_Handwheel` / `AcidTank_B_OutletIsolationHandwheel` untuk valve acid
