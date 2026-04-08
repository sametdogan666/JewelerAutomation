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
import { TransactionsService, Transaction, TransactionKind } from '../../core/services/transactions.service';
import { NotificationService } from '../../core/services/notification.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';
import { ThermalReceiptService } from '../../core/services/thermal-receipt.service';

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
  private thermalReceipt = inject(ThermalReceiptService);

  dataSource = new MatTableDataSource<Transaction>([]);
  loading = signal(true);
  deleting = signal<string | null>(null);
  printing = signal<string | null>(null);
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

  /** Saf döviz (Borsa) — altın yok. */
  isForexRow(tx: Transaction): boolean {
    return (tx.kind as TransactionKind | undefined) === 1;
  }

  /** Nakit bağlama kaydı: CorrelationId + sepet kalemi yok (sistem işlemi). */
  isNakitBaglamaRow(tx: Transaction): boolean {
    if (this.isForexRow(tx)) return false;
    return !!tx.correlationId && (!tx.items || tx.items.length === 0);
  }

  private fmtCash(n: number): string {
    return Math.abs(n).toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 });
  }

  /** Liste hücresi: ayrı para birimleri, sembol ile (karıştırılmaz). */
  netCashLines(tx: Transaction): string[] {
    if (this.isNakitBaglamaRow(tx)) return [];
    const lines: string[] = [];
    const push = (v: number | undefined, sym: string) => {
      const x = Number(v ?? 0);
      if (Math.abs(x) < 1e-9) return;
      const sign = x > 0 ? '+' : '−';
      lines.push(`${sign}${this.fmtCash(x)} ${sym}`);
    };
    push(tx.netCashAmount, '₺');
    push(tx.netCashAmountUsd, '$');
    push(tx.netCashAmountEur, '€');
    push(tx.netCashAmountGbp, '£');
    return lines;
  }

  hasAnyNetCash(tx: Transaction): boolean {
    return this.netCashLines(tx).length > 0;
  }

  /** Genişletilebilir: döviz özeti veya sepet kalemleri. */
  hasExpandableDetail(tx: Transaction): boolean {
    return this.isForexRow(tx) || (tx.items?.length ?? 0) > 0;
  }

  netCashLineClass(line: string): string {
    if (line.startsWith('+')) return 'net-cash-line net-cash-line--in';
    if (line.startsWith('−') || line.startsWith('-')) return 'net-cash-line net-cash-line--out';
    return 'net-cash-line';
  }

  forexBaseCode(tx: Transaction): string {
    const c = tx.forexBaseCurrency;
    if (c === 1) return 'USD';
    if (c === 2) return 'EUR';
    if (c === 3) return 'GBP';
    return '—';
  }

  forexBaseSymbol(tx: Transaction): string {
    const c = tx.forexBaseCurrency;
    if (c === 1) return '$';
    if (c === 2) return '€';
    if (c === 3) return '£';
    return '';
  }

  forexActionTr(tx: Transaction): string {
    return tx.forexIsBuy ? 'Alış' : 'Satış';
  }

  /**
   * Nakit bağlama: kasadan çıkan net nakit (negatif TL), Transaction.CashAmount / NetCashAmount üzerinden.
   */
  peggingNetCashSigned(tx: Transaction): number {
    const fromEntity =
      tx.cashAmount != null && tx.cashAmount !== undefined
        ? Number(tx.cashAmount)
        : null;
    const mag =
      fromEntity != null && !Number.isNaN(fromEntity) && Math.abs(fromEntity) > 0.000001
        ? Math.abs(fromEntity)
        : Math.abs(Number(tx.netCashAmount ?? tx.price ?? 0));
    return -mag;
  }

  /**
   * Nakit bağlama: üretilen has gr (pozitif), Transaction.EquivalentHasGram öncelikli.
   */
  peggingEquivalentHasGram(tx: Transaction): number {
    if (tx.equivalentHasGram != null && tx.equivalentHasGram !== undefined) {
      return Math.abs(Number(tx.equivalentHasGram));
    }
    return Math.abs(Number(tx.netHasGram ?? tx.hasGram ?? 0));
  }

  netLabel(tx: Transaction): string {
    if (this.isForexRow(tx)) return 'Döviz İşlemi';
    if (this.isNakitBaglamaRow(tx)) return 'Nakit Bağlama';
    if (tx.netHasGram > 0) return 'Alış (Net)';
    if (tx.netHasGram < 0) return 'Satış (Net)';
    return 'Dengeli';
  }

  itemCountLabel(tx: Transaction): string {
    if (this.isForexRow(tx) || this.isNakitBaglamaRow(tx)) return '—';
    const n = tx.items?.length ?? 0;
    return n > 0 ? String(n) : '1';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  }

  toggleExpand(tx: Transaction): void {
    this.expandedRow.set(this.expandedRow() === tx.id ? null : tx.id);
  }

  async printReceipt(tx: Transaction): Promise<void> {
    this.printing.set(tx.id);
    this.cdr.detectChanges();
    try {
      await this.thermalReceipt.openReceipt(tx);
    } catch (err) {
      console.error(err);
      this.notify.error('Fiş', 'PDF oluşturulamadı. Açılır pencere engellenmiş olabilir; tekrar deneyin.');
    } finally {
      this.printing.set(null);
      this.cdr.detectChanges();
    }
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
