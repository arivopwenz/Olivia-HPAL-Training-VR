# 🟠 GAMEPLAY LEVEL 5 — STEAM VALVE & PRE-HEATER
**Lokasi:** Area Pre-Heater (lapangan, dekat pipa steam)
**Peran Pemain:** Field Worker
**Mesin Utama:** Pre-Heater (`Mesin Utama/PreHeater_Field_1`)
**Durasi target:** 4–6 menit gameplay
**Sumber kode:** `Level5SteamValveController.cs`, `GameLevelManager.cs`, `PreHeaterVisualSync.cs`

---

## 1. Tujuan Edukasi
Player belajar:
- Steam (uap panas) digunakan untuk memanaskan slurry sebelum masuk autoclave.
- Operator field membuka **katup steam manual** dengan memutar handwheel besar.
- Suhu Pre-Heater harus mencapai **180–200°C** sebelum proses lanjut ke acid injection.
- Komunikasi 2 arah: field operator buka valve, DCS operator monitor suhu.

---

## 2. Pre-Kondisi (Apa yang Harus Sudah Selesai)

| Syarat | Sumber |
|--------|--------|
| Level 4 (Slurry Pump) selesai dengan flow rate ±450 m³/h | `GameLevelManager._level4Complete` |
| Player sudah pakai APD lapangan (kacamata, respirator, sepatu, sarung tangan tahan panas) | `PhaseManager` |
| Slurry sudah mengalir di pipa Pump → Pre-Heater (animasi liquid dari Level 4) | `PipeLiquidFlow` |

---

## 3. Alur Gameplay Step-by-Step

### STEP 0 — Spawn & Briefing (~20 detik)
**Trigger:** `OnLevelStarted(Level5_SteamValve)` di `Level5SteamValveController`.
- Player teleport dari DCS ke `SpawnPoint_Lvl5` (depan Pre-Heater).
- HUD muncul pesan: *"Putar katup steam searah jarum jam untuk memanaskan Pre-Heater."*
- DCS Monitor: status fase = **"PEMANASAN AWAL"** (warna biru).
- Voice over (opsional): *"Field, buka katup steam. Naikkan suhu pre-heater ke target 190°C."*

### STEP 1 — Approach Steam Valve (~30 detik)
**Player sees:**
- Pipa steam besar (silinder horizontal) masuk ke Pre-Heater.
- **Steam Valve Handwheel** (roda kemudi dengan 4–6 jari-jari) terpasang di pipa.
- Tag/label di valve: *"STEAM INLET — PRE-HEATER"*.
- Warna handwheel: merah (steam line standar).
- Arrow indicator (`DirectionArrowIndicator`) menunjuk ke handwheel.
- Temperature gauge analog di samping Pre-Heater menunjukkan **25°C** (suhu ambient).

### STEP 2 — Grab & Putar Valve (~2–3 menit, INTERAKSI INTI)

**Mekanik VR:**
- `XRGrabInteractable` di `_valveWheel`.
- Player grab → `OnValveGrabbed()` set `_sedangDiGrab = true`.
- Player putar tangan **searah jarum jam** (rotasi sumbu Z forward).
- Akumulasi rotasi disimpan di `_rotasiAkumulasi`.
- **4 putaran penuh = 1440°** = valve 100% open.

**Visual feedback per rotasi:**
| Putaran | Valve Open % | Suhu (°C) | Steam FX | Audio |
|---------|--------------|-----------|----------|-------|
| 0 | 0% | 25 | mati | mati |
| 1 putaran | 25% | 70 | partikel tipis | desis pelan, pitch rendah |
| 2 putaran | 50% | 115 | sedang | desis sedang |
| 3 putaran | 75% | 160 | tebal | desis kuat |
| 4 putaran | 100% | 200 | maksimum | desis kencang, pitch tinggi |

