# ⚙️ Breakdown Sistem & Mekanik Teknis OLIVIA VR v3.0
## Panduan Developer — Semua Mekanik Dipetakan ke Industri HPAL Nyata

---

## 1. Arsitektur Level System

```
GameLevelManager (Pusat Kontrol)
    │
    ├── Level 0  Tutorial
    ├── Level 1  APD Safety
    ├── Level 2  DCS Voice Prep
    ├── Level 3  Field: Ore → Slurry
    ├── Level 4  DCS: Slurry Pump (Sync Speed)
    ├── Level 5  Field: Steam Valve + Pre-heater
    ├── Level 6  DCS: Acid Injection
    ├── Level 7  Field: X-Ray Autoclave
    ├── Level 8  DCS: Monitoring Ketat
    ├── Level 9  Field: Letdown + Flash Vessel
    ├── Level 10 DCS: CCD Activation
    ├── Level 11 Field: MHP Sampling
    ├── Level 12 DCS: Tailing Discharge
    ├── Level 13 Field: Tailing Waste Management (Immersive Learning)
    └── Level 14 DARURAT: K3 Kebocoran Asam/Steam (NO EXPLOSION)
```

**Script Utama:**
- `GameLevelManager.cs` — state machine level, unlock, skor
- `DCSMonitorUI.cs` — 14 Tombol Sinkronisasi Level & Kecepatan Flow
- `PhaseManager.cs` — sub-state dalam setiap level (APD, operasional, dll)
- `VoiceCommandSystem.cs` — pendeteksi kata kunci pemain (wajib per level) + trigger balasan MP3 NPC.
- `XRayViewController.cs` — toggle transparan pada mesin

---

## 2. Mekanisme DCS 14-Tombol & Flow Sync

**UI Hologram Tombol DCS:**
Panel utama DCS memiliki **14 tombol khusus**, merepresentasikan 14 titik kontrol. Di setiap level yang membutuhkan aksi DCS, tombol yang relevan akan **berkedip** dan memiliki **outline tepi tebal bercahaya (glowing outline) / hologram panah** ke arah tombol tersebut. Pemain tahu persis tombol mana yang harus ditekan.

**Sinkronisasi Kecepatan (Flow Sync):**
Nilai parameter `Flow Rate (m³/h)` di layar DCS mengontrol langsung parameter animasi di lapangan:
- Jika Flow Rate di DCS = `12 m³/h`, kecepatan shader aliran cairan dan RPM rotasi partikel slurry di lapangan berada pada `Speed = 1.0`.
- Jika Flow Rate turun = `5 m³/h`, parameter `Speed` di Material Shader cairan turun menjadi `0.41` (animasi berjalan sangat lambat). Hal ini menjadi parameter penilaian yang presisi.

---

## 3. Sistem APD — Level 1 (7 Item Wajib)

| No | Item APD | Fungsi Industri Nyata | Socket Target |
|----|----------|----------------------|---------------|
| 1 | Helm K3 | Lindungi kepala dari benda jatuh | Head socket |
| 2 | Rompi Safety | Visibilitas + pelindung dada | Torso socket |
| 3 | Kacamata Pelindung | Lindungi dari percikan H₂SO₄ | Face socket |
| 4 | Sepatu Safety | Lindungi kaki dari asam & benda berat | Feet socket |
| 5 | Sarung Tangan Kimia | Kontak pipa & peralatan berasam | Hands socket |
| 6 | **Masker / Respirator** | Wajib area H₂SO₄ & uap panas | Face socket (lapis) |
| 7 | **Walkie Talkie / HT** | Komunikasi DCS ↔ Lapangan | Hip socket (pinggang) |

---

## 4. Sistem Walkie Talkie & Voice Command Wajib

**Teknologi:** `UnityEngine.Windows.Speech.KeywordRecognizer` (offline, gratis, akurat) dikombinasikan dengan pemutar `AudioSource` MP3/WAV.

**Alur Penggunaan:**
1. Pemain **grab Walkie Talkie** dari pinggang
2. Tekan **tombol PTT** (XR Button Interactable)
3. Pemain **bicara** → kata kunci terdeteksi
4. Jika cocok → event dikirim ke `GameLevelManager`
5. **Wajib: Audio Balasan Manusia (NPC) diputar** (e.g. *"Copy DCS, melaksanakan."*) 

**Kamus Kata Kunci (Update 14 Level):**

