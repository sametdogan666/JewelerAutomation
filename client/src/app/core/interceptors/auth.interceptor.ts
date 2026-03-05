import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { MatSnackBar } from '@angular/material/snack-bar';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);

  // #region agent log
  console.log('[INTERCEPTOR] Request intercepted', {url: req.url, urlWithParams: req.urlWithParams, method: req.method});
  // #endregion

  const url = req.url || req.urlWithParams || '';
  const isLoginRequest = url.includes('/auth/login');

  // #region agent log
  console.log('[INTERCEPTOR] isLoginRequest check', {url, isLoginRequest});
  // #endregion

  // Login isteğine token ekleme; diğerlerine ekle
  if (!isLoginRequest) {
    const token = auth.getToken(); // doğrudan localStorage okur
    // #region agent log
    console.log('[INTERCEPTOR] Adding token to request', {url:url, hasToken:!!token, tokenLength:token?.length??0});
    // #endregion
    if (token) {
      req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      // #region agent log
      console.log('[INTERCEPTOR] Token added, Authorization header set');
      // #endregion
    }
  }

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        if (isLoginRequest) {
          // Login hatası: form'da gösterilir, interceptor snackbar göstermesin
        } else {
          // Korunan endpoint 401: oturumu kapat
          auth.logout();
          snackBar.open('Oturumunuz sona erdi. Lütfen tekrar giriş yapın.', 'Kapat', { duration: 5000 });
        }
        return throwError(() => err);
      }
      if (err.status === 403) {
        snackBar.open('Bu işlem için yetkiniz yok.', 'Kapat', { duration: 4000 });
        router.navigate(['/']);
        return throwError(() => err);
      }
      if (err.status === 0) {
        snackBar.open('Sunucuya ulaşılamıyor.', 'Kapat', { duration: 5000 });
        return throwError(() => err);
      }
      const message =
        typeof err.error === 'string'
          ? err.error
          : err.error?.message ?? err.error?.title ?? `Hata (${err.status})`;
      snackBar.open(message, 'Kapat', { duration: 5000 });
      return throwError(() => err);
    })
  );
};
