import { Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe, NgIf } from '@angular/common';
import { SafeService, SafeStatus } from '../../core/services/safe.service';

@Component({
  selector: 'app-safe-status-widget',
  standalone: true,
  imports: [MatCardModule, MatIconModule, DecimalPipe, NgIf],
  template: `
    <mat-card class="safe-status-card">
      <mat-card-header>
        <div class="widget-header">
          <mat-icon class="widget-icon">account_balance_wallet</mat-icon>
          <h2 class="widget-title">Kasa Durumu</h2>
        </div>
      </mat-card-header>
      <mat-card-content>
        @if (loading()) {
          <div class="loading-state">Yükleniyor...</div>
        } @else if (status()) {
          <div class="status-grid">
            <div class="status-item status-item--gold">
              <mat-icon class="status-icon">stars</mat-icon>
              <div class="status-info">
                <div class="status-label">Kasadaki Altın</div>
                <div class="status-value">{{ status()!.goldBalance | number:'1.2-4' }} <span class="unit">Has Gr</span></div>
              </div>
            </div>

            <div class="status-item status-item--cash">
              <mat-icon class="status-icon">payments</mat-icon>
              <div class="status-info">
                <div class="status-label">Kasadaki Nakit</div>
                <div class="status-value">{{ status()!.cashBalance | number:'1.2-2' }} <span class="unit">TRY</span></div>
              </div>
            </div>

            <div class="status-item status-item--expected">
              <mat-icon class="status-icon">trending_up</mat-icon>
              <div class="status-info">
                <div class="status-label">Beklenen Altın</div>
                <div class="status-value">{{ status()!.expectedGold | number:'1.2-4' }} <span class="unit">Has Gr</span></div>
              </div>
            </div>

            <div class="status-item" [ngClass]="shortageClass()">
              <mat-icon class="status-icon">{{ shortageIcon() }}</mat-icon>
              <div class="status-info">
                <div class="status-label">Altın Açığı / Fazlası</div>
                <div class="status-value">{{ status()!.goldShortage | number:'1.2-4' }} <span class="unit">Has Gr</span></div>
                @if (hasShortage()) {
                  <div class="shortage-warning">
                    <mat-icon class="warning-icon">warning</mat-icon>
                    Altın açığı tespit edildi!
                  </div>
                }
                @if (hasSurplus()) {
                  <div class="surplus-info">
                    <mat-icon class="info-icon">check_circle</mat-icon>
                    Altın fazlası mevcut
                  </div>
                }
              </div>
            </div>
          </div>
        } @else {
          <div class="error-state">Veri yüklenemedi.</div>
        }
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .safe-status-card {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border-radius: 16px;
      box-shadow: 0 8px 32px rgba(102, 126, 234, 0.3);
    }

    .widget-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 0;
    }

    .widget-icon {
      font-size: 2rem;
      width: 2rem;
      height: 2rem;
      color: rgba(255, 255, 255, 0.9);
    }

    .widget-title {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 600;
      color: white;
    }

    .status-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 1rem;
      margin-top: 1.5rem;
    }

    .status-item {
      background: rgba(255, 255, 255, 0.15);
      backdrop-filter: blur(10px);
      border-radius: 12px;
      padding: 1.25rem;
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      transition: all 0.3s ease;
      border: 1px solid rgba(255, 255, 255, 0.2);
    }

    .status-item:hover {
      background: rgba(255, 255, 255, 0.2);
      transform: translateY(-2px);
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
    }

    .status-item--gold {
      border-left: 4px solid #ffd700;
    }

    .status-item--cash {
      border-left: 4px solid #4caf50;
    }

    .status-item--expected {
      border-left: 4px solid #2196f3;
    }

    .status-item--shortage {
      border-left: 4px solid #f44336;
      background: rgba(244, 67, 54, 0.2);
    }

    .status-item--surplus {
      border-left: 4px solid #4caf50;
      background: rgba(76, 175, 80, 0.2);
    }

    .status-icon {
      font-size: 2.5rem;
      width: 2.5rem;
      height: 2.5rem;
      color: rgba(255, 255, 255, 0.9);
      flex-shrink: 0;
    }

    .status-info {
      flex: 1;
    }

    .status-label {
      font-size: 0.875rem;
      color: rgba(255, 255, 255, 0.8);
      margin-bottom: 0.5rem;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      font-weight: 500;
    }

    .status-value {
      font-size: 1.75rem;
      font-weight: 700;
      color: white;
      line-height: 1.2;
    }

    .unit {
      font-size: 0.875rem;
      font-weight: 500;
      color: rgba(255, 255, 255, 0.7);
      margin-left: 0.25rem;
    }

    .shortage-warning {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.75rem;
      padding: 0.5rem 0.75rem;
      background: rgba(244, 67, 54, 0.3);
      border-radius: 8px;
      font-size: 0.875rem;
      font-weight: 600;
      color: #ffebee;
    }

    .surplus-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-top: 0.75rem;
      padding: 0.5rem 0.75rem;
      background: rgba(76, 175, 80, 0.3);
      border-radius: 8px;
      font-size: 0.875rem;
      font-weight: 600;
      color: #e8f5e9;
    }

    .warning-icon,
    .info-icon {
      font-size: 1.25rem;
      width: 1.25rem;
      height: 1.25rem;
    }

    .loading-state,
    .error-state {
      text-align: center;
      padding: 2rem;
      color: rgba(255, 255, 255, 0.8);
      font-size: 1rem;
    }

    @media (max-width: 768px) {
      .status-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class SafeStatusWidgetComponent implements OnInit {
  private safeService = inject(SafeService);

  status = signal<SafeStatus | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loadStatus();
  }

  loadStatus(): void {
    this.loading.set(true);
    this.safeService.getStatus().subscribe({
      next: (data) => {
        this.status.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  hasShortage(): boolean {
    return this.status()?.goldShortage ? this.status()!.goldShortage > 0 : false;
  }

  hasSurplus(): boolean {
    return this.status()?.goldShortage ? this.status()!.goldShortage < 0 : false;
  }

  shortageClass(): string {
    if (this.hasShortage()) return 'status-item--shortage';
    if (this.hasSurplus()) return 'status-item--surplus';
    return '';
  }

  shortageIcon(): string {
    if (this.hasShortage()) return 'trending_down';
    if (this.hasSurplus()) return 'trending_up';
    return 'horizontal_rule';
  }
}
