# 🛠️ AUDIT BUG & REFACTOR — OLIVIA VR HPAL
**Tanggal:** 27 Mei 2026
**Tujuan:** Daftar masalah nyata di codebase + rekomendasi pembersihan agar tidak boros file dan tidak ngawur.
**Sumber data:** Unity console, file system Assets/Scripts, ukuran tiap script (line count + KB).

---

## 0. Ringkasan Cepat

| Indikator | Nilai | Catatan |
|-----------|------:|---------|
| Total `.cs` di `Assets/` | 114 | Termasuk template VR bawaan |
| Script asli proyek (`Assets/Scripts/**`) | 53 | Inti gameplay |
| File terbesar (lines) | 1.510 | `GameLevelManager.cs` — wajib dipecah |
| Folder placeholder kosong | 1 set | `Assets/Script/` (tanpa "s") punya `Core/Phase1/Phase2/Phase3/Roadmap/UI` semua kosong |
| Error console aktif | 4 (2 unik) | Kinematic rigidbody + haptic XR Simulated |
| Dual state manager | Ya | `PhaseManager` + `GameLevelManager` jalan paralel |
| Scene di `Scenes/` | 9 | Beberapa kandidat scene tidak terpakai (lihat §3) |

---

## 1. 🔴 Bug Aktif (Konfirmasi dari Unity Console)

### 1.1. Kinematic Rigidbody Velocity (REPRODUSIBEL — log ganda)
- **File:** `Assets/Scripts/Simulation/PhaseManager.cs`
- **Baris:** 507–508
- **Pesan:**
  - `Setting linear velocity of a kinematic body is not supported.`
  - `Setting angular velocity of a kinematic body is not supported.`
- **Penyebab:** Method `PastikanMaskerAdaDiSocketBaju()` melakukan urutan:
  1. Set `rb.linearVelocity = Vector3.zero`
  2. Set `rb.angularVelocity = Vector3.zero`
  3. **Baru** set `rb.isKinematic = true`
  Tapi method ini juga dipanggil saat respirator sudah dalam state kinematic (mis. dari socket baju), sehingga step 1 & 2 dieksekusi pada body kinematic.
- **Perbaikan:** Cek `if (!rb.isKinematic)` dulu, atau set `isKinematic = false` sementara, lalu reset velocity, lalu set kembali ke `true`. Idiom yang aman:
  ```csharp
  if (rb.isKinematic) rb.isKinematic = false;
  rb.linearVelocity = Vector3.zero;
  rb.angularVelocity = Vector3.zero;
  rb.isKinematic = true;
  rb.useGravity = false;
  ```
- **Dampak:** Spam error tiap kali Level 3 mereset masker. Bisa membingungkan saat debugging hal lain.

### 1.2. Haptic Capabilities XR Simulated (Editor only)
- **File:** package XR Interaction Toolkit (bukan kita)
- **Pesan:** `Failed to get haptic capabilities of XRSimulatedController... error code -1`
- **Penyebab:** Hanya muncul saat XR Device Simulator aktif di Editor.
- **Perbaikan:** Tidak perlu fix kode — abaikan di Editor, atau matikan XR Device Simulator saat tidak debug controller. Hilang otomatis di build runtime headset.

### 1.3. APD Lapangan Belum Lengkap (Warning Berulang)
- **File:** `Level3OreSlurryController.cs:272`
- **Pesan:** `Pakai APD lapangan dulu sebelum ke crusher/slurry: Walkie Talkie / HT, Kacamata Pelindung, Respirator / Masker Gas.`
- **Catatan:** Ini bukan bug kode, tapi indikasi flow Level 3 men-trigger validasi APD walau player belum sampai ke trigger. Cek apakah trigger zone-nya tidak dipanggil dua kali di awal level.

---

## 2. 🟠 Smell Arsitektur (Bukan Crash, Tapi Sumber Bug Berkelanjutan)

### 2.1. Dua State Manager Jalan Bareng
- `PhaseManager.cs` (627 baris) — masih pegang APD state, walkie taken flag, level3FieldApdLengkap, dst.
- `GameLevelManager.cs` (1.510 baris) — pegang 14 level + transisi.
- **Masalah:** Roadmap menyatakan PhaseManager harusnya jadi sub-state kecil saja, tapi kenyataannya level controller masih ambil data APD via `PhaseManager.Instance.*` (Level3, WalkieTalkie, TaskTrigger, dll). Akibatnya:
  - Sulit tahu siapa pemilik truth.
  - Dependency siklik: level controller butuh APD state, APD state hidup di scene singleton terpisah.
