# GAMEPLAY Level 8 - Flash Letdown Vessel Train (3-Stage Pressure & Heat Recovery)

## Riset Industri HPAL (sumber: Springer/Hatch "Optimizing Process Design of Flash Vessels", Nickel Institute, 911 Metallurgist, US Patent 6,482,250)

### Apa itu Flash Letdown?
Setelah slurry keluar dari autoclave pada **250-255°C dan 45-50 atm**, slurry TIDAK BISA langsung dibuang ke atmosfer — kalau langsung, akan terjadi *explosive flashing* (ledakan uap) yang merusak pipa dan berbahaya. Jadi tekanan & panas diturunkan **bertahap** lewat rangkaian **flash vessel** (biasanya 2-3 tahap).

### Fungsi Flash Vessel (per Springer/Hatch 2023):
> Flash vessels dipakai di pressure hydrometallurgy untuk **mendisipasi energi slurry** saat transisi dari kondisi autoclave ke atmosferik, dan **memisahkan flashed steam** dari residual slurry. Disipasi energi terjadi lewat slurry pool + impingement block yang melindungi vessel.

### 3-Stage Letdown (data nyata):
- **Stage 1 (FV1)**: 45 atm → ~18 atm, 250°C → ~210°C. Flashed steam tahap 1 (tekanan tertinggi) di-recover ke preheater slurry feed.
- **Stage 2 (FV2)**: 18 atm → ~6 atm, 210°C → ~160°C. Steam tahap 2 di-recover.
- **Stage 3 (FV3)**: 6 atm → ~1 atm (atmospheric), 160°C → ~100°C. Final flash.

### Kenapa Steam Recovery Penting (Springer 2023):
> Di sirkuit HPAL, meningkatkan recovery flashed steam **meminimalkan kebutuhan injeksi boiler steam langsung ke autoclave**, mengurangi dilusi liquor tenor downstream, dan menurunkan carbon footprint.

### Sampling Setelah Letdown:
Setelah slurry mencapai atmosferik (~100°C), slurry **di-sampling** untuk analisa:
- **Ni tenor** (g/L nikel terlarut) — verifikasi recovery autoclave
- **Free acid** (g/L H2SO4 sisa) — untuk dosis neutralisasi downstream
- **Density / SG** — kontrol solid content
- **Co tenor** (kobalt terlarut)

---

## Alur Gameplay Level 8 (VR-native, SOP-accurate)

1. Player di DCS → tekan **tombol DCS 8** → "Mulai sekuens flash letdown".
2. Fade + teleport ke **platform flash vessel train** (FV1/FV2/FV3 berjajar).
3. **TAHAP 1 — Buka Interstage Letdown Valve FV1→FV2** (handwheel `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel`):
   - Player putar handwheel → choke valve membuka bertahap
   - Pressure cascade panel FV1 turun 45 → 18 atm (animasi gauge + angka)
   - **Buka Steam Valve FV1** (`FV1_SteamValve_Handwheel`) → flashed steam recovered (lampu hijau "STEAM RECOVERED")
   - Slurry pool FV1 turun, slurry mengalir ke FV2 (animasi)
4. **TAHAP 2 — Buka Interstage Letdown Valve FV2→FV3** (handwheel `FV2_To_FV3_InterstageLetdownValve_BypassHandwheel`):
   - Pressure FV2 turun 18 → 6 atm
   - **Buka Steam Valve FV2**
   - Slurry mengalir ke FV3
5. **TAHAP 3 — Final flash di FV3**:
   - **Buka Steam Valve FV3** → pressure FV3 turun 6 → 1 atm (atmospheric)
   - Slurry sekarang aman di ~100°C / 1 atm
   - Lampu "ATMOSPHERIC OK" hijau
6. **LAPOR HT pertama** (tahan T): "Flash letdown selesai, slurry atmospheric, suhu 100 derajat."
7. **TAHAP SAMPLING** — Player ke `FlashLetdown_SampleStation`:
   - Buka sample valve (handwheel kecil) → slurry mengalir ke sample cup (animasi cairan)
   - Cup terisi → tutup valve
   - **Analisa sample** muncul di panel: Ni tenor (g/L), Free acid (g/L), Density, Co tenor
8. **LAPOR HT kedua** (tahan T): "Sample diambil, Ni tenor 4.2 gram per liter, free acid 25 gram per liter, normal."
9. **Mission Complete Canvas**: STAY (lihat proses) / KEMBALI KE DCS (Level 9).

## Mekanik VR-Native (kenapa VR)

### 1. Cascade Pressure Visualization (X-Ray flash)
Player melihat **3 slurry pool simultan** dengan level berbeda + flashed steam keluar dari masing-masing stage. Di plant nyata, flash vessel adalah baja solid — operator TIDAK bisa lihat slurry pool turun. Di VR, X-Ray ghost (`FV1/FV2/FV3_XRay_SlurryPool_Ghost`) menunjukkan kaskade tekanan visual.

### 2. Sequential Valve Operation (urutan SOP)
Player HARUS buka valve dalam **urutan benar** (FV1→FV2→FV3). Kalau salah urutan (misal buka FV3 dulu), terjadi *upset* — pressure shock. Ini melatih muscle memory urutan SOP kritikal.

### 3. Steam Recovery Decision
Setiap stage, player buka steam valve untuk recovery energi. Kalau lupa, ada warning "STEAM WASTED — energy loss". Melatih kesadaran efisiensi energi (carbon footprint).

### 4. Sampling Asam Panas Aman
Sample slurry pada 100°C / pH<1 — masih berbahaya. Player buka valve, isi cup, tutup. Mensimulasikan SOP sampling tanpa kontak langsung.

### 5. Assay Reading
Player baca hasil analisa (Ni/Co tenor, free acid) — melatih interpretasi data lab untuk verifikasi performa autoclave.

## Object Names di Scene (auto-find)
- `FV1_To_FV2_InterstageLetdownValve_BypassHandwheel`, `FV2_To_FV3_InterstageLetdownValve_BypassHandwheel`
- `FV1_SteamValve_Handwheel`, `FV2_SteamValve_Handwheel`, `FV3_SteamValve_Handwheel`
- `FV1_PressureCascadePanel_Text`, `FV2_PressureCascadePanel_Text`, `FV3_PressureCascadePanel_Text`
- `FV1_XRay_SlurryPool_Ghost`, `FV2_XRay_SlurryPool_Ghost`, `FV3_XRay_SlurryPool_Ghost`
- `FlashLetdown_SampleStation_Backplate`, `SampleStation_Coil_A/B`, `SampleStation_DrainBucket`
- `SpawnPoint_Lvl8` (perlu dibuat di depan flash vessel train)

## Parameter SOP (Target)
| Stage | Pressure In | Pressure Out | Temp In | Temp Out |
|-------|-------------|--------------|---------|----------|
| FV1   | 45 atm      | 18 atm       | 250°C   | 210°C    |
| FV2   | 18 atm      | 6 atm        | 210°C   | 160°C    |
| FV3   | 6 atm       | 1 atm        | 160°C   | 100°C    |

Sample target: Ni tenor 4.0-4.5 g/L, Free acid 20-30 g/L, Density 1.3-1.4 SG, Co tenor 0.3-0.4 g/L.
