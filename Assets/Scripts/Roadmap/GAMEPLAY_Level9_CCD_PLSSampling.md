# GAMEPLAY Level 9 — CCD Activation + PLS Sampling + Lab QC

**Display**: "Level 9 - CCD" (enum internal: `Level10_CCD`, tombol DCS 9, controller `Level10CCDController.cs`)

## Konteks Industrial (HPAL Real-World)

Setelah flash letdown (Level 8), slurry asam panas (1.05 atm, 102°C, mengandung Ni/Co/Fe terlarut + residu padat) masuk ke **CCD train (Counter-Current Decantation)** — 5-7 thickener seri yang mencuci padatan dari larutan berharga.

Sumber: Nickel Institute (2025), Moa Bay flowsheet (Cuba), Coral Bay (Filipina), Taganito HPAL case study, BC Campus hydrometallurgy textbook.

### Prinsip CCD
```
Feed slurry (dari flash) → Thickener-1 → Th-2 → Th-3 → Th-4 → Th-5
                              ↑ underflow (padat)        ↓
                          ←──────── overflow (cair PLS) ─┘
                             (counter-current wash water flow)
```
- **Underflow** (padat) jalan ke thickener berikutnya (T1→T5).
- **Overflow** (cair) jalan balik (T5→T1) sebagai wash water.
- **PLS keluar di Th-1 overflow**: ini cairan jernih kaya Ni/Co yang akan di-analisa lab QC.
- Tailing solid keluar di Th-5 underflow (sudah dicuci, dikirim ke neutralization Level 11).

### Parameter SOP per Thickener
| Stage | Sample Point | Ni (g/L) | Co (g/L) | Free Acid (g/L) | Fe (g/L) |
|-------|--------------|----------|----------|-----------------|----------|
| Th-1 PLS (richest) | Overflow | 5.0-5.5 | 0.42-0.48 | 17-19 | 0.6-1.0 |
| Th-3 PLS (mid) | Overflow | 4.2-4.8 | 0.38-0.44 | 14-17 | 0.4-0.7 |
| Th-5 PLS (wash) | Overflow | 0.8-1.5 | 0.05-0.15 | 5-7 | 0.1-0.3 |

Wash efficiency target: **≥95%** (Ni di tailing < 0.05 g/L).

---

## Tujuan Gameplay (Level 9)

Player adalah operator field/lab yang:
1. Aktivasi CCD train via DCS 9 (start rake arms + flow).
2. Tunggu CCD stabil (rake muter, slurry settle, overflow jernih).
3. Ambil 3 sample PLS dari sample station (Th-1, Th-3, Th-5 overflow).
4. Bawa sample ke laboratorium QC.
5. Submit sample ke spectrometer + titration → analisa Ni/Co/Fe + free acid.
6. Verifikasi hasil dalam SOP → ACCEPT.
7. Voice report HT: "CCD aktif, PLS lulus QC".

---

## Flow Step-by-Step

### Phase 1 — DCS 9 Start
1. Player tekan tombol DCS 9 di control room.
2. HUD: "CCD aktif. Pergi ke field, ambil 3 sample PLS dari overflow Th-1, Th-3, Th-5."
3. Fade teleport ke CCD field area.

### Phase 2 — Observe CCD Separation (~14 detik)
4. Rake arms muter (4 RPM) di tiap thickener.
5. Slurry masuk T1 → mengendap → overflow naik perlahan.
6. Particle FX di interface padat-cair.
7. Audio: drive motor humming.
8. Setelah stabil → audio "complete chime" → `NotifyLevel10CCDComplete()`.

### Phase 3 — Ambil Sample PLS (3 station)
9. Setelah CCD stable, 3 sample station spawn dekat thickener:
   - **Station Th-1** (PLS pekat, ungu) — di samping Thickener-1
   - **Station Th-3** (PLS mid, ungu pucat) — Thickener-3
   - **Station Th-5** (wash overflow, biru-abu) — Thickener-5
10. Player jalan ke tiap station (dekat <2.8m horizontal) → botol terisi otomatis dengan animasi liquid naik (2 detik).
11. HUD progress: "(1/3, 2/3, 3/3)".
12. 3 sample terkumpul → HUD: "Masuk LAB QC, tekan [L] untuk submit."

### Phase 4 — Lab QC Analysis (di gedung lab)
13. Player masuk gedung **LAB QC PLS** (model Blender, `CCDLab.fbx`):
    - Spectrometer ICP-OES (rotor + lampu indikator)
    - Sample inlet 3 botol (PLS Th-1/3/5)
    - Titration station (burette + Erlenmeyer + magnetic stirrer)
    - Komputer + monitor
    - Result screen besar
    - Fume hood
    - Glassware shelf
14. Player tekan [L] → animasi analisa berjalan (5 detik):
    - 3 inlet liquid terisi berurutan (player "memasukkan" sample)
    - Spectrometer rotor berputar
    - Result screen progress bar 0→100%
    - "QC SELESAI - PLS dalam SOP ✓"
