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
import { NotificationService } from '../../core/services/notification.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';

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
  private notify = inject(NotificationService);
  private refreshService = inject(DashboardRefreshService);

  dataSource = new MatTableDataSource<Transaction>([]);
  loading = signal(true);
  deleting = signal<string | null>(null);
  expandedRow = signal<string | null>(null);
  displayedColumns: string[] = [
    'transactionDate', 'itemCount', 'direction', 'netHasGram', 'netCashAmount',
    'customer', 'description', 'actions'
  ];

  dateFilterType = new FormControl<'all' | 'today' | 'week' | 'custom'>('all');
  dateRangeForm = new FormGroup({
    startDate: new FormControl<Date | null>(null),
    endDate: new FormControl<Date | null>(null),
  });

  ngOnInit(): void {
    this.loadData();
    this.dateFilterType.valueChanges.subscribe((type) => {
      this.onDateFilterChange(type || 'all');
    });
  }

  private loadData(params?: { from?: string; to?: string }): void {
    this.loading.set(true);
    this.api.getAll(params).subscribe({
      next: (list) => {
        this.dataSource.data = list;
        this.loading.set(false);
        this.cdr.detectChanges();
      },
      error: () => this.loading.set(false),
    });
  }

  onDateFilterChange(type: 'all' | 'today' | 'week' | 'custom'): void {
    const now = new Date();
    let params: { from?: string; to?: string } | undefined;

    switch (type) {
      case 'today': {
        const s = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const e = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
        params = { from: s.toISOString(), to: e.toISOString() };
        break;
      }
      case 'week': {
        const ws = new Date(now);
        ws.setDate(now.getDate() - 7);
        params = { from: ws.toISOString(), to: now.toISOString() };
        break;
      }
      case 'custom':
        return;
      default:
        params = undefined;
    }
    this.loadData(params);
  }

  applyCustomDateRange(): void {
    const s = this.dateRangeForm.value.startDate;
    const e = this.dateRangeForm.value.endDate;
    if (s && e) this.loadData({ from: s.toISOString(), to: e.toISOString() });
  }

  clearCustomDateRange(): void {
    this.dateRangeForm.reset();
    this.dateFilterType.setValue('all');
  }

  netLabel(tx: Transaction): string {
    if (tx.netHasGram > 0) return 'Alış (Net)';
    if (tx.netHasGram < 0) return 'Satış (Net)';
    return 'Dengeli';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }

  toggleExpand(tx: Transaction): void {
    this.expandedRow.set(this.expandedRow() === tx.id ? null : tx.id);
  }

  async onDelete(tx: Transaction): Promise<void> {
    const desc = tx.description || 'Bu sepet';
    const confirmed = await this.notify.confirmDelete(
      `"${desc}" kaydını silmek istediğinizden emin misiniz? Tüm kalemler ve kasa hareketleri de silinecektir.`
    );
    if (!confirmed) return;

    this.deleting.set(tx.id);
    this.api.delete(tx.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.dataSource.data = this.dataSource.data.filter(t => t.id !== tx.id);
        this.notify.success('İşlem silindi');
        this.refreshService.triggerRefresh();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.deleting.set(null);
        this.notify.error('Silme Hatası', err?.error?.message || 'İşlem silinirken bir hata oluştu.');
        this.cdr.detectChanges();
      },
    });
  }
}
