# PANEL 2 — Panel Misi / Quest (kanan layar)

> Panel HUD di **kanan-atas layar** berisi: header level, misi aktif, hint "LAPORAN HT",
> dan checklist operasional (atau checklist APD di Level 1).

- **Script:** `Assets/Scripts/UI/PlayerHUD.cs`
- **Objek root:** `Panel_Quest` (parent dari `txtQuestLabel`)
- **Tipe canvas:** Screen-Space (overlay/kamera) — menempel kanan layar
- **Override layout:** `CacheAndFixLayout()` di `Start()` memaksa SEMUA posisi/ukuran/warna

## Status sekarang
- Tata letak v3 (stack vertikal) sudah rapi tapi visualnya polos: kotak biru-gelap bertumpuk,
  tanpa ikon, progress ring, atau pembeda section yang kuat.
- Header cuma teks tengah; tidak ada nomor level besar / ikon level.
- Checklist `[OK]`/`[ ]` berupa teks ASCII, bukan checkbox/ikon.

## ⚙️ Cara desain manual tanpa ditimpa kode
`CacheAndFixLayout()` menimpa geometri tiap `Start()`. Pilih:
- **Opsi A (disarankan):** minta toggle `_manualLayout` (lihat `00_OVERVIEW.md`) → blok geometri
  di-skip, binding teks/warna tetap. Desain bebas.
- **Opsi B:** ikuti angka di bawah persis (kode memaksa angka yang sama → tidak bentrok),
  fokus ganti warna/sprite/font/ikon.

Field serialized yang HARUS tetap ter-wire (apa pun desainmu):
`txtLevelLabel`, `txtQuestLabel`, `bgHeader`, `panelOperasional`, `txtParameterInfo`,
`panelWalkieTalkieHint`, `txtHintKataKunci`, 8 task APD (`taskHelm`…`taskWalkieTalkie`),
`panelNotif`, `txtNotif`, `bgNotif`.

---

## 📐 Layout sekarang (titik awal presisi)

**Panel_Quest** (root): anchor & pivot **kanan-atas (1, 1)**, posisi `(−24, −24)`,
size **500 × 800**, warna `BG_PANEL` `#0D121C` α0.95.

Token: `PAD=14`, `GAP=10`, `HEADER_H=56`, `MISI_H=128`, `HT_H=150`.
Stack dari atas ke bawah (Y dihitung dari atas panel, makin ke bawah makin negatif):

| Urut | Section | Objek | Anchor | Y (dari atas) | Size (W,H) | Warna band |
|---|---|---|---|---|---|---|
| 1 | Header band | `BG_Header` | top stretch (0,1)→(1,1) | −14 | (0 = full, 56) | `HEADER_MISI` `#0F5785` |
| 2 | MISI band | `Panel_Quest/...Quest` (`txtQuestLabel`) | top stretch | −80 | (−28 = full−2·PAD, 128) | `BAND` `#172133` |
| 3 | LAPORAN HT band | `panelWalkieTalkieHint` | top stretch | −218 | (−28, 150) | `BAND_DARK` `#080D14` |
| 4 | Checklist (Ops/APD) | `panelOperasional` / parent APD | top stretch | −378 | (−28, sisa ≈ 408) | `BAND` `#172133` |

> Rumus Y: `yHeader=−14`, `yMisi=−14−56−10=−80`, `yHt=−80−128−10=−218`,
> `yChecklist=−218−150−10=−378`. `checklistH = 800 + (−378) − 14 = 408`.

### Teks per section

| Teks | Objek | Font | Warna | Catatan |
|---|---|---|---|---|
| Label level | `txtLevelLabel` | 23 Bold | putih (merah saat Emergency) | Center, truncate |
| Misi aktif | `txtQuestLabel` | 17 (auto 12–19) | putih | TopLeft, wrap |
| Hint laporan | `txtHintKataKunci` | 16 (auto 12–17) | `WARN_YELLOW` `#FFE057` | margin (14,34,14,10) |
| Label "LAPORAN HT" | `Lbl_HT` (auto-dibuat) | 15 Bold | `ACCENT_BLUE` `#73D9FF` | pojok kiri-atas band HT |
| Checklist | `txtParameterInfo` | 17 (auto 12–18) | putih | lineSpacing 14, offset (16,14)–(−16,−44) |

