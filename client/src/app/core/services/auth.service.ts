import { Injectable, signal, computed } from '@angular/core';
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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'jeweler_auth';
  private tokenSignal = signal<string | null>(this.getStoredToken());
  private userSignal = signal<{ userName: string; role: string } | null>(this.getStoredUser());

  token = this.tokenSignal.asReadonly();
  currentUser = this.userSignal.asReadonly();
  isAuthenticated = computed(() => !!this.tokenSignal());

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  login(body: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', body).pipe(
      tap((res) => {
        this.tokenSignal.set(res.token);
        this.userSignal.set({ userName: res.userName, role: res.role });
        localStorage.setItem(this.storageKey, JSON.stringify({
          token: res.token,
          userName: res.userName,
          role: res.role,
          expiresAt: res.expiresAt,
        }));
      })
    );
  }

  logout(): void {
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    localStorage.removeItem(this.storageKey);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    this.syncTokenFromStorage();
    return this.tokenSignal();
  }

  /** Re-read token from storage and clear if expired. Call before isAuthenticated() in guards. */
  syncTokenFromStorage(): void {
    const raw = localStorage.getItem(this.storageKey);
    if (!raw) {
      if (this.tokenSignal()) {
        this.tokenSignal.set(null);
        this.userSignal.set(null);
      }
      return;
    }
    try {
      const data = JSON.parse(raw) as { token: string; expiresAt: string; userName: string; role: string };
      if (new Date(data.expiresAt) <= new Date()) {
        localStorage.removeItem(this.storageKey);
        this.tokenSignal.set(null);
        this.userSignal.set(null);
        return;
      }
      this.tokenSignal.set(data.token);
      this.userSignal.set({ userName: data.userName, role: data.role });
    } catch {
      localStorage.removeItem(this.storageKey);
      this.tokenSignal.set(null);
      this.userSignal.set(null);
    }
  }

  private getStoredToken(): string | null {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return null;
      const data = JSON.parse(raw) as { token: string; expiresAt: string };
      if (new Date(data.expiresAt) <= new Date()) {
        localStorage.removeItem(this.storageKey);
        return null;
      }
      return data.token;
    } catch {
      return null;
    }
  }

  private getStoredUser(): { userName: string; role: string } | null {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return null;
      const data = JSON.parse(raw) as { userName: string; role: string };
      return { userName: data.userName, role: data.role };
    } catch {
      return null;
    }
  }
}
