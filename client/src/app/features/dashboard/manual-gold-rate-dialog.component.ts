import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { GoldRatesService } from '../../core/services/gold-rates.service';

@Component({
  selector: 'app-manual-gold-rate-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Manuel kur ayarla</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="dialog-body">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>HAS (TL / gr)</mat-label>
          <input matInput type="number" formControlName="hasTryPerGramMid" step="0.01" min="1" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>USD/TRY (isteğe bağlı)</mat-label>
          <input matInput type="number" formControlName="usdTryMid" step="0.0001" min="0" />
        </mat-form-field>
        <p class="hint">Türkiye takvim günü için geçerlidir. Canlı API yerine bu değer kullanılır.</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>İptal</button>
        <button mat-raised-button color="primary" type="submit" [disabled]="form.invalid || saving">Kaydet</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: [`
    .dialog-body { display: flex; flex-direction: column; gap: 0.5rem; min-width: 280px; padding-top: 0.25rem; }
    .full-width { width: 100%; }
    .hint { font-size: 0.8rem; color: rgba(0,0,0,.6); margin: 0; }
  `],
})
export class ManualGoldRateDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<ManualGoldRateDialogComponent>);
  private goldRates = inject(GoldRatesService);

  saving = false;

  form = this.fb.group({
    hasTryPerGramMid: [2500, [Validators.required, Validators.min(1)]],
    usdTryMid: [null as number | null],
  });

  save(): void {
    if (this.form.invalid)
      return;
    const v = this.form.getRawValue();
    const has = v.hasTryPerGramMid;
    if (has == null)
      return;
    const usd = v.usdTryMid;
    this.saving = true;
    this.goldRates.setManualDayRate(has, usd === null || usd === undefined || usd === 0 ? null : usd).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => { this.saving = false; },
    });
  }
}
