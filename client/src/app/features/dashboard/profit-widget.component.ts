import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import { CapitalService, CapitalSummary } from '../../core/services/capital.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';

@Component({
  selector: 'app-profit-widget',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
  ],
  template: `
    <mat-card class="profit-card">
      <mat-card-header>
        <div class="widget-header">
          <mat-icon class="widget-icon">analytics</mat-icon>
          <div>
            <h2 class="widget-title">Sermaye Özeti</h2>
            <p class="widget-subtitle">Anlık sermaye · Nakit bağlama: üstteki Hızlı Erişim</p>
          </div>
        </div>
      </mat-card-header>
      <mat-card-content>
        @if (loading()) {
          <div class="loading-state">
            <mat-icon class="spin">refresh</mat-icon>
            Yükleniyor...
          </div>
        } @else if (capitalSummary()) {
          <div class="profit-display">
            <!-- Detaylar Grid -->
            <div class="details-grid">
              <!-- Net Sermaye -->
              <div class="detail-card highlight">
                <div class="detail-icon-wrapper capital">
                  <mat-icon>account_balance</mat-icon>
                </div>
                <div class="detail-content">
                  <span class="detail-label">Net Sermaye</span>
                  <span class="detail-value">{{ capitalSummary()!.netGoldCapital | number:'1.2-2' }} Has Gr</span>
                </div>
              </div>

              <!-- Kasadaki Altın -->
              <div class="detail-card">
                <div class="detail-icon-wrapper gold">
                  <mat-icon>stars</mat-icon>
                </div>
                <div class="detail-content">
                  <span class="detail-label">Kasadaki Altın</span>
                  <span class="detail-value">{{ capitalSummary()!.totalGoldInSafe | number:'1.2-2' }} Has Gr</span>
                </div>
              </div>

              <!-- Cari Borç -->
              <div class="detail-card">
                <div class="detail-icon-wrapper debt">
                  <mat-icon>business</mat-icon>
                </div>
                <div class="detail-content">
                  <span class="detail-label">Carilere Borç</span>
                  <span class="detail-value">{{ capitalSummary()!.totalCustomerGoldDebt | number:'1.2-2' }} Has Gr</span>
                </div>
              </div>

              <!-- Cari Alacak -->
              <div class="detail-card">
                <div class="detail-icon-wrapper receivable">
                  <mat-icon>store</mat-icon>
                </div>
                <div class="detail-content">
                  <span class="detail-label">Carilerden Alacak</span>
                  <span class="detail-value">{{ capitalSummary()!.totalCustomerGoldReceivable | number:'1.2-2' }} Has Gr</span>
                </div>
              </div>
            </div>
          </div>
        } @else {
          <div class="empty-state">
            <mat-icon>insights</mat-icon>
            <p>Veri yükleniyor...</p>
          </div>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .profit-card {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border-radius: 16px;
      box-shadow: 0 8px 32px rgba(102, 126, 234, 0.3);
      height: 100%;
      display: flex;
      flex-direction: column;
    }

    :host ::ng-deep .mat-mdc-card-header {
      padding: 0.75rem 1rem 0.25rem 1rem;
    }
    :host ::ng-deep .mat-mdc-card-content {
      padding: 0.5rem 1rem 0.85rem 1rem !important;
    }

    .widget-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.15rem 0 0;
    }

    .widget-icon {
      font-size: 2rem;
      width: 2rem;
      height: 2rem;
      color: rgba(255, 255, 255, 0.9);
    }

    .widget-title {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 600;
      color: white;
    }

    .widget-subtitle {
      margin: 0.25rem 0 0 0;
      font-size: 0.875rem;
      color: rgba(255, 255, 255, 0.7);
    }

    mat-card-content {
      flex: 1;
      display: flex;
      flex-direction: column;
    }

    .loading-state {
      text-align: center;
      padding: 2rem;
      color: rgba(255, 255, 255, 0.9);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 1rem;
      flex: 1;
      justify-content: center;
    }

    .spin {
      animation: spin 1s linear infinite;
      font-size: 2rem;
      width: 2rem;
      height: 2rem;
    }

    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }

    .profit-display {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      flex: 1;
    }

    .details-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.65rem;
    }

    .detail-card {
      background: rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 0.7rem 0.85rem;
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      transition: all 0.3s ease;
    }

    .detail-card.highlight {
      background: rgba(255, 255, 255, 0.15);
      border: 1px solid rgba(255, 255, 255, 0.3);
      grid-column: span 2;
    }

    .detail-card:hover {
      background: rgba(255, 255, 255, 0.15);
      transform: translateY(-2px);
    }

    .detail-icon-wrapper {
      width: 40px;
      height: 40px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .detail-icon-wrapper.capital {
      background: linear-gradient(135deg, #6a1b9a 0%, #9c27b0 100%);
    }

    .detail-icon-wrapper.gold {
      background: linear-gradient(135deg, #ffa000 0%, #ffc107 100%);
    }

    .detail-icon-wrapper.debt {
      background: linear-gradient(135deg, #c62828 0%, #f44336 100%);
    }

    .detail-icon-wrapper.receivable {
      background: linear-gradient(135deg, #2e7d32 0%, #4caf50 100%);
    }

    .detail-icon-wrapper mat-icon {
      font-size: 1.5rem;
      width: 1.5rem;
      height: 1.5rem;
      color: white;
    }

    .detail-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .detail-label {
      font-size: 0.75rem;
      color: rgba(255, 255, 255, 0.7);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .detail-value {
      font-size: 1.1rem;
      font-weight: 600;
      color: white;
    }

    .detail-card.highlight .detail-value {
      font-size: 1.5rem;
      font-weight: 700;
    }

    .empty-state {
      text-align: center;
      padding: 3rem 1rem;
      color: rgba(255, 255, 255, 0.7);
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
    }

    .empty-state mat-icon {
      font-size: 3rem;
      width: 3rem;
      height: 3rem;
      margin-bottom: 1rem;
      opacity: 0.5;
    }

    @media (max-width: 768px) {
      .details-grid {
        grid-template-columns: 1fr;
      }

      .detail-card.highlight {
        grid-column: span 1;
      }
    }
  `]
})
export class ProfitWidgetComponent implements OnInit, OnDestroy {
  private capitalService = inject(CapitalService);
  private refreshService = inject(DashboardRefreshService);
  private refreshSub?: Subscription;

  capitalSummary = signal<CapitalSummary | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadData();
    this.refreshSub = this.refreshService.refresh$.subscribe(() => this.loadData());
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
  }

  loadData(): void {
    this.loading.set(true);
    this.capitalService.getSummary().subscribe({
      next: (data) => {
        this.capitalSummary.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

}
