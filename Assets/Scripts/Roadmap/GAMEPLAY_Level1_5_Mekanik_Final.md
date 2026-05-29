# OLIVIA Gameplay Mechanics Level 1-5

Dokumen ini mencatat mekanik gameplay yang dipakai untuk Level 1 sampai Level 5.

## Level 1 - APD

Tujuan: player mengenakan APD lengkap sebelum masuk operasi.

Alur:
- Player spawn di area APD.
- Player mengambil dan memakai APD wajib.
- Setelah APD lengkap, Level 1 selesai dan lanjut ke Level 2.

Status mekanik:
- APD dasar sudah benar.
- Walkie talkie/HT ikut dibawa untuk laporan level berikutnya.
- Saat level mulai, respirator dipastikan berada di socket dada kanan bila socket tersedia.

## Level 2 - DCS Preparation

Tujuan: player mulai dari DCS dan mengaktifkan operasi awal.

Alur:
- Player spawn di DCS.
- Laser/ray controller harus aktif agar bisa klik tombol DCS.
- Respirator tidak berada di mulut. Respirator disimpan di socket dada/baju.
- Player melihat mesin DCS.
- Player menekan tombol DCS 2.
- Player laporan HT lengkap.
- Level 2 selesai dan lanjut ke Level 3.

Fix runtime:
- `InteractorRayHealer` auto-spawn dan memulihkan Near-Far Interactor + `XRInteractorLineVisual` bawaan Unity XR tanpa menyalakan semua `LineRenderer` custom.
- `PhaseManager` memaksa respirator pindah ke `Socket_Respirator_Baju` setiap start level, termasuk retry beberapa frame.

## Level 3 - Ore & Slurry

Tujuan: player menjalankan ore feed, melihat slurry tank terisi, lalu melaporkan kondisi.

Alur:
- Player start dari DCS.
- Player menekan tombol DCS 3.
- Player laporan HT awal.
- Player teleport ke field slurry/crusher.
- Player mengambil respirator dari dada dan memakainya.
- Player melihat ore berjalan di belt menuju slurry tank.
- Ore/batu jatuh ke slurry tank.
- Air/liquid masuk ke slurry tank.
- Liquid slurry naik dari bawah ke atas sampai 75%.
- Saat 75%, sistem memberi status siap laporan HT akhir.
- Player laporan HT akhir.
- Agitator berputar setelah laporan akhir diterima.
- Menu pilihan muncul: lanjut atau diam di area tersebut.
- Misi Level 3 selesai.

Status mekanik:
- Belt `L2_V2_Wide_Inclined_Rubber_Ore_Belt` auto-find.
- Ore runtime bergerak di belt dan jatuh ke slurry tank.
- Liquid runtime `Level3_Runtime_Tank_Liquid_Rising_75` naik di dalam tank.
- Water stream diarahkan ke center slurry tank, bukan ke posisi marker pipa yang meleset.

## Level 4 - Slurry Pump

Tujuan: player mengaktifkan pump dan memantau slurry mengalir ke Pre-Heater.

Alur:
- Player teleport/spawn di DCS.
- Player menekan tombol DCS 4 untuk menghidupkan slurry pump.
- Player mengatur flow rate ke 450 m3/h.
- Player laporan HT awal: slurry pump aktif.
- Player teleport ke pump/field.
- Player melihat cairan tersedot dari slurry tank.
- Slurry mengalir di pipa menuju Pre-Heater.
- Liquid di slurry tank turun/menguras sampai habis ke bawah.
- Setelah aliran mencapai Pre-Heater, player laporan HT akhir.
- Misi Level 4 selesai.

Status mekanik:
- Steam visual Pre-Heater tidak boleh muncul di Level 4. Steam hanya muncul dari mekanik Level 5.

## Level 5 - Steam Valve & Pre-Heater

Tujuan: player mengaktifkan Pre-Heater dari DCS lalu membuka steam valve secara fisik di field.

Alur:
- Player mulai di DCS.
- Player menekan tombol DCS 5 untuk menghidupkan Pre-Heater.
- Player laporan HT awal.
- Player teleport ke mesin Pre-Heater.
- Player grab handwheel/valve.
- Player memutar valve searah jarum jam dengan tangan VR.
- Visual valve/handwheel mengikuti putaran tangan player.
- Gauge/needle suhu naik mengikuti bukaan valve.
- Steam/uap muncul perlahan hanya setelah player berada di field dan valve mulai diputar.
- Steam emission dan audio naik perlahan sesuai persentase bukaan valve.
- Setelah suhu operasi tercapai, player laporan HT akhir.
- Misi Level 5 selesai.

Fix runtime:
- `Level5SteamValveController` mengunci steam sampai field unlock dan valve open percent > 0.
- `Level5SteamValveController` auto-find `RealSteamValve_Pivot_Lvl5` dan `Gauge_Needle`, menyimpan rotasi awal, lalu memutar handwheel dan jarum gauge relatif ke rotasi awal supaya animasi visual terlihat.
- `HandwheelVirtualPivot` disinkronkan ke sumbu rotasi controller supaya mesh handwheel dekoratif ikut berputar, bukan hanya pivot kosong.
- `PreHeaterVisualSync` tidak lagi menyalakan steam pada Level 4/5 agar tidak muncul sebelum valve diputar.
