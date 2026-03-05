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

  const token = auth.getToken();
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && token) {
        auth.logout();
        snackBar.open('Oturumunuz sona erdi. Lütfen tekrar giriş yapın.', 'Kapat', { duration: 5000 });
        return throwError(() => err);
      }
      if (err.status === 401 && !token) {
        const msg = err.error?.message ?? err.error ?? 'Kullanıcı adı veya şifre hatalı.';
        snackBar.open(typeof msg === 'string' ? msg : 'Giriş başarısız.', 'Kapat', { duration: 4000 });
        return throwError(() => err);
      }
      if (err.status === 403) {
        snackBar.open('Bu işlem için yetkiniz yok.', 'Kapat', { duration: 4000 });
        router.navigate(['/']);
        return throwError(() => err);
      }
      const message = err.error?.message ?? err.error ?? (typeof err.error === 'string' ? err.error : null);
      const display = message || (err.status === 0 ? 'Sunucuya ulaşılamıyor.' : `Hata (${err.status}).`);
      snackBar.open(display, 'Kapat', { duration: 5000 });
      return throwError(() => err);
    })
  );
};
