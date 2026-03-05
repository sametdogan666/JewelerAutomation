# Excel Dosyaları Formül ve Kolon Analizi

## 1. Alış-Satış (Aralık2024.xlsx)

### Yapı
- Her sayfa bir tarih (örn. 03.12.2024). Kolonlar A–Q.
- **SATIŞ bloğu:** A–J | **ALIŞ bloğu:** L–Q

### SATIŞ kolonları
| Kolon | Alan      | Açıklama        | Veri tipi / Formül |
|-------|-----------|-----------------|---------------------|
| A     | TARİH     | İşlem tarihi    | Sayı (Excel seri) veya metin |
| B     | MİKTAR    | Gram cinsinden  | decimal |
| C     | MİLYEM    | Saflık (916, 995 vb.) | decimal |
| D     | ADET      | Parça adedi     | int? |
| E     | BİRİM İŞÇİLİK | İşçilik birim fiyatı | decimal |
| F     | TOPLAM İŞÇİLİK | **Formül:** `-(D*E*0.01)` veya `(D*E*0.01)` | decimal (Adet × Birimİşçilik × 0.01) |
| G     | HAS-GR    | **Formül:** `(B*C*0.001)+F` → **(Miktar × Milyem / 1000) + Toplamİşçilik** | decimal |
| H     | FİYAT     | Satış fiyatı    | decimal |
| I     | AÇIKLAMA  | Metin           | string |
| J     | MİLYEM İŞÇİLİK | **Formül:** `IF(C>916, (C-916)*B*0.001, 0)` | decimal (916 üstü fazlalık gram) |

### ALIŞ kolonları
| Kolon | Alan    | Formül / Tip |
|-------|--------|---------------|
| L | Tarih   | Tarih |
| M | Ağırlık | decimal (gram) |
| N | Milyem  | decimal |
| O | Has     | **Formül:** `(M*N*0.001)` → **Ağırlık × Milyem / 1000** |
| P | Fiyat   | decimal |
| Q | AÇIKLAMA | string |

### Kritik formüller (Domain’e taşınacak)
- **HasGram (satış, işçilikli):** `HasGram = (Miktar * Milyem) / 1000m + Toplamİşçilik`, `Toplamİşçilik = ±(Adet * Birimİşçilik * 0.01)`.
- **HasGram (alış / sade):** `HasGram = Ağırlık * Milyem / 1000`.
- **Milyemİşçilik (fazlalık):** `Milyem > 916 ⇒ (Milyem - 916) * Miktar * 0.001`, else 0.

---

## 2. Kasa (Kasa.xlsx)

### Kolonlar
| Kolon | Alan     | Formül / Tip |
|-------|----------|----------------|
| A | TARİH   | Tarih |
| B | GRAM    | decimal (ham gram) |
| C | MİLYEM  | decimal (1000 = has) |
| D | HAS-GR  | **Formül:** `B*C*0.001` → **Gram × Milyem / 1000** |
| E | AÇIKLAMA | string |
| L | Ana sermaye | **Formül:** `SUM(D3:D600)` → Kümülatif HAS-GR toplamı |

### Bakiye mantığı
- Her hareket: `HasGram = Gram * Milyem / 1000` (pozitif = giriş, negatif = çıkış).
- **Devreden / Toplam stok:** `SUM(HasGram)` tüm hareketler üzerinden.

---

## 3. Cari (Cariler.xlsx)

### Yapı
- Her sayfa bir cari (müşteri/tedarikçi) adı.

### Kolonlar
| Kolon | Alan | Formül / Tip |
|-------|------|----------------|
| A | TARİH | Tarih |
| B | MİKTAR | decimal (negatif = bize verilen, pozitif = bizim verdiğimiz) |
| C | MİLYEM | decimal |
| D–E | ADET, BİRİM İŞÇİLİK | İşçilik |
| F | TOPLAM İŞÇİLİK | `-(D*E*0.01)` veya `(D*E*0.01)` |
| G | HAS-GR | `(B*C*0.001)+F` |
| H | AÇIKLAMA | string |
| I | DURUM | **Formül:** `IF(G=0,"BOŞ", IF(B>0,"VERİLDİ","ALINDI"))` → VERİLDİ / ALINDI / BOŞ |
| J | MİLYEM İŞÇİLİK | `IF(I="ALINDI", IF(C>916,(C-916)*B*0.001,0), 0)` |
| L | TOPLAM HESAP | **Formül:** `SUM(G4:G500)` → Cari bakiye (Has Gram cinsinden) |

### Bakiye
- **Bakiye (Has Gram):** Tüm hareketlerin HAS-GR toplamı. Negatif = cari bize borçlu (biz verdik), pozitif = biz borçluyuz.

---

## 4. Şahıs / Borç Sorgulama (BorçSorgulama.xlsx)

### Yapı
- Her sayfa bir şahıs (kişi adı).

### Kolonlar
| Kolon | Alan | Formül / Tip |
|-------|------|----------------|
| B | MIKTAR | decimal |
| C | MILYEM | decimal |
| D | GR-HAS | **Formül:** `(B*C*0.001)` → **Miktar × Milyem / 1000** |
| E | DURUM | BORÇ / ALACAK (metin) |
| F | AÇIKLAMA | string |
| G–H | TARİH-1, TARİH-2 | Tarih |
| L | TOPLAM HAS-GR | **Formül:** `SUM(D5:D200)` → Şahıs bakiyesi (Has Gram) |

### Bakiye
- **Toplam Has-GR:** Hareketlerin GR-HAS toplamı. Borç/Alacak yönü DURUM ve Miktar işareti ile.

---

## Veritabanı hassasiyeti
- Gram, Milyem, Has Gram, fiyat ve oranlar: **decimal(18,6)** (veya daha yüksek).
- Tarih: **DateTime** (UTC veya local tutarlı kullanım).

## Cari & Kasa entegrasyonu (özet)
- **Satış:** Stoktan (Kasa/Envanter) Has Gram düşülür; cari alacak (veya nakit) artar.
- **Alış:** Kasaya Has Gram girer; cari borç veya nakit çıkışı.
- **Şahıs:** Verilen/alinan altın hareketleri Has Gram ile; bakiye SUM(GR-HAS).

Bu dokümandaki formüller `AccountingService` ve Domain servislerinde birebir uygulanacaktır.
