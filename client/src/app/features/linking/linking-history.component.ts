import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { LinkingService, LinkingProcessListItem } from '../../core/services/linking.service';
import { TransactionsService } from '../../core/services/transactions.service';
import { NotificationService } from '../../core/services/notification.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';

@Component({
  selector: 'app-linking-history',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  template: `
    <div class="page">
      <div class="page-header">
        <a mat-button routerLink="/dashboard"><mat-icon>arrow_back</mat-icon> Panele dön</a>
        <h1>Bağlantı Geçmişi</h1>
        <p class="sub">FIFO parçalı bağlama ve dönem nakit bağlama (hibrit) kayıtları. Silindiğinde kasa, defter ve açık satış pozisyonu güncellenir.</p>
      </div>

      <mat-card>
        <mat-card-content>
          @if (loading()) {
            <div class="loading"><mat-spinner diameter="40"></mat-spinner></div>
          } @else {
            <div class="table-wrap">
              <table mat-table [dataSource]="dataSource()" class="mat-elevation-z0 history-table">
                <ng-container matColumnDef="kind">
                  <th mat-header-cell *matHeaderCellDef>Tür</th>
                  <td mat-cell *matCellDef="let row">
                    <span class="kind-badge" [class.kind-badge--hybrid]="isHybrid(row)">
                      {{ isHybrid(row) ? 'Nakit bağlama' : 'FIFO' }}
                    </span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="linkingDate">
                  <th mat-header-cell *matHeaderCellDef>Tarih</th>
                  <td mat-cell *matCellDef="let row">{{ row.linkingDate | date:'dd.MM.yyyy HH:mm' }}</td>
                </ng-container>
                <ng-container matColumnDef="periodRange">
                  <th mat-header-cell *matHeaderCellDef>Dönem</th>
                  <td mat-cell *matCellDef="let row">{{ formatPeriod(row) }}</td>
                </ng-container>
                <ng-container matColumnDef="targetAmount">
                  <th mat-header-cell *matHeaderCellDef>Has (gr)</th>
                  <td mat-cell *matCellDef="let row">{{ row.targetAmount | number:'1.4-4' }}</td>
                </ng-container>
                <ng-container matColumnDef="targetPrice">
                  <th mat-header-cell *matHeaderCellDef>Bağlama fiyatı (₺/gr)</th>
                  <td mat-cell *matCellDef="let row">{{ row.targetPrice | number:'1.2-2' }}</td>
                </ng-container>
                <ng-container matColumnDef="cashAmount">
                  <th mat-header-cell *matHeaderCellDef>Toplam nakit (₺)</th>
                  <td mat-cell *matCellDef="let row">
                    @if (isHybrid(row) && row.cashAmount != null) {
                      {{ row.cashAmount | number:'1.2-3' }}
                    } @else {
                      —
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="totalProfit">
                  <th mat-header-cell *matHeaderCellDef>Kâr (TL)</th>
                  <td mat-cell *matCellDef="let row" [class.negative]="row.totalProfit < 0" [class.positive]="row.totalProfit >= 0">
                    {{ row.totalProfit | number:'1.2-2' }}
                  </td>
                </ng-container>
                <ng-container matColumnDef="netProfitHasGram">
                  <th mat-header-cell *matHeaderCellDef>Net kâr (Has gr)</th>
                  <td mat-cell *matCellDef="let row">
                    @if (isHybrid(row) && row.netProfitHasGram != null) {
                      {{ row.netProfitHasGram >= 0 ? '+' : '' }}{{ row.netProfitHasGram | number:'1.4-4' }}
                    } @else {
                      —
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let row">
                    <button mat-icon-button color="warn" matTooltip="Sil / iptal et" [disabled]="deleting() === row.id" (click)="onCancel(row)">
                      <mat-icon>delete_forever</mat-icon>
                    </button>
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
              </table>
            </div>
            @if (dataSource().length === 0) {
              <p class="empty">Henüz kayıt yok.</p>
            }
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .page { padding: 1rem 1.5rem; max-width: 1280px; margin: 0 auto; }
    .page-header h1 { margin: 0.5rem 0; font-size: 1.5rem; }
    .sub { color: rgba(255,255,255,.65); margin: 0 0 1rem; font-size: 0.9rem; }
    .loading { display: flex; justify-content: center; padding: 2rem; }
    .table-wrap { overflow-x: auto; }
    .history-table { min-width: 960px; width: 100%; }
    .negative { color: #ef9a9a; }
    .positive { color: #a5d6a7; }
    .empty { padding: 1rem; text-align: center; opacity: 0.7; }
    .kind-badge {
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      padding: 0.2rem 0.45rem;
      border-radius: 4px;
      background: rgba(100, 149, 237, 0.25);
      color: #b3c7ff;
    }
    .kind-badge--hybrid {
      background: rgba(76, 175, 80, 0.25);
      color: #c8e6c9;
    }
  `],
})
export class LinkingHistoryComponent implements OnInit, OnDestroy {
  private readonly api = inject(LinkingService);
  private readonly transactionsApi = inject(TransactionsService);
  private readonly notify = inject(NotificationService);
  private readonly refresh = inject(DashboardRefreshService);
  private refreshSub?: Subscription;

