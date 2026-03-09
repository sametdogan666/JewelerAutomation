import { Component, inject, OnInit, signal, ChangeDetectorRef } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { TransactionsService, Transaction, TransactionDirection } from '../../core/services/transactions.service';

@Component({
  selector: 'app-transactions-list',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSelectModule,
    MatFormFieldModule,
    MatDatepickerModule,
    MatNativeDateModule,
  ],
  templateUrl: './transactions-list.component.html',
  styleUrl: './transactions-list.component.scss',
})
export class TransactionsListComponent implements OnInit {
  private api = inject(TransactionsService);
  private cdr = inject(ChangeDetectorRef);

  dataSource = new MatTableDataSource<Transaction>([]);
  loading = signal(true);
  deleting = signal<string | null>(null);
  displayedColumns: string[] = ['transactionDate', 'direction', 'quantity', 'milyem', 'hasGram', 'price', 'customer', 'description', 'actions'];

  // Date filter
  dateFilterType = new FormControl<'all' | 'today' | 'week' | 'custom'>('all');
  dateRangeForm = new FormGroup({
    startDate: new FormControl<Date | null>(null),
    endDate: new FormControl<Date | null>(null),
  });

  ngOnInit(): void {
    this.loadData();
    
    // Watch for filter changes
    this.dateFilterType.valueChanges.subscribe((type) => {
      this.onDateFilterChange(type || 'all');
    });
  }

  private loadData(params?: { from?: string; to?: string }): void {
    this.loading.set(true);
    this.api.getAll(params).subscribe({
      next: (list) => {
        console.log('[TRANSACTIONS] Data received:', list);
        console.log('[TRANSACTIONS] First item price:', list[0]?.price);
        console.log('[TRANSACTIONS] Displayed columns:', this.displayedColumns);
        console.log('[TRANSACTIONS] Filter params:', params);
        this.dataSource.data = list;
        this.loading.set(false);
        // Force change detection for table rendering
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('[TRANSACTIONS] Error loading data:', err);
        this.loading.set(false);
      },
    });
  }

  onDateFilterChange(type: 'all' | 'today' | 'week' | 'custom'): void {
    const now = new Date();
    let params: { from?: string; to?: string } | undefined;

    switch (type) {
      case 'today':
        const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const todayEnd = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
        params = {
          from: todayStart.toISOString(),
          to: todayEnd.toISOString(),
        };
        break;
      case 'week':
        const weekStart = new Date(now);
        weekStart.setDate(now.getDate() - 7);
        params = {
          from: weekStart.toISOString(),
          to: now.toISOString(),
        };
        break;
      case 'custom':
        // Custom range will be applied when user clicks "Uygula" button
        return;
      case 'all':
      default:
        params = undefined;
        break;
    }

    this.loadData(params);
  }

  applyCustomDateRange(): void {
    const startDate = this.dateRangeForm.value.startDate;
    const endDate = this.dateRangeForm.value.endDate;

    if (startDate && endDate) {
      const params = {
        from: startDate.toISOString(),
        to: endDate.toISOString(),
      };
      this.loadData(params);
    }
  }

  clearCustomDateRange(): void {
    this.dateRangeForm.reset();
    this.dateFilterType.setValue('all');
  }

  directionLabel(d: TransactionDirection): string {
    return d === 0 ? 'Satış' : 'Alış';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }

  onDelete(transaction: Transaction): void {
    if (!confirm(`"${transaction.description || 'Bu işlem'}" kaydını silmek istediğinizden emin misiniz? İlişkili kasa hareketi de silinecektir.`)) {
      return;
    }
    this.deleting.set(transaction.id);
    this.api.delete(transaction.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.dataSource.data = this.dataSource.data.filter(t => t.id !== transaction.id);
        this.cdr.detectChanges();
      },
      error: () => {
        this.deleting.set(null);
        this.cdr.detectChanges();
      },
    });
  }
}