### Warna state checklist
- Task selesai: prefix `[OK]` + warna `DONE_GREEN` `#33F273`.
- Task pending: prefix `[ ]` + putih.
- Header level: biru `_cBlue #4DD9FF` normal, **merah `#FF3333`** saat `Level14_Emergency`.

---

## 🎯 Hierarki yang disarankan (redesain)

```
Panel_Quest                 (Image BG_PANEL, outline tipis HEADER_MISI)
├── BG_Header               (Image HEADER_MISI)          → bgHeader
│   ├── Badge_Level         (Image kotak + nomor level besar, mis. "9")
│   └── txtLevelLabel       (TMP 23 Bold putih, Center)  → txtLevelLabel
├── Band_Misi               (Image BAND)
│   ├── Icon_Target         (Image ikon 🎯/panah)
│   ├── Lbl_Misi            (TMP 13 Bold ACCENT_BLUE "MISI AKTIF")
│   └── txtQuestLabel       (TMP 17 putih, wrap)         → txtQuestLabel
├── Band_HT                 (Image BAND_DARK)            → panelWalkieTalkieHint
│   ├── Lbl_HT              (TMP 15 Bold ACCENT_BLUE "LAPORAN HT")  (kode auto-buat kalau belum ada)
│   ├── Icon_Radio          (Image ikon HT/radio)
│   └── txtHintKataKunci    (TMP 16 WARN_YELLOW)         → txtHintKataKunci
├── Band_Checklist          (Image BAND)
│   ├── Lbl_Checklist       (TMP 13 Bold ACCENT_BLUE "CHECKLIST OPERASIONAL")
│   └── txtParameterInfo    (TMP 17 putih, lineSpacing 14) → txtParameterInfo
└── Container_APD           (hanya Level 1 — checklist APD 8 item)
    ├── taskHelm ... taskWalkieTalkie  (8× TMP)          → 8 field task
```

> Catatan: `panelOperasional` dan `Container_APD` (parent dari `taskHelm`) menempati **area
> checklist yang sama** dan ditampilkan bergantian (APD di Level 1, Ops di level lain). Beri
> ukuran/anchor sama supaya transisinya mulus.

> `Lbl_*` adalah label statis baru = aman dari kode. Hanya field di kolom "→" yang wajib di-wire.

---

## 📝 Konten dinamis (diisi kode)
- `txtLevelLabel`: nama level ("LEVEL 9 - CCD", dst) — dari `GetLabelLevel`.
- `txtQuestLabel`: misi aktif per fase (LihatDCS / MulaiMesin / LaporHT / GunakanWT) — bisa
  diedit di Inspector `PlayerHUD` (list `_teksPerLevel`, `_teksLevel3`, `_teksUmum`).
- `txtHintKataKunci`: kalimat laporan HT yang harus diucapkan.
- `txtParameterInfo`: checklist `[OK]/[ ]` per langkah level (driven `GameLevelManager`).

### Tips look game/quest
- Ganti `[OK]/[ ]` jadi **ikon checkbox** secara visual: kamu boleh menaruh ikon di samping,
  tapi teksnya tetap punya kode. Alternatif: minta aku ganti string `Check()` ke karakter
  TMP sprite (mis. `<sprite=0>`/`<sprite=1>`) supaya muncul ikon centang asli.
- **Progress ring/bar** di header (mis. "3/6 langkah") — butuh tambahan kode kecil, bisa aku bantu.
- Bedakan band aktif (glow/outline) vs non-aktif (redup) untuk fokus mata.
- Konsisten lebar `−28` (full − 2·PAD) supaya rapi dengan padding 14px.