  loading = signal(true);
  deleting = signal<string | null>(null);
  dataSource = signal<LinkingProcessListItem[]>([]);
  displayedColumns = [
    'kind',
    'linkingDate',
    'periodRange',
    'targetAmount',
    'targetPrice',
    'cashAmount',
    'totalProfit',
    'netProfitHasGram',
    'actions',
  ];

  ngOnInit(): void {
    this.load();
    this.refreshSub = this.refresh.refresh$.subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
  }

  isHybrid(row: LinkingProcessListItem): boolean {
    return (row.kind ?? 'Fifo') === 'Hybrid';
  }

  formatPeriod(row: LinkingProcessListItem): string {
    if (!this.isHybrid(row) || !row.periodStartDate || !row.periodEndDate) return '—';
    const a = new Date(row.periodStartDate);
    const b = new Date(row.periodEndDate);
    return `${a.toLocaleDateString('tr-TR')} – ${b.toLocaleDateString('tr-TR')}`;
  }

  load(): void {
    this.loading.set(true);
    this.api.getHistory().subscribe({
      next: (list) => {
        this.dataSource.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  async onCancel(row: LinkingProcessListItem): Promise<void> {
    const hybrid = this.isHybrid(row);
    const ok = await this.notify.confirmDelete(
      hybrid
        ? `Bu dönem nakit bağlama kaydını silmek istediğinize emin misiniz? (${row.cashAmount != null ? row.cashAmount.toFixed(2) : '—'} ₺ → ${row.targetAmount.toFixed(4)} Has gr) Kasa hareketleri, defter ve FIFO pozisyonları geri alınır.`
        : `Bu FIFO bağlantısını (${row.targetAmount.toFixed(4)} Has @ ${row.targetPrice.toFixed(2)} TL/gr) iptal etmek istediğinize emin misiniz?`
    );
    if (!ok) return;
    this.deleting.set(row.id);

    const done = (): void => {
      this.deleting.set(null);
      this.notify.success(hybrid ? 'Nakit bağlama silindi' : 'Bağlantı iptal edildi');
      this.refresh.triggerRefresh();
      this.load();
    };

    const fail = (err: unknown): void => {
      this.deleting.set(null);
      const msg =
        err && typeof err === 'object' && 'error' in err
          ? (err as { error?: { message?: string } }).error?.message
          : undefined;
      this.notify.error('İşlem hatası', msg || (err instanceof Error ? err.message : 'Bilinmeyen hata'));
    };

    if (hybrid) {
      this.transactionsApi.delete(row.id).subscribe({ next: () => done(), error: (e) => fail(e) });
    } else {
      this.api.cancel(row.id).subscribe({ next: () => done(), error: (e) => fail(e) });
    }
  }
}
