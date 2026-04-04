import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { ProductTemplate, ProductTemplatesService } from '../../core/services/product-templates.service';

export interface ProductTemplateDialogData {
  template: ProductTemplate | null;
}

@Component({
  selector: 'app-product-template-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.template ? 'Şablonu düzenle' : 'Yeni ürün şablonu' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Ürün adı</mat-label>
          <input matInput formControlName="name" placeholder="örn. Çeyrek Altın Yeni Tarihli" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Kategori</mat-label>
          <input matInput formControlName="category" placeholder="örn. Sarrafiye, Ziynet" />
        </mat-form-field>
        <div class="milyem-row">
          <mat-form-field appearance="outline" class="milyem-field">
            <mat-label>Milyem satış</mat-label>
            <input matInput type="number" formControlName="milyemSatis" step="0.001" min="0" max="1000" placeholder="örn. 0,923" />
          </mat-form-field>
          <mat-form-field appearance="outline" class="milyem-field">
            <mat-label>Milyem alış</mat-label>
            <input matInput type="number" formControlName="milyemAlis" step="0.001" min="0" max="1000" placeholder="örn. 0,916" />
          </mat-form-field>
        </div>
        <mat-form-field appearance="outline">
          <mat-label>Varsayılan gram (gr)</mat-label>
          <input matInput type="number" formControlName="defaultGram" step="0.001" min="0" placeholder="örn. 1,75 Çeyrek" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Varsayılan birim işçilik (₺)</mat-label>
          <input matInput type="number" formControlName="defaultLaborPrice" step="0.01" min="0" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>İptal</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving">Kaydet</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .dlg-body {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      min-width: 400px;
      max-width: 520px;
      padding-top: 0.5rem;
    }
    .full { width: 100%; }
    mat-form-field { width: 100%; }
    .milyem-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.75rem;
    }
    .milyem-field { width: 100%; }
  `,
})
export class ProductTemplateDialogComponent {
  private fb = inject(FormBuilder);
  private api = inject(ProductTemplatesService);
  private ref = inject(MatDialogRef<ProductTemplateDialogComponent, boolean>);
  data = inject<ProductTemplateDialogData>(MAT_DIALOG_DATA);

  saving = false;

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    category: [''],
    milyemSatis: [0.923, [Validators.required, Validators.min(0), Validators.max(1000)]],
    milyemAlis: [0.916, [Validators.required, Validators.min(0), Validators.max(1000)]],
    defaultGram: [0, [Validators.required, Validators.min(0)]],
    defaultLaborPrice: [0, [Validators.required, Validators.min(0)]],
  });

  constructor() {
    const t = this.data.template;
    if (t) {
      this.form.patchValue({
        name: t.name,
        category: t.category ?? '',
        milyemSatis: t.milyemSatis,
        milyemAlis: t.milyemAlis,
        defaultGram: t.defaultGram,
        defaultLaborPrice: t.defaultLaborPrice,
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const dto = {
      name: v.name.trim(),
      category: v.category?.trim() || null,
      milyemSatis: v.milyemSatis,
      milyemAlis: v.milyemAlis,
      defaultGram: v.defaultGram,
      defaultLaborPrice: v.defaultLaborPrice,
    };
    this.saving = true;
    const op = this.data.template
      ? this.api.update(this.data.template.id, dto)
      : this.api.create(dto);
    op.subscribe({
      next: () => {
        this.saving = false;
        this.ref.close(true);
      },
      error: () => {
        this.saving = false;
      },
    });
  }
}
