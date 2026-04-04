import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DecimalPipe } from '@angular/common';
import {
  ProductTemplate,
  ProductTemplatesService,
} from '../../core/services/product-templates.service';
import {
  ProductTemplateDialogComponent,
  ProductTemplateDialogData,
} from './product-template-dialog.component';

@Component({
  selector: 'app-product-templates-page',
  standalone: true,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule,
    DecimalPipe,
  ],
  templateUrl: './product-templates-page.component.html',
  styleUrl: './product-templates-page.component.scss',
})
export class ProductTemplatesPageComponent implements OnInit {
  private api = inject(ProductTemplatesService);
  private dialog = inject(MatDialog);

  displayedColumns = ['name', 'category', 'milyemSatis', 'milyemAlis', 'defaultGram', 'defaultLaborPrice', 'actions'];
  rows = signal<ProductTemplate[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openCreate(): void {
    this.openDialog(null);
  }

  edit(t: ProductTemplate): void {
    this.openDialog(t);
  }

  private openDialog(template: ProductTemplate | null): void {
    const ref = this.dialog.open<ProductTemplateDialogComponent, ProductTemplateDialogData, boolean>(
      ProductTemplateDialogComponent,
      {
        width: '520px',
        maxWidth: '95vw',
        data: { template },
      }
    );
    ref.afterClosed().subscribe((saved) => {
      if (saved) this.reload();
    });
  }

  remove(t: ProductTemplate): void {
    if (!confirm(`“${t.name}” silinsin mi?`)) return;
    this.api.delete(t.id).subscribe(() => this.reload());
  }
}
