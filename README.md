# Jeweler Automation

Kuyumculuk işletmeleri için ASP.NET Core API + **Angular 17** frontend. PostgreSQL veritabanı, JWT kimlik doğrulama ve Excel formüllerine uyumlu hesaplama servisleri.

## Mimari

| Katman | Açıklama |
|--------|----------|
| **Backend** | .NET 8, N-Tier (Core, Application, Infrastructure, WebAPI). JWT + CORS. |
| **Frontend** | Angular 17, standalone components, Angular Material, responsive layout. |
| **Veritabanı** | **PostgreSQL** (varsayılan). EF Core Code-First, decimal(18,6) hassasiyet. |

## Proje yapısı

```
JewelerAutomation/
├── client/                 # Angular 17 SPA
│   ├── src/app/
│   │   ├── core/          # Auth, guards, interceptors, API servisleri
│   │   ├── features/      # Login, Dashboard, Cariler, Kasa
│   │   └── layout/        # Sidebar + toolbar (responsive)
│   └── ...
├── src/
│   ├── JewelerAutomation.Core/
│   ├── JewelerAutomation.Application/
│   ├── JewelerAutomation.Infrastructure/
│   └── JewelerAutomation.WebAPI/
└── docs/EXCEL_FORMULA_ANALYSIS.md
```

## Veritabanı (PostgreSQL)

Varsayılan bağlantı (`appsettings.json`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=JewelerAutomation;Username=postgres;Password=postgres;Include Error Detail=true"
},
"UsePostgres": true
```

PostgreSQL 15+ (veya 18) ile uyumludur. Kullanıcı/şifre ve host bilgisini ortamınıza göre güncelleyin.

## İlk çalıştırma (yerel)

### Gereksinimler

- **PostgreSQL** kurulu ve çalışır durumda (varsayılan: `localhost:5432`, kullanıcı `postgres`, şifre `postgres`).
- **.NET 8 SDK** ve **Node.js (npm)** yüklü.

Bağlantıyı kendi ortamınıza göre `src/JewelerAutomation.WebAPI/appsettings.json` içindeki `ConnectionStrings:DefaultConnection` ile değiştirebilirsiniz.

### Tek komutla (PowerShell)

İki ayrı pencerede backend ve frontend’i başlatır:

```powershell
.\scripts\run-local.ps1
```

### Adım adım

**1. Backend (API)**  
İlk çalıştırmada veritabanı migration’ları ve **seed veriler** (admin + örnek cariler + kasa hareketleri) otomatik uygulanır.

```bash
cd src/JewelerAutomation.WebAPI
dotnet run
```

> **Build hatası:** `dotnet build` sırasında "file is locked" (MSB3027/MSB3021) görürseniz, çalışan API'yi kapatıp tekrar deneyin. Veya `.\scripts\build.ps1` çalıştırın; script WebAPI process'ini durdurup build alır.

API adresi: **https://localhost:7177**

**2. Frontend (Angular)**

```bash
cd client
npm install
npm start
```

Tarayıcıda **http://localhost:4200** açılır.

**3. Giriş**

- Kullanıcı adı: **admin**  
- Şifre: **122333**

Girişten sonra Panel’de özet kartlar, **Cariler** menüsünde 6 örnek cari, **Kasa** menüsünde bakiye ve örnek hareketler listelenir.

## Frontend özellikleri

- **Responsive**: Mobilde menü overlay, masaüstünde sabit sidebar.
- **Tema**: Angular Material, indigo/amber palet, DM Sans font.
- **Sayfalar**: Giriş, Panel (özet kartlar), Cariler (liste + ekleme/düzenleme), Kasa (bakiye + hareket listesi ve yeni hareket formu).
- **Auth**: JWT token, HTTP interceptor, route guard’lar.

API URL’i `client/src/environments/environment.ts` içinde (`apiUrl: 'https://localhost:7177'`). Production için `environment.prod.ts` içindeki `apiUrl` değerini ayarlayın.

## Excel formül özeti

| Hesaplama | Formül |
|-----------|--------|
| Has Gram (sade) | `Miktar * Milyem / 1000` |
| Has Gram (işçilikli) | `(Miktar * Milyem / 1000) + Toplamİşçilik` |
| Toplam İşçilik | `±(Adet * Birimİşçilik * 0.01)` |
| Milyem İşçilik (916 üstü) | `(Milyem - 916) * Miktar * 0.001` |
| Kasa / Cari bakiye | `SUM(HasGram)` |

Detay: [docs/EXCEL_FORMULA_ANALYSIS.md](docs/EXCEL_FORMULA_ANALYSIS.md)

## API özeti

| Endpoint | Açıklama |
|----------|----------|
| `POST /api/auth/login` | JWT token (anonim) |
| `GET/POST/PUT/DELETE /api/customers` | Cari CRUD (JWT) |
| `GET /api/customers/{id}/account/balance` | Cari bakiye (altın + nakit) |
| `GET /api/customers/{id}/account/statement` | Cari ekstre |
| `POST /api/customers/{id}/account/transactions` | Cari hareket ekleme |
| `GET /api/safe/balance`, `GET/POST /api/safe/movements` | Kasa (JWT) |
| `GET/POST /api/transactions` | Alış-Satış (JWT) |

Tüm API’ler (login hariç) **JWT** ile korunur. Token süresi dolunca 401 döner; frontend kullanıcıyı tekrar `/login` sayfasına yönlendirir.

---

## GitHub’a ekleme

Projeyi GitHub’a yüklemek için:

```bash
cd /path/to/JewelerAutomation
git init
git add .
git commit -m "Initial commit: DOĞAN KUYUMCULUK - Backend + Angular frontend"
git branch -M main
git remote add origin https://github.com/KULLANICI_ADINIZ/JewelerAutomation.git
git push -u origin main
```

- **Kullanıcı adı / token:** GitHub’da repo oluşturduktan sonra `KULLANICI_ADINIZ` kısmını kendi kullanıcı adınızla değiştirin. SSH kullanacaksanız: `git remote add origin git@github.com:KULLANICI_ADINIZ/JewelerAutomation.git`
- **Hassas bilgiler:** `appsettings.json` içindeki veritabanı şifresi ve JWT anahtarını production’da ortam değişkeni veya User Secrets ile yönetin; gerçek şifreleri doğrudan repoya koymayın.
