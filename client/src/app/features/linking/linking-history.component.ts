import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { LinkingService, LinkingProcessListItem } from '../../core/services/linking.service';
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
        <h1>Bağlantı Geçmişi (FIFO)</h1>
        <p class="sub">Parçalı nakit bağlama işlemleri; iptal edildiğinde kasa ve satış pozisyonları geri alınır.</p>
      </div>

      <mat-card>
        <mat-card-content>
          @if (loading()) {
            <div class="loading"><mat-spinner diameter="40"></mat-spinner></div>
          } @else {
            <table mat-table [dataSource]="dataSource()" class="mat-elevation-z0">
              <ng-container matColumnDef="linkingDate">
                <th mat-header-cell *matHeaderCellDef>Tarih</th>
                <td mat-cell *matCellDef="let row">{{ row.linkingDate | date:'dd.MM.yyyy HH:mm' }}</td>
              </ng-container>
              <ng-container matColumnDef="targetAmount">
                <th mat-header-cell *matHeaderCellDef>Has (gr)</th>
                <td mat-cell *matCellDef="let row">{{ row.targetAmount | number:'1.4-4' }}</td>
              </ng-container>
              <ng-container matColumnDef="targetPrice">
                <th mat-header-cell *matHeaderCellDef>Fiyat (TL/gr)</th>
                <td mat-cell *matCellDef="let row">{{ row.targetPrice | number:'1.4-4' }}</td>
              </ng-container>
              <ng-container matColumnDef="totalProfit">
                <th mat-header-cell *matHeaderCellDef>Tahmini Kâr (TL)</th>
                <td mat-cell *matCellDef="let row" [class.negative]="row.totalProfit < 0" [class.positive]="row.totalProfit >= 0">
                  {{ row.totalProfit | number:'1.2-2' }}
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let row">
                  <button mat-icon-button color="warn" matTooltip="İptal et" [disabled]="deleting() === row.id" (click)="onCancel(row)">
                    <mat-icon>delete_forever</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
            </table>
            @if (dataSource().length === 0) {
              <p class="empty">Henüz bağlantı kaydı yok.</p>
            }
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .page { padding: 1rem 1.5rem; max-width: 1100px; margin: 0 auto; }
    .page-header h1 { margin: 0.5rem 0; font-size: 1.5rem; }
    .sub { color: rgba(255,255,255,.65); margin: 0 0 1rem; font-size: 0.9rem; }
    .loading { display: flex; justify-content: center; padding: 2rem; }
    table { width: 100%; }
    .negative { color: #ef9a9a; }
    .positive { color: #a5d6a7; }
    .empty { padding: 1rem; text-align: center; opacity: 0.7; }
  `],
})
export class LinkingHistoryComponent implements OnInit {
  private readonly api = inject(LinkingService);
  private readonly notify = inject(NotificationService);
  private readonly refresh = inject(DashboardRefreshService);

  loading = signal(true);
  deleting = signal<string | null>(null);
  dataSource = signal<LinkingProcessListItem[]>([]);
  displayedColumns = ['linkingDate', 'targetAmount', 'targetPrice', 'totalProfit', 'actions'];

  ngOnInit(): void {
    this.load();
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
    const ok = await this.notify.confirmDelete(
      `Bu FIFO bağlantısını (${row.targetAmount.toFixed(4)} Has @ ${row.targetPrice.toFixed(2)} TL/gr) iptal etmek istediğinize emin misiniz? Kasa ve satış pozisyonları geri alınacaktır.`
    );
    if (!ok) return;
    this.deleting.set(row.id);
    this.api.cancel(row.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.notify.success('Bağlantı iptal edildi');
        this.refresh.triggerRefresh();
        this.load();
      },
      error: (err) => {
        this.deleting.set(null);
        this.notify.error('İptal hatası', err?.error?.message || err?.message || 'Bilinmeyen hata');
      },
    });
  }
}