**Yang berubah real-time:**
- `Steam_FX` (`ParticleSystem`) emisi naik linear: `_steamEmisiMax * _valveOpenPercent` (default max 80 partikel/dtk).
- `_steamAudio` (procedural hiss) volume dan pitch naik.
- `_gaugeNeedle` (jarum gauge analog) berputar dari 45° → -135°.
- `GameLevelManager.SetSuhu(_suhuSaatIni)` → DCS Monitor menampilkan suhu hidup.
- Pipa logam di sekitar Pre-Heater bisa dikasih emissive merah halus (panas) — saat ini belum diimplementasikan, kandidat improvement.

**Testing tanpa headset:** tahan **R key** untuk simulasi rotasi (lihat `SimulateValveInput()`).

### STEP 3 — Quest Tercapai (~15 detik)
**Trigger:** `_suhuSaatIni >= _suhuMinimumQuest` (default 180°C).
- Method `CheckQuestCompletion()` panggil `GameLevelManager.NotifyLevel5PreheaterReady()`.
- Flag `_level5PreheaterReady = true`.
- HUD ganti pesan: *"Suhu Pre-Heater mencapai target! Tahan T dan lapor: 'katup steam terbuka'."*
- Steam FX dan audio tetap nyala (proses pemanasan terus).
- Player **tidak boleh lapor** kalau suhu < 180°C — `GameLevelManager` akan reject voice report dengan pesan:
  *"Pre-heater belum mencapai suhu operasi. Buka katup steam sampai suhu minimal 180 C dulu."*

### STEP 4 — Lapor Walkie Talkie (~30 detik)
- Player ambil HT (sudah di pinggang dari Level 1).
- Tahan tombol PTT (T atau trigger XR).
- Bicara: **"katup steam terbuka"** atau alias: *"steam valve open"*, *"heater temperature up"*.
- Voice recognizer (`UnityEngine.Windows.Speech.KeywordRecognizer`) match → event `OnVoiceReportAccepted`.
- Audio balasan NPC dari `audio_level5_balasan`:
  *"Copy field, suhu pre-heater 190 derajat. Bersiap untuk injeksi asam di level 6."*

### STEP 5 — Transisi (~10 detik)
- Layar fade hitam (`_durasiFade = 2.5f`).
- Teleport ke `SpawnPoint_DCS`.
- Level 6 (Acid Injection) dimulai otomatis.

---

## 4. Asset & GameObject yang Terlibat

### Hierarchy yang harus ada di scene:
```
Mesin Utama/
├── PreHeater_Field_1/
│   ├── PreHeater_Vessel              (silinder utama)
│   ├── Steam_Inlet_Pipe              (pipa steam masuk)
│   ├── Steam_Valve_Wheel             ← _valveWheel target
│   │   ├── XRGrabInteractable
│   │   ├── Rigidbody (kinematic)
│   │   └── BoxCollider
│   ├── Steam_FX                      ← _steamParticle target
│   │   ├── ParticleSystem
│   │   └── AudioSource (procedural hiss)
│   ├── Temperature_Gauge/
│   │   ├── Gauge_Body
│   │   └── Gauge_Needle              ← _gaugeNeedle target
│   └── LED_Preheater                 (indikator hijau saat target)
└── (level lain)
```

### Material/Visual yang dibutuhkan:
- Material steam pipe: silver/abu metalik dengan emissive merah halus saat hot.
- Material valve wheel: merah safety (`Ind_Safety_Yellow.mat` atau bikin red variant).
- Particle steam: putih opaque dengan soft edge, lifetime 1.5s, gravity ke atas.
- Audio: procedural di-generate `BuatClipSteamHiss()` (sudah ada).

---

## 5. Field Inspector yang Bisa Di-tweak

Di `Level5SteamValveController` (Inspector):