15. Pop-up canvas hasil detail (Ni, Co, Fe, free acid per stage + verdict).
16. Player tap **ACCEPT** → `NotifyLevel10SamplePLSAccepted()`.

### Phase 5 — Voice Report
17. HUD: "Lapor HT (tahan T): 'CCD aktif, PLS lulus QC'."
18. Player tahan T + bicara → voice accepted.
19. Mission Complete canvas (STAY / KEMBALI KE DCS → Level 10 MHP).

---

## Mekanik VR-Native

### 1. Sample Proximity (bukan keypress)
Player physically dekati 3 sample station di tiap thickener. Mendetect jarak horizontal (Y-ignored) supaya tinggi kamera tidak ganggu trigger. Bottle terisi animasi (scale Y 0→1.7 dalam 2 detik), warna sesuai stage (ungu pekat → ungu pucat → biru-abu).

### 2. Lab Building Real Architecture (Blender Headless)
Lab dibangun via Blender 5.1 headless mode (`--background --python build_ccd_lab.py`). Auto-export FBX + import Unity. Detail:
- Ruangan 10m × 9m × 4m, dinding+lantai+plafon+pintu+papan nama
- Spectrometer ICP-OES (rotor animasi + 5 LED indikator + label "Ni/Co/Fe ASSAY")
- Sample inlet 3 slot (botol kaca + liquid material)
- Titration station (burette glass tube + Erlenmeyer flask + magnetic stirrer + clamp)
- Computer setup (monitor + keyboard + mouse + stand)
- Big result screen (3.2m wide, dinding belakang)
- Fume hood (kabinet kaca dengan beaker isi liquid kuning + LED hijau)
- Glassware shelf (8 beaker/flask dekoratif di rak dinding kiri)
- Side desk + stool + logbook
- 4 panel ceiling light dengan emission

### 3. Lab QC Mini-Game
Pop-up canvas dengan 3 baris data + verdict + ACCEPT. Player baca data → cek kalau dalam SOP → tap ACCEPT. Mengajarkan interpretasi data lab.

### 4. Real-World Validation
Sample value menggunakan range realistik dari Moa Bay/Coral Bay flowsheet. Wash efficiency Th-5 ≈ 95% (Ni residual 1.1 g/L vs Th-1 5.2 g/L).

---

## Object Names di Scene (auto-find)

### Existing CCD Field
- `CCD_Field` atau `CCD_BlenderRig` (cari via AutoFindReferences)
- Rake arms (3-7 buah) — animasi rotate
- Feed liquid + overflow liquid + settled mud layers
- Particle FX `_separationFx`

### Sample Stations (runtime)
- `L9_PLS_SampleStation_Th1` (di samping Thickener 1)
- `L9_PLS_SampleStation_Th3`
- `L9_PLS_SampleStation_Th5`

### Lab Building (runtime, dari CCDLab.fbx)
- `L9_LabBuilding` (root prefab instance)
- Children:
  - `CCDLab_Spectrometer_Rotor` (rotor analyzer animasi)
  - `CCDLab_InletLiquid_1/2/3` (liquid yang animasi terisi)
  - `CCDLab_ResultScreen` (anchor untuk TextMesh hasil)
  - `CCDLab_MonitorScreen` (decorative)
  - `CCDLab_Spec_LED_0..4` (lampu indikator)
  - `CCDLab_Erlenmeyer_*`, `CCDLab_Burette_Tube` (titration)
  - `CCDLab_Fume_*` (fume hood)
  - `CCDLab_Glassware_0..7` (rak alat)

---

## Hotkey Debug Keyboard

| Key | Action |
|-----|--------|
| `L` | Submit sample PLS ke lab QC analyzer (setelah 3 sample terkumpul) |
| `T` | Voice report HT (tahan, bicara, lepas) |

---

## Controller File

`Assets/Scripts/Simulation/Level10CCDController.cs` — sudah lengkap dengan:
- CCD activation sequence (rake arm animation, slurry settle, overflow)
- 3 sample station fisik (proximity-based fill)
- Lab building load FBX (`CCDLab.fbx` prioritas, fallback `QCLab.fbx`)
- Lab analysis coroutine (slot fill → rotor spin → progress → result)
- Lab QC pop-up canvas (3 row data + ACCEPT button)
- Voice report integration (`NotifyLevel10SamplePLSAccepted`)

## Asset 3D
- `Assets/Art/Lab/CCDLab.fbx` (lab khusus Level 9, dibuild via `build_ccd_lab.py` headless)
- `Assets/Art/Lab/QCLab.fbx` (fallback)

## Catatan Implementasi
1. Lab dibangun via Blender headless — TIDAK ada Blender GUI yang nyala (laptop ringan).
2. Lab di-instantiate runtime saat CCD stable. Posisi: di sebelah CCD field (offset +X 6m).
3. Sample stations spawn di tiap thickener berdasarkan bounds CCD field.
4. Voice keyword: "CCD aktif" (existing).