- **Rekomendasi:**
  1. Pindahkan APD state dari `PhaseManager` ke `ApdStateService` (file baru kecil ~150 baris) atau jadikan field di `GameLevelManager`.
  2. Sisakan `PhaseManager` cuma sebagai event bus (atau hapus total). Sebelum hapus: cek 8 file pemakai (lihat §2.2).

### 2.2. Daftar Pemakai `PhaseManager.Instance` (yang harus diupdate kalau dirombak)
| File | Cara Pakai |
|------|-----------|
| `Scripts/UI/PlayerHUD.cs` | event APD + `Level3FieldApdLengkap` |
| `Scripts/UI/Level1ApdTaskHintDirector.cs` | baca state APD per item |
| `Scripts/UI/GlobalTaskArrowDirector.cs` | reference instance |
| `Scripts/Simulation/Level3OreSlurryController.cs` | validasi APD lapangan, glow respirator |
| `Scripts/Simulation/TaskTrigger.cs` | dispatch event pemakaian APD |
| `Scripts/Simulation/WalkieTalkieManager.cs` | cek APD lengkap + WT diambil |
| `Scripts/Simulation/WalkieTalkieMouthPttTrigger.cs` | cek WT diambil |
| `Scripts/Simulation/WalkieTalkieWearableSocket.cs` | mark WT diambil |
| `Scripts/Simulation/MachineActivationButton.cs` | cek state |

### 2.3. `Level1ApdTaskHintDirector.cs:119` Anti-Pattern
```csharp
PhaseManager phase = PhaseManager.Instance != null
    ? PhaseManager.Instance
    : FindAnyObjectByType<PhaseManager>();
```
Dipanggil setiap kali `IsComplete(task)`. Kalau dipanggil dari Update/loop hint, `FindAnyObjectByType` mahal. Ganti jadi cache sekali di `OnEnable`, atau cukup `PhaseManager.Instance` saja.

### 2.4. Folder Duplikat: `Assets/Script/` vs `Assets/Scripts/`
- `Assets/Script/` punya 6 subfolder kosong (`Core, Phase1, Phase2, Phase3, Roadmap, UI`) — semuanya nol file.
- Konvensi proyek nyatanya pakai `Assets/Scripts/` (dengan "s").
- **Rekomendasi:** Hapus folder `Assets/Script/` beserta `.meta`-nya. Tidak ada kode yang pakai.

### 2.5. Console Log Spam dari Path Produksi
File berikut nge-log info pakai `Debug.Log` di kode produksi (bukan dijaga `[Conditional]` atau flag):
- `FlowRateControlPanel.cs:109,138` — auto-find logging tiap inisialisasi.
- `DCSParameterControl.cs:162` — log target tercapai (warna hijau).
- `DCSTombolPanel.cs:129` — log tombol ditekan.
- `Level4SlurryPumpController.cs:183` — log set slurry tank penuh.
- `Level9FlashVesselController.cs:168` — log stable.
- `WalkieTalkieManager.cs:734` — `[HT-LABEL] ...` setiap event mic.
- **Dampak:** Console penuh saat playtest, error nyata jadi tertimbun.
- **Rekomendasi:** Bungkus dengan flag `_verbose` Inspector atau ganti ke `Debug.Log` hanya di `#if UNITY_EDITOR`.

### 2.6. WalkieTalkieManager Selalu Buka Mic Saat PTT
- `_debugMicInput = true` (default) → setiap kali PTT ditekan, `Microphone.Start` dipanggil untuk monitor amplitudo (untuk fallback "ada suara").
- Ini menjadi cost dan log spam, bahkan setelah debugging selesai.
- **Rekomendasi:** Default `_debugMicInput = false`. Aktifkan hanya saat butuh diagnose. Atau pisahkan jadi script `WalkieTalkieMicMonitor.cs` opsional.

### 2.7. File Controller per Level Terlalu Gendut

| File | Lines | Kondisi |
|------|------:|---------|
| `GameLevelManager.cs` | **1510** | Mengandung 14 level state + event + helper UI + voice mapping. Wajib dipecah |
| `Level3OreSlurryController.cs` | 830 | Validasi APD + glow + teleport + audio + masker stub semua di sini |
| `PlayerHUD.cs` | 801 | Logic teks per-level + event APD + arah quest |
| `DCSMonitorUI.cs` | 692 | Render parameter + alarm + 14 tombol logic |
| `WalkieTalkieManager.cs` | 629 | PTT + recognizer + balasan NPC + mic monitor |
| `Level4SlurryPumpController.cs` | 627 | Animasi liquid + teleport + slurry tank fill |
| `Level7AutoclaveController.cs` | 599 | Reactor sim + agitator + audio prosedural |

- **Rekomendasi:** Pecah jadi partial class atau helper class:
  - `GameLevelManager.Events.cs` (semua `static event Action<...>`)
  - `GameLevelManager.VoiceMap.cs` (mapping keyword → level)
  - `GameLevelManager.Transitions.cs` (logic teleport DCS↔Field)
  - `Level3.Glow.cs` / `Level3.ApdValidation.cs` partial files

