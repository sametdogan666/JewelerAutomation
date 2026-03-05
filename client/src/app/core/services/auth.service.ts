import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  userName: string;
  role: string;
  expiresAt: string;
}

interface StoredAuth {
  token: string;
  userName: string;
  role: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'jeweler_auth';
  private readonly DEFAULT_EXPIRY_MS = 8 * 60 * 60 * 1000; // 8 saat

  // Yalnızca UI reaktivitesi için (header'da isim göstermek vb.)
  private userSignal = signal<{ userName: string; role: string } | null>(
    this.readValidStorage()
      ? { userName: this.readValidStorage()!.userName, role: this.readValidStorage()!.role }
      : null
  );
  currentUser = this.userSignal.asReadonly();

  constructor(private http: HttpClient, private router: Router) {}

  // -----------------------------------------------
  // Temel doğrulama: doğrudan localStorage okur
  // -----------------------------------------------

  isAuthenticated(): boolean {
    const token = this.getToken();
    // #region agent log
    console.log('[AUTH] isAuthenticated called', {hasToken:!!token, tokenLength:token?.length??0, caller:new Error().stack?.split('\n')[2]?.trim()});
    // #endregion
    return token !== null;
  }

  getToken(): string | null {
    const stored = this.readValidStorage();
    // #region agent log
    console.log('[AUTH] getToken called', {hasStored:!!stored, tokenLength:stored?.token.length??0});
    // #endregion
    if (!stored) return null;
    return stored.token;
  }

  // -----------------------------------------------
  // Auth işlemleri
  // -----------------------------------------------

  login(body: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', body).pipe(
      tap((res) => {
        // #region agent log
        console.log('[AUTH] Login response received', {hasToken:!!res.token, tokenLength:res.token?.length, expiresAtRaw:res.expiresAt, userName:res.userName, role:res.role});
        // #endregion
        const expiresAt = this.normalizeExpiresAt(res.expiresAt);
        const auth: StoredAuth = {
          token: res.token,
          userName: res.userName,
          role: res.role,
          expiresAt,
        };
        localStorage.setItem(this.storageKey, JSON.stringify(auth));
        // #region agent log
        console.log('[AUTH] Token written to localStorage', {key:this.storageKey, expiresAtNormalized:expiresAt, writtenAt:new Date().toISOString()});
        // #endregion
        this.userSignal.set({ userName: res.userName, role: res.role });
      })
    );
  }

  logout(): void {
    // #region agent log
    console.log('[AUTH] LOGOUT CALLED', {caller:new Error().stack?.split('\n')[2]?.trim(), timestamp:new Date().toISOString()});
    // #endregion
    localStorage.removeItem(this.storageKey);
    this.userSignal.set(null);
    this.router.navigate(['/login']);
  }

  // -----------------------------------------------
  // Yardımcı metodlar
  // -----------------------------------------------

  private readValidStorage(): StoredAuth | null {
    try {
      const raw = localStorage.getItem(this.storageKey);
      // #region agent log
      console.log('[AUTH] readValidStorage: reading localStorage', {hasRaw:!!raw, rawLength:raw?.length??0});
      // #endregion
      if (!raw) return null;
      const data = JSON.parse(raw) as StoredAuth;
      if (!data.token || !data.expiresAt) {
        // #region agent log
        console.log('[AUTH] readValidStorage: missing fields', {hasToken:!!data.token, hasExpiresAt:!!data.expiresAt});
        // #endregion
        return null;
      }
      const valid = this.isExpiryValid(data.expiresAt);
      // #region agent log
      console.log('[AUTH] readValidStorage: after expiry check', {expiresAt:data.expiresAt, valid, willRemove:!valid});
      // #endregion
      if (!valid) {
        console.log('[AUTH] TOKEN REMOVED - expired');
        localStorage.removeItem(this.storageKey);
        return null;
      }
      return data;
    } catch (e) {
      // #region agent log
      console.error('[AUTH] readValidStorage: parse error', e);
      // #endregion
      return null;
    }
  }

  private isExpiryValid(expiresAt: string): boolean {
    const expMs = new Date(expiresAt).getTime();
    const now = Date.now();
    const valid = !Number.isNaN(expMs) && now < expMs;
    // #region agent log
    console.log('[AUTH] isExpiryValid:', {expiresAt, expMs, now, diffSeconds:(expMs-now)/1000, valid});
    // #endregion
    if (Number.isNaN(expMs)) return false;
    // Süresi henüz dolmamışsa geçerli
    return Date.now() < expMs;
  }

  private normalizeExpiresAt(expiresAt: string | undefined): string {
    if (expiresAt) {
      const ms = new Date(expiresAt).getTime();
      if (!Number.isNaN(ms) && ms > Date.now()) return expiresAt;
    }
    return new Date(Date.now() + this.DEFAULT_EXPIRY_MS).toISOString();
  }
}
