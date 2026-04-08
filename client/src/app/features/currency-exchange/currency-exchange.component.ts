import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { startWith } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DecimalPipe } from '@angular/common';
import {
  CurrencyExchangeService,
  ForexBaseCurrencyCode,
} from '../../core/services/currency-exchange.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';

@Component({
  selector: 'app-currency-exchange',
  standalone: true,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatRadioModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    DecimalPipe,
  ],
  template: `
    <div class="page">
      <a mat-button routerLink="/dashboard" class="back">
        <mat-icon>arrow_back</mat-icon>
        Panele dön
      </a>
      <h1>Döviz (Borsa)</h1>
      <p class="sub">
        Saf döviz alış/satış: TL kasası ile USD, EUR veya GBP arasında tek kayıt.
        Örnek: <strong>100 GBP alış</strong>, kur <strong>42,50</strong> → GBP kasa +100, TRY kasa −4.250.
        İşlem <strong>Alış-Satış listesinde</strong> “Döviz İşlemi” olarak görünür.
      </p>

      <mat-card class="borsa-card">
        <mat-card-content [formGroup]="form">
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>Tarih</mat-label>
              <input matInput type="date" formControlName="transactionDate" />
            </mat-form-field>
          </div>

          <div class="row">
            <label class="field-label">Temel döviz</label>
            <mat-form-field appearance="outline" class="grow">
              <mat-label>Para birimi</mat-label>
              <mat-select formControlName="baseCurrency">
                <mat-option [value]="1">USD ($)</mat-option>
                <mat-option [value]="2">EUR (€)</mat-option>
                <mat-option [value]="3">GBP (£)</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          <div class="row">
            <label class="field-label">İşlem</label>
            <mat-radio-group formControlName="isBuy" class="radio-row">
              <mat-radio-button [value]="true">Alış (döviz alıyorum, TRY ödüyorum)</mat-radio-button>
              <mat-radio-button [value]="false">Satış (döviz veriyorum, TRY alıyorum)</mat-radio-button>
            </mat-radio-group>
          </div>

          <div class="row row-pair">
            <mat-form-field appearance="outline">
              <mat-label>Tutar (döviz)</mat-label>
              <input matInput type="number" formControlName="amountBase" step="0.000001" min="0" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Kur (1 birim döviz = ? TRY)</mat-label>
              <input matInput type="number" formControlName="rateTryPerUnit" step="0.0001" min="0" />
            </mat-form-field>
          </div>

          <div class="counter-box">
            <mat-icon>calculate</mat-icon>
            <div>
              <div class="counter-label">TRY karşılığı (otomatik)</div>
              <div class="counter-val">{{ counterTry() | number:'1.2-2' }} ₺</div>
            </div>
          </div>

          <mat-form-field appearance="outline" class="full">
            <mat-label>Açıklama (isteğe bağlı)</mat-label>
            <input matInput formControlName="description" />
          </mat-form-field>

          <div class="actions">
            <button
              mat-flat-button
              color="primary"
              type="button"
              (click)="onSubmit()"
              [disabled]="form.invalid || saving()">
              @if (saving()) {
                <mat-spinner diameter="20" />
              } @else {
                <mat-icon>swap_horiz</mat-icon>
              }
              Kaydet
            </button>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .page { padding: 1.25rem; max-width: 560px; margin: 0 auto; }
    .back { margin-bottom: 0.75rem; }
    h1 { font-size: 1.35rem; margin: 0 0 0.35rem 0; }
    .sub { color: var(--text-secondary, #666); font-size: 0.875rem; margin: 0 0 1.25rem 0; line-height: 1.45; }
    .borsa-card mat-card-content { padding-top: 1rem !important; }
    .row { display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; align-items: center; }
    .row-pair mat-form-field { flex: 1; min-width: 160px; }
    .grow { flex: 1; min-width: 200px; }
    .field-label { font-size: 0.8rem; font-weight: 600; color: var(--text-secondary, #666); min-width: 100px; }
    .radio-row { display: flex; flex-direction: column; gap: 0.35rem; }
    .full { width: 100%; }
    .counter-box {
      display: flex; align-items: center; gap: 0.75rem;
      padding: 0.85rem 1rem; margin-bottom: 0.75rem;
      background: #f5f7fa; border-radius: 8px; border: 1px solid #e0e4ea;
    }
    .counter-box mat-icon { color: #5c6bc0; }
    .counter-label { font-size: 0.75rem; color: #666; }
    .counter-val { font-size: 1.25rem; font-weight: 700; color: #1a237e; }
    .actions { margin-top: 1rem; }
    .actions button mat-spinner { display: inline-block; vertical-align: middle; margin-right: 0.35rem; }
  `,
})
export class CurrencyExchangeComponent {
  private fb = inject(FormBuilder);
  private api = inject(CurrencyExchangeService);
  private refresh = inject(DashboardRefreshService);
  private snack = inject(MatSnackBar);

  saving = signal(false);

  form = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    baseCurrency: [3 as ForexBaseCurrencyCode, Validators.required],
    isBuy: [true, Validators.required],
    amountBase: [0, [Validators.required, Validators.min(0.000001)]],
    rateTryPerUnit: [0, [Validators.required, Validators.min(0.000001)]],
    description: [''],
  });

  private formVal = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });

  counterTry = computed(() => {
    const v = this.formVal();
    const a = Number(v?.amountBase ?? 0);
    const r = Number(v?.rateTryPerUnit ?? 0);
    if (!Number.isFinite(a) || !Number.isFinite(r)) return 0;
    return Math.round(a * r * 100) / 100;
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .createForexTrade({
        transactionDate: new Date(v.transactionDate).toISOString(),
        baseCurrency: v.baseCurrency,
        isBuy: v.isBuy,
        amountBase: v.amountBase,
        rateTryPerUnit: v.rateTryPerUnit,
        description: v.description?.trim() || undefined,
      })
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          this.refresh.triggerRefresh();
          this.snack.open(`Döviz işlemi kaydedildi. Liste: ${res.transactionId.slice(0, 8)}…`, 'Tamam', {
            duration: 5000,
          });
          this.form.patchValue({ amountBase: 0, rateTryPerUnit: 0, description: '' });
        },
        error: () => {
          this.saving.set(false);
          this.snack.open('Kayıt başarısız; tutar ve kur kontrol edin.', 'Tamam', { duration: 6000 });
        },
      });
  }
}