---

## 3. 🟡 File Mubazir / Boros

### 3.1. Folder & File Kandidat Hapus
| Path | Alasan | Aksi |
|------|--------|------|
| `Assets/Script/` (semua subfolder kosong) | Tidak dipakai, salah konvensi | **Hapus** |
| `Assets/Scenes/SampleScene.unity` | Default Unity, bukan scene proyek | Konfirmasi dulu, lalu hapus |
| `Assets/Scenes/BasicScene.unity` | Tidak disebut di roadmap | Konfirmasi |
| `Assets/_Recovery/` | Folder recovery | Cek isinya, biasanya bisa dihapus setelah commit |
| `Level7AutoclaveInstall.log` di root Assets | Log instalasi | Hapus, atau pindah ke `.kiro/logs/` |
| `Assets/Scripts/Roadmap/Reference machine/` | 2 PNG referensi | OK kalau dipakai sebagai dokumentasi, tapi pertimbangkan keluar dari `Assets/` agar tidak kena import Unity |

### 3.2. Scene Aktif vs Tidak
Berdasarkan handoff Level 13: scene aktif gameplay = `Scenes/Level1.unity`. Scene berikut perlu konfirmasi:
- `BasicScene.unity` — scene kosong?
- `ControlRoomScene.unity` — masih dipakai?
- `EmergencyScene.unity` — apakah ditarik dari Level 14 standalone?
- `GameScene.unity` / `PlantFloorScene.unity` / `ResultScene.unity` / `TutorialScene.unity` — semua kandidat scene per fase yang sekarang sudah dimasukkan ke Level1 monolitik.
- **Rekomendasi:** Kalau memang semua gameplay digabung di `Level1.unity`, pindahkan scene-scene di atas ke folder `Assets/Scenes/_Archive/` (atau hapus setelah konfirmasi).

### 3.3. Folder Art Redesign
Sebanyak 18 folder di `Assets/Art/*Redesign/` — masing-masing punya `.blend`, `.fbx`, `.blend1`, dan kadang `.py` script. Setelah model diimpor ke `Assets/Models/` atau dipakai di scene:
- `.blend1` (file backup Blender) **selalu boleh dihapus** dari Unity import — sudah di `.gitignore`-kan kalau perlu.
- Script `.py` Blender tidak dipakai Unity — bisa dipindah keluar `Assets/` agar tidak ikut diproses AssetDatabase.

---

## 4. 🟢 Inkonsistensi & Bug Logic Lebih Halus

### 4.1. Typo Method Name
- `PhaseManager.OnRespiratiorWorn()` — typo: harusnya `OnRespiratorWorn`.
- Dipanggil dari `TaskTrigger.cs:79`. Rename pakai semantic rename agar konsisten.

### 4.2. `MachineActivationButton.cs` Masih Bergantung `phaseManager`
- File ini dapat instansi via `Object.FindAnyObjectByType<PhaseManager>` — tapi proyek sudah mau move ke `GameLevelManager`. Kandidat untuk diganti event `GameLevelManager.OnMachineActivationRequested`.

### 4.3. `TaskTrigger.cs:46` `Debug.LogError` saat startup
Kalau scene tertentu memang tidak butuh PhaseManager (mis. Hub atau Result), error ini akan muncul tanpa konteks. Ganti jadi `LogWarning` atau set self-disable (`enabled = false; return;`).

### 4.4. Naming Backing Field Tidak Konsisten
Beberapa file pakai `_camelCase`, lain `m_camelCase`, lain `lowercase`. Misal `WalkieTalkieManager` pakai `_audioSourceRadio`, `Level4SlurryPumpController` pakai `_pipaUtama`, sementara `MachineActivationButton` pakai `phaseManager`. Bukan blocker, tapi bagusnya satu konvensi.

### 4.5. Event Static Tanpa Unsubscribe yang Eksplisit di Beberapa Tempat
`Level3OreSlurryController.cs` subscribe `PhaseManager.OnApdItemWorn += OnApdItemWorn;` di `OnEnable` — tapi handler di-unsubscribe di `OnDisable`. Cek apakah semua event subscriber simetris (subscribe == unsubscribe). Kalau tidak, leak saat scene reload.

---

## 5. 📋 Rekomendasi Urutan Pengerjaan (Prioritas)

> Aturan main: kerjakan satu per satu, commit per item, tes ulang sebelum lanjut.