| Level | Kata Kunci Pemain | Arah | Audio Balasan NPC (Contoh) |
|-------|-------------------|------|----------------------------|
| 1 | "APD lengkap" | Field → DCS | "Copy, pintu Safety Gate terbuka." |
| 2 | "siapkan area", "cek crusher" | DCS → Field | "Siap, menuju area Crusher." |
| 3 | "ore masuk", "cairan 25%" | Field → DCS | "Copy, standby aktivasi Slurry Pump." |
| 4 | "slurry pump aktif", "450 kubik" | DCS → Field | "Copy, memantau aliran ke Pre-heater." |
| 5 | "katup steam terbuka" | Field → DCS | "Copy, bersiap untuk injeksi asam." |
| 6 | "acid aktif", "rasio 350 kilo" | DCS → Field | "Copy, aman masuk Autoclave." |
| 7 | "suhu 250", "tekanan 50 atm" | Field → DCS | "Copy, lanjut monitoring ketat." |
| 8 | "parameter stabil" | DCS → Field | "Copy, proses optimal." |
| 9 | "flash vessel normal" | Field → DCS | "Copy, siap ke CCD." |
| 10| "CCD aktif" | DCS → Field | "Copy, menuju area presipitasi." |
| 11| "MHP terbentuk" | Field → DCS | "Copy, produksi utama selesai." |
| 12| "limbah dialirkan" | DCS → Field | "Copy, siap melakukan netralisasi." |
| 13| "tailing aman", "pH 8.5" | Field → DCS | "Copy, lingkungan aman." |
| 14| "emergency", "evakuasi" | DCS → Sirine| "Copy, kami evakuasi sekarang!" |

---

## 5. Sistem X-Ray / Invisible View

**Opsi Implementasi:** Stencil Buffer atau Material Swap Shader. Pemain dapat melihat proses di dalam mesin seperti partikel dihancurkan, campuran cairan (slurry), proses pemanasan, reaksi kimia perubahan warna, hingga proses pres press pemisahan air dan lumpur limbah.

---

## 6. Detail Mekanisme Mesin & Target SOP Pabrik

- **Slurry Pump:** Input menggunakan **tombol [+] atau [-]** di monitor mini DCS. Pemain harus menekan pelan hingga target **Flow Rate: 450 m³/h**. Laju aliran ini mengontrol kecepatan shader partikel slurry di pipa secara real-time.
- **Pre-Heater:** Memutar Rotary Valve fisik. Interaksi memutar roda mengubah nilai **Suhu** secara linear menuju target **180°C - 200°C**.
- **Autoclave (Reaktor HPAL):** Menggunakan agitator. Parameter target yang WAJIB dipenuhi: **Tekanan Atmosfer: 45 - 50 atm**, **Suhu: 250°C - 255°C**, dan **RPM Agitator: 60 RPM**.
- **Acid Injection:** Pemain harus memasukkan rasio asam yang tepat, yaitu **350 kg/ton bijih** menggunakan **tombol [+] atau [-]** di monitor mini. Target akhir adalah menurunkan **pH menjadi 1.0**.
- **Filter Press (Level 13):** Saat pemain menekan start, plat filter merapat. Targetnya adalah menekan sisa air hingga kelembaban kue tailing (tailing cake moisture) turun di bawah **25%**.
- **Limestone / Kapur (Level 13):** Grab karung, tuangkan ke tangki asam. Setiap taburan menaikkan pH. Target akhir adalah **pH 8.0 - 9.0** sebelum dibuang ke tailing.

---

## 7. Sistem ESD — HANYA DI DCS! (Level 14)

**Penting:** Tidak ada ledakan. Fokus ke prosedur K3 menangani kegagalan sistem. 

**Mekanisme Emergency Level 14:**
1. Secara tiba-tiba, alarm gas detektor menyala.
2. Terdengar suara mendesis keras dari lantai pabrik (Kebocoran H2SO4 atau Steam). Efek partikel asap putih/kuning menyebar.
3. Pemain DCS harus melaporkan *"Emergency! Evakuasi!"* via Walkie Talkie.
4. Pemain DCS menekan tombol **ESD (merah)**.
5. Tombol ESD akan menutup paksa semua `Valve Input Asam` dan `Valve Steam` (Status UI Valve DCS berubah menjadi TUTUP semua).
6. Proses di pabrik terhenti aman. Asap berhenti menyembur. Skenario Lulus.

---

## 8. Sistem Skor Per Level

```
Skor Level = (Kecepatan × 0.25) + (Kesesuaian Flow/Aksi × 0.25) + (Laporan Walkie Talkie × 0.25) + (Urutan SOP K3 × 0.25)

Nilai Akhir = Rata-rata semua 14 level
Syarat Lulus: ≥ 70%
Output: Sertifikat K3 Virtual OLIVIA (ditampilkan di layar akhir)
```