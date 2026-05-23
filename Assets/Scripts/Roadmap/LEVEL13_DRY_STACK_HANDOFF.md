# OLIVIA Handoff - Level 13 Dry Stack Tailing

Tanggal: 2026-05-23

## Status Terakhir

Level 13 sudah dilanjutkan dari titik yang kemarin sempat terhenti. Tahap compile, verifikasi reference scene, clear console, dan save scene sudah selesai.

## Yang Sudah Dibuat

- Area scene baru: `Mesin Utama/Level13_DryStack_Field`
- Spawn point: `SpawnPoint_Lvl13`
- Controller: `Scripts/Simulation/Level13DryStackController.cs`
- Mesin/visual utama:
  - Final neutralization tank
  - Limestone/lime dosing hopper dan pour stream
  - pH monitor panel dengan needle + lampu red/green
  - Final filter press dengan 16 press plate
  - Filtrate channel
  - Cake transfer conveyor dengan 8 cake block
  - Dry stack storage dengan 6 pile
  - Safe cover dry stack
  - B3 warning signage
  - Environmental red/green beacon
  - Limestone dust FX dan dry stack dust FX

## Logic Gameplay

Urutan Level 13:

1. Saat `GameLevelManager` masuk `Level13_TailingWaste`, player diteleport ke `SpawnPoint_Lvl13`.
2. Player menekan DCS/local button 13.
3. Limestone dosing berjalan, pH naik dari 7.5 ke 8.5.
4. Setelah pH aman, visual limestone stop.
5. Final filter press aktif, plate merapat, filtrate keluar, moisture cake turun dari 34% ke 22%.
6. Cake bergerak lewat conveyor ke dry stack.
7. Dry stack pile muncul, safe cover aktif, beacon berubah hijau.
8. Controller memanggil `GameLevelManager.NotifyLevel13DryStackComplete()`.
9. Baru setelah itu laporan HT `"tailing aman"` diterima.

## File Yang Terkait

- `Scripts/Simulation/Level13DryStackController.cs`
- `Scripts/Simulation/GameLevelManager.cs`
- `Scripts/UI/DCSMonitorUI.cs`
- `Scenes/Level1.unity`
- `Materials/Generated/Level13_*.mat`

## Verifikasi Terakhir

- Unity mengenali `Level13DryStackController`.
- Script validation:
  - `Level13DryStackController.cs`: 0 error
  - `GameLevelManager.cs`: 0 error
  - `DCSMonitorUI.cs`: 0 error
- Scene reference Level 13:
  - missing reference: kosong
  - cake blocks: 8
  - dry stack piles: 6
  - filter plates: 16
  - conveyor rollers: 7
  - laporan HT: `DCS, netralisasi berhasil. pH delapan koma lima dan tailing aman di dry stack.`
- Unity console terakhir: 0 error, 0 warning setelah clear/reimport.
- Scene `Level1.unity` sudah disimpan.

## Catatan Penting

- Level 13 adalah showcase kedua setelah autoclave. Fokusnya harus terasa edukatif dan immersive: limbah B3 tidak boleh langsung dibuang, tapi harus pH aman dan cake masuk dry stack.
- Slurry/liquid tetap memakai `Assets/Materials/Color Utama/Slurry_Fill.mat`.
- Pipa transparan tetap memakai `Assets/Materials/Color Utama/Pipe_Transparent.mat`.
- Teleport tetap mengikuti aturan XR: `XROrigin.MoveCameraToWorldLocation` + `MatchOriginUpCameraForward`.
- Ada file `XR/Settings/OpenXRPackageSettings.asset` yang ikut modified setelah reimport OpenXR settings. Console sudah bersih, tapi jangan asal revert kalau masih dipakai Unity.

## Next Step Yang Masuk Akal

1. Playtest Level 12 -> Level 13 penuh dari tombol DCS dan laporan HT.
2. Lanjut Level 14 Emergency K3:
   - trigger kebocoran/pressure critical
   - alarm DCS
   - laporan emergency
   - tekan ESD
   - evaluasi evakuasi/safety
3. Setelah Level 14, lakukan polish full chain Level 8-14 supaya transisi, HUD, dan voice report terasa satu ekosistem.
