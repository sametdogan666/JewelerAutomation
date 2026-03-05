import { Component, inject } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/services/auth.service';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    NgIf,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loading = false;
  error = '';

  // constructor'da token temizleme YOK — bu döngüye neden oluyordu

  form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  onSubmit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.error = '';
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading = false;
        // #region agent log
        console.log('[LOGIN] Login success, before navigate', {isAuthNow:this.auth.isAuthenticated(), timestamp:new Date().toISOString()});
        // #endregion
        // Token zaten localStorage'da; guard doğrudan okur
        const returnUrl = this.route.snapshot.queryParams['returnUrl'] ?? '/';
        console.log('[LOGIN] Navigating to:', returnUrl);
        this.router.navigateByUrl(returnUrl);
      },
      error: (err) => {
        this.loading = false;
        const body = err.error;
        if (err.status === 401) {
          this.error = 'Kullanıcı adı veya şifre hatalı.';
        } else if (err.status === 0) {
          this.error = 'Sunucuya bağlanılamıyor. API çalışıyor mu?';
        } else {
          this.error = typeof body === 'string' ? body : (body?.message ?? `Hata (${err.status}).`);
        }
      },
    });
  }
}
