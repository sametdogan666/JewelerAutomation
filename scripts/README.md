# Scripts

## run-local.ps1

Yerelde projeyi ayağa kaldırır: önce API’yi, sonra Angular’ı ayrı PowerShell pencerelerinde başlatır.

**Kullanım (PowerShell):**

```powershell
.\scripts\run-local.ps1
```

**Gereksinimler:**

- PostgreSQL çalışır durumda (varsayılan: `localhost:5432`, kullanıcı `postgres`, şifre `postgres`).
- .NET 8 SDK ve Node.js/npm yüklü.

Bağlantı bilgisi `src/JewelerAutomation.WebAPI/appsettings.json` içindeki `ConnectionStrings:DefaultConnection` ile değiştirilebilir.
