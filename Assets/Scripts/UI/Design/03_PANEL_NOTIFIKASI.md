# PANEL 3 — Banner Notifikasi (tengah-atas, biru)

> Banner yang muncul sebentar di **tengah-atas layar** untuk info/sukses/sedang-bicara.
> Inilah "notifikasi yang tengah warna biru" itu.

- **Script:** `Assets/Scripts/UI/PlayerHUD.cs`
- **Objek:** `panelNotif` (background `bgNotif`, teks `txtNotif`)
- **Override layout:** `PositionNotifTop()` di `Start()` memaksa posisi/ukuran/font
- **Warna state:** `ShowNotif()` mengganti warna `bgNotif` per jenis pesan

## Status sekarang
- Cuma kotak warna solid + teks tengah. Tidak ada ikon, border, atau animasi masuk/keluar
  (sekarang hanya `SetActive(true/false)` + auto-hide setelah 4.5 detik).
- Muncul/hilang mendadak (tanpa fade/slide).

## ⚙️ Cara desain manual tanpa ditimpa kode
`PositionNotifTop()` memaksa: anchor, pivot, size, posisi, dan setting font `txtNotif`.
`ShowNotif()` selalu mengeset `bgNotif.color` sesuai state. Maka:
- **Layout & font** banner → butuh toggle `_manualLayout` (Opsi A di `00_OVERVIEW.md`) kalau mau beda.
- **Warna** background → tetap dikuasai `ShowNotif()` (by design: warna = makna state). Kalau mau
  warna custom per state, aku bisa pindahkan ke field serialized `colorInfo/colorSukses/colorPtt`.
- Field wajib ter-wire: `panelNotif`, `txtNotif`, `bgNotif`.

---

## 📐 Layout sekarang (titik awal presisi)

**panelNotif**: anchor & pivot **tengah-atas (0.5, 1)**, posisi `(0, −26)`, size **900 × 84**.
Selalu `SetAsLastSibling()` → render paling depan (di atas semua panel + overlay transisi).

| Elemen | Objek | Anchor | Pivot | Pos (X,Y) | Size (W,H) |
|---|---|---|---|---|---|
| Banner bg | `panelNotif` (Image) | (0.5, 1) | (0.5, 1) | (0, −26) | (900, 84) |
| Teks | `txtNotif` (TMP) | stretch penuh | — | margin (20,6,20,6) | — |

**txtNotif:** font 22 (auto-size 14–24), **Bold**, **Center**, wrap normal, truncate.

### Warna background per-state (dari `ShowNotif`)

| State | Kapan | Warna | HEX | Alpha |
|---|---|---|---|---|
| **Info (biru)** | default, "Level X dimulai", info umum | 0.06, 0.18, 0.42 | `#0F2E6B` | 0.95 |
| **Sukses (hijau)** | `sukses=true` ("Level selesai", "lengkap") | 0.08, 0.45, 0.12 | `#14731F` | 0.95 |
| **Bicara/PTT (merah)** | saat tahan tombol T (sedang lapor HT) | 0.50, 0.10, 0.05 | `#801A0D` | 0.95 |

- Auto-hide: **4.5 detik** (default) setelah muncul; saat PTT, hilang **2 detik** setelah lepas T.
- Teks PTT: `"BERBICARA... (lepas tombol setelah laporan selesai)"`.

---

## 🎯 Hierarki yang disarankan (redesain)

```
panelNotif                  (Image bg per-state)         → bgNotif (Image)
├── Frame                   (Image border 9-slice / outline cyan tipis)
├── Stripe_Left             (Image strip aksen kiri 6px — warna ikut state)
├── Icon_State              (Image: ℹ biru / ✓ hijau / 🎙 merah)
├── txtNotif                (TMP 22 Bold Center)         → txtNotif
└── (opsional) Progress_Underline (Image garis menipis = sisa durasi)
```

> Ikon & frame = objek baru, aman dari kode. Yang wajib: `panelNotif` (punya `bgNotif` di-wire)
> dan `txtNotif`. Karena `txtNotif` di-stretch penuh, taruh ikon dengan **padding kiri** atau
> kecilkan margin teks supaya tidak menabrak ikon (atur via `txtNotif.margin` kalau pakai Opsi A).

---

## 📝 Konten dinamis (diisi kode)
Pesan datang dari banyak event: mulai/selesai level, APD terpasang/dilepas, flow tercapai,
prompt lapor HT, dll. Contoh: `"Slurry pump menyala. Atur flow rate ke 450 m³/h."`,
`"Semua APD lengkap! Lapor lewat WT untuk lanjut ke Level 2."`.

Saat desain manual, isi teks placeholder bebas; akan ditimpa pesan asli saat Play.

### Tips polish (butuh sedikit kode — bisa aku bantu)
- **Animasi masuk/keluar:** slide dari atas + fade (mengganti `SetActive` mendadak). Halus & mahal-murah.
- **Progress underline:** garis yang menyusut selama 4.5s = feedback sisa waktu.
- **Antrian notif:** kalau 2 pesan beruntun, sekarang yang baru menimpa yang lama. Bisa dibuat queue.
- Pertahankan **kontras tinggi** (teks putih di bg gelap-jenuh) supaya kebaca di VR saat kepala bergerak.
- Ukuran 900px lebar itu besar untuk VR; pertimbangkan 700–760 agar tidak terlalu lebar di FOV.

---

## ✅ Ringkas — yang wajib kamu hormati saat desain manual ketiga panel
1. **Jangan rename** field yang di-wire kalau tidak update Inspector (binding putus).
2. **DCS:** assign `_root` → kode berhenti membangun otomatis.
3. **Misi & Notif:** layout dipaksa `CacheAndFixLayout`/`PositionNotifTop`. Untuk desain bebas,
   pakai toggle `_manualLayout` (minta aku tambahkan).
4. **Warna notif** = makna state (biru/hijau/merah) dikontrol kode; minta aku externalize kalau
   mau atur dari Inspector.
5. Teks semua panel **dinamis** dari kode/`GameLevelManager` — desain untuk konten yang berubah
   panjang (pakai auto-size + wrap, sudah aktif).