| Field | Default | Fungsi |
|-------|---------|--------|
| `_totalDerajatFullOpen` | 1440 | Berapa derajat total = 100% (1440 = 4 putaran) |
| `_kecepatanRotasiMax` | 180 | Derajat/detik saat keyboard simulasi |
| `_suhuAwal` | 25 | Suhu start (°C) |
| `_suhuTarget` | 200 | Suhu saat valve full open |
| `_suhuMinimumQuest` | 180 | Suhu minimum untuk lulus quest |
| `_steamEmisiMax` | 80 | Partikel/detik saat full open |
| `_steamVolumeMax` | 0.7 | Volume audio max |
| `_steamPitchMin` / `_steamPitchMax` | 0.6 / 1.3 | Range pitch hiss |
| `_gaugeAngleMin` / `_gaugeAngleMax` | 45° / -135° | Rotasi needle 0°C / 200°C |

---

## 6. Sistem Skor Level 5 (jika sudah aktif)

```
┌────────────────────────────────────────────────┐
│  RAPOR LEVEL 5 — STEAM VALVE                    │
├────────────────────────────────────────────────┤
│  1. APD Lengkap (25)                            │
│     Pakai sarung tangan tahan panas             │
│  2. Buka Valve (25)                             │
│     Suhu mencapai 180–200°C                     │
│  3. Voice Report (25)                           │
│     Keyword tepat & timing benar                │
│  4. Kepatuhan SOP (25)                          │
│     Tidak skip-step, tidak buka berlebihan      │
│  TOTAL: 100  | LULUS: ≥70                       │
└────────────────────────────────────────────────┘
```

---

## 7. Bug & Smell yang Perlu Diperbaiki di Level 5

> **Update 27 Mei 2026:** Setelah re-audit kode aktual `Level5SteamValveController.cs` (versi terbaru), beberapa issue sudah diselesaikan. Status di-mark per item.

### 7.1. ✅ SELESAI — Teleport saat Level 5 mulai
- Status: `SeqTeleportKeField()` + `EnsureFieldSpawnPoint()` sudah implement fade + teleport ke depan handwheel.
- Spawn point auto-create di runtime (`SpawnPoint_Lvl5_PreHeater (Runtime)`) berdasarkan `_handwheelReference` + `_offsetSpawnField`.

### 7.2. ✅ SELESAI — XRGrab nyambung ke rotasi valve
- Status: `WireHandwheelGrab()` subscribe `selectEntered`/`selectExited`. `OnHandwheelGrabbed` simpan `_grabInteractor` dan `_grabYawTanganLastFrame`.
- Logic rotasi VR: `AmbilYawTanganRelatifValve()` project forward tangan ke bidang sumbu valve, hitung `Mathf.DeltaAngle` per frame. Hanya searah jarum jam yang akumulasi (steam tidak bisa di-undo).

### 7.3. ✅ SELESAI — Arrow indicator ke valve
- Status: `ShowArrowKe(_handwheelGroup)` dipanggil setelah teleport ke field. `HideArrow()` saat suhu tercapai.

### 7.4. 🟠 BELUM — Validasi APD lapangan
- Saat ini Level 5 tidak cek apa-apa untuk APD. Player bisa muncul di lapangan tanpa sarung tangan tahan panas atau respirator.
- **Fix:** Ikuti pola Level 3. Tambah cek `PhaseManager.Instance.Level3FieldApdLengkap` (atau bikin `Level5FieldApdLengkap` baru kalau APD-nya beda) sebelum `SeqTeleportKeField()` dijalankan.
- Estimasi: 30 menit.

### 7.5. ✅ SELESAI — Steam FX & Audio cleanup
- Status: `StopSteamFx()` dipanggil di `OnDisable`, `ResetState`, dan `SeqKembaliKeDcs`.

### 7.6. ✅ SELESAI — FindObjectOfType deprecated
- Status: sudah pakai `FindFirstObjectByType<PlayerHUD>()` dan `FindFirstObjectByType<DirectionArrowIndicator>()`.

