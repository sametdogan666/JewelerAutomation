import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

// Her zaman aynı origin: /api (ng serve proxy → http://localhost:5145). 7177 HTTPS sertifika hatası vermesin diye kullanılmıyor.
const base = '/api';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  get<T>(path: string, params?: Record<string, string | number>): Observable<T> {
    let httpParams = new HttpParams();
    if (params) {
      Object.entries(params).forEach(([k, v]) => { httpParams = httpParams.set(k, String(v)); });
    }
    return this.http.get<T>(`${base}/${path}`, { params: httpParams });
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${base}/${path}`, body);
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${base}/${path}`, body);
  }

  delete(path: string): Observable<void> {
    return this.http.delete<void>(`${base}/${path}`);
  }
}