### Sprint 1 — Quick Wins (1–2 jam)
1. **Fix bug 1.1 (kinematic rigidbody velocity)** — 1 file, ~5 baris.
2. **Hapus folder `Assets/Script/`** (kosong) + `.meta`-nya.
3. **Hapus `Level7AutoclaveInstall.log`** di root Assets, pindahkan ke folder logs.
4. **Default `_debugMicInput = false`** di `WalkieTalkieManager`.
5. **Rename typo `OnRespiratiorWorn` → `OnRespiratorWorn`** (semantic rename, otomatis update pemakai).

### Sprint 2 — Bersihkan Console (2–3 jam)
6. Bungkus 6 `Debug.Log` dari §2.5 dengan `[Conditional("OLIVIA_VERBOSE")]` atau flag Inspector `_verbose`.
7. Audit 9 scene di `Assets/Scenes/`. Pindahkan yang tidak dipakai ke `Scenes/_Archive/`.
8. Hapus `.blend1` files dari `Assets/Art/**/*` dan tambahkan `*.blend1` ke `.gitignore`.

### Sprint 3 — Refactor Aman (1–2 hari)
9. **Pecah `GameLevelManager.cs` jadi partial class** (3–4 file, masing-masing 300–400 baris).
10. **Buat `ApdStateService.cs`** sebagai pemilik APD state. PhaseManager tetap ada tapi delegate ke service ini.
11. **Pisah `Level3OreSlurryController` Glow logic** ke helper `Level3RespiratorGlow.cs`.
12. **Cache `PhaseManager.Instance` di `Level1ApdTaskHintDirector`** sekali, hapus `FindAnyObjectByType` di hot path.

### Sprint 4 — Konsolidasi (1 hari)
13. Pilih satu konvensi naming backing field. Jalankan semantic rename pada outlier (`MachineActivationButton.phaseManager` → `_phaseManager`, dst).
14. Audit semua `event +=` dan pastikan ada `event -=` simetris.
15. Tambahkan `Debug.Assert` di constructor singleton untuk deteksi double-instance.

---

## 6. 📁 Daftar Script Aktif (Referensi Cepat)

### `Assets/Scripts/Simulation/` — 33 file
> Dari yang paling besar:
> `GameLevelManager.cs` (1510), `Level3OreSlurryController.cs` (830), `WalkieTalkieManager.cs` (629), `PhaseManager.cs` (627), `Level4SlurryPumpController.cs` (627), `Level7AutoclaveController.cs` (599), `Level13DryStackController.cs` (543), `Level6AcidInjectionController.cs` (518), `Level8MonitoringController.cs` (511), `Level11MHPController.cs` (478), `Level12TailingFilterController.cs` (468), `Level10CCDController.cs` (402), `Level14EmergencyController.cs` (392), `Level9FlashVesselController.cs` (356), `PipeLiquidFiller.cs` (324), `Level5SteamValveController.cs` (306), `SlurryPumpVisualSync.cs` (277), `ProcessPipeNetwork.cs` (272), `PreHeaterVisualSync.cs` (240), `SlurryFXController.cs` (225), dan 13 lainnya.

### `Assets/Scripts/UI/` — 13 file
> `PlayerHUD.cs` (801), `DCSMonitorUI.cs` (692), `LevelTransitionChoicePanel.cs` (252), `Level1ApdTaskHintDirector.cs` (241), `DCSParameterControl.cs` (228), `DirectionArrowIndicator.cs` (224), `GlobalTaskArrowDirector.cs` (219), dan 6 lainnya.

### `Assets/Scripts/System/` — 7 file
> `LevelTeleportManager.cs` (261), `LoadingScreenManager.cs` (228), `LockerHubController.cs` (204), `LevelLoader.cs`, `ApdDisplayItemStabilizer.cs`, `SocketAutoTidy.cs`, `TorsoChestAnchor.cs`, `XRSocketHoverMeshSuppressor.cs`.

### `Assets/Editor/` — 1 file
> `OliviaUIBuilder.cs`

---

## 7. ✅ Definition of Done

Refactor ini selesai kalau:
- [ ] Console editor bersih saat masuk play mode (0 error nyata, hanya warning XR Simulator).
- [ ] Tidak ada `Debug.Log` produksi yang nge-spam.
- [ ] `GameLevelManager.cs` di bawah 600 baris per file (boleh partial multi file).
- [ ] Tidak ada folder kosong di `Assets/`.
- [ ] Satu sumber truth untuk APD state.
- [ ] Semua event `+=` punya pasangan `-=`.
- [ ] Satu konvensi naming backing field (`_camelCase`).
- [ ] `.gitignore` mencakup `*.blend1`, `Library/`, `Temp/`.

---

> Dokumen ini status hidup. Update kolom checklist sembari progress refactor. Setelah Sprint 1–4 selesai, lakukan playtest penuh Level 1 → Level 14 untuk memastikan tidak ada regresi.