### 7.7. 🟡 BELUM — Feedback "valve sudah penuh"
- `_rotasiAkumulasi` di-clamp ke `_totalDerajatFullOpen` (1440°), tapi tidak ada audio/haptic saat batas tercapai.
- **Fix:** Saat `_valveOpenPercent >= 1.0f` baru pertama kali, mainkan AudioClip "click" + haptic ke `_grabInteractor`. Mark flag `_valveSudahFull` agar tidak loop.
- Estimasi: 30 menit.

### 7.8. 🟡 BELUM — Visual slurry panas di pipa
- `PreHeaterVisualSync.cs` saat ini sync ke flow rate, bukan suhu.
- **Fix:** Extend `PreHeaterVisualSync` baca `Level5SteamValveController.SuhuSaatIni`, lerp warna emissive material slurry dari abu (25°C) ke oranye redup (200°C).
- Estimasi: 1 jam (perlu shared material atau MaterialPropertyBlock).

### 7.9. 🟡 BELUM — Alarm suhu over 220°C
- Tidak ada batas atas. Karena `_suhuTarget = 200f` dan `_rotasiAkumulasi` di-clamp, secara hard tidak bisa over. Tapi kalau `_suhuTarget` dinaikkan untuk skenario ekstrem, butuh alarm.
- **Fix:** Opsional, hanya untuk hard mode atau Level 14. Skip kalau bukan prioritas.
- Estimasi: 1 jam.

### 7.10. 🟡 TAMBAHAN — Voice listener di phase yang salah masih nyambung
- `OnVoiceReportAccepted` cek `_phase == MenungguLaporanAwal` dan `MenungguLaporanAkhir`. Tapi kalau player selesai keyboard simulasi tanpa VR grab, phase bisa loncat. Tidak kritis.
- Skip kecuali muncul bug konkret saat playtest.

---

## 8. Checklist QA Level 5

- [ ] Player teleport ke depan Pre-Heater saat Level 5 mulai (BUG 7.1)
- [ ] Valve handwheel bisa di-grab di VR
- [ ] Valve bisa diputar 4 putaran penuh (BUG 7.2)
- [ ] Steam FX naik intensitas seiring valve dibuka
- [ ] Audio steam hiss volume + pitch berubah real-time
- [ ] Gauge needle berputar 45° → -135° saat suhu 25 → 200°C
- [ ] DCS Monitor menampilkan suhu hidup
- [ ] HUD menampilkan misi & checklist
- [ ] Voice report `"katup steam terbuka"` accept saat suhu ≥180°C
- [ ] Voice report reject dengan pesan jelas saat suhu <180°C
- [ ] Audio balasan NPC ter-trigger setelah voice report sukses
- [ ] Steam FX & audio mati saat transisi ke Level 6 (BUG 7.5)
- [ ] Skor Level 5 dihitung dengan benar di rapor akhir

---

## 9. Prioritas Perbaikan (Urutan Kerja)

| Prioritas | Item | Status | Effort |
|-----------|------|--------|--------|
| 🔴 P0 | Wire XRGrab ke rotasi valve (7.2) | ✅ SELESAI | — |
| 🔴 P0 | Teleport ke field saat Level 5 mulai (7.1) | ✅ SELESAI | — |
| 🟠 P1 | Cleanup Steam FX/Audio saat OnDisable (7.5) | ✅ SELESAI | — |
| 🟠 P1 | Arrow indicator ke valve (7.3) | ✅ SELESAI | — |
| 🟠 P1 | Validasi APD lapangan (7.4) | 🔧 BELUM | 30 menit |
| 🟡 P2 | Ganti FindObjectOfType deprecated (7.6) | ✅ SELESAI | — |
| 🟡 P2 | Feedback valve full (7.7) | 🔧 BELUM | 30 menit |
| 🟡 P2 | Visual slurry panas di pipa (7.8) | 🔧 BELUM | 1 jam |
| 🟡 P3 | Alarm suhu over 220°C (7.9) | ⏸ OPSIONAL | 1 jam |

---

> Dokumen hidup. Update checklist QA setelah tiap perbaikan dan playtest.
