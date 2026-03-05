# ERR_CERT_AUTHORITY_INVALID / Giriş çalışmıyor

Frontend artık **hiçbir zaman** `https://localhost:7177` adresine istek atmıyor. Tüm API istekleri **aynı origin** üzerinden gidiyor (`/api/...`), Angular proxy bunları `http://localhost:5145` adresine yönlendiriyor; böylece tarayıcıda sertifika hatası oluşmaz.

## Yapmanız gerekenler

1. **Eski frontend’i durdurun**  
   Çalışan `npm start` varsa terminalde **Ctrl+C** ile kapatın.

2. **Önbelleği temizleyin (önemli)**  
   Tarayıcı eski JavaScript’i kullanıyor olabilir:
   - **Chrome/Edge:** `Ctrl+Shift+Delete` → “Önbelleğe alınan resimler ve dosyalar” → Temizle  
   - veya **Gizli pencere (Incognito)** açıp `http://localhost:4200` adresine gidin

3. **Backend’i tek instance çalıştırın**  
   Başka bir terminalde yalnızca **bir kez**:
   ```bash
   cd src/JewelerAutomation.WebAPI
   dotnet run
   ```
   “Now listening on: http://localhost:5145” yazısını gördükten sonra devam edin.

4. **Frontend’i yeniden başlatın**
   ```bash
   cd client
   npm start
   ```

5. **Tarayıcıda sertifikasız deneyin**  
   - Adres: **http://localhost:4200** (https değil)  
   - Mümkünse **Ctrl+Shift+R** (hard refresh) veya Gizli pencere  
   - Giriş: **admin** / **Admin123!**

Network sekmesinde (F12) login isteğinin **Request URL** değeri `http://localhost:4200/api/auth/login` olmalı; `https://localhost:7177` olmamalı.
