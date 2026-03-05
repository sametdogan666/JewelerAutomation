import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // #region agent log
  console.log('[GUARD] authGuard invoked', {url:router.url, timestamp:new Date().toISOString()});
  // #endregion
  // Doğrudan localStorage okur; signal/timing bağımlılığı yok
  const isAuth = auth.isAuthenticated();
  // #region agent log
  console.log('[GUARD] authGuard result', {isAuthenticated:isAuth, willRedirectToLogin:!isAuth, url:router.url});
  // #endregion
  if (isAuth) return true;
  router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
  return false;
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // Giriş yapmış kullanıcı login sayfasına giremez
  if (!auth.isAuthenticated()) return true;
  router.navigate(['/']);
  return false;
};
