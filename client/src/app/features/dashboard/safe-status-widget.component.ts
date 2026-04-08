import { Component, computed, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe, NgClass } from '@angular/common';
import { SafeStatus } from '../../core/services/safe.service';
import { DashboardSummary } from '../../core/services/dashboard.service';

function safeStatusFromDashboard(d: DashboardSummary): SafeStatus {
  return {
    physicalGoldBalance: d.physicalGoldBalance ?? 0,
    physicalCashBalance: d.physicalCashBalance ?? 0,
    physicalCashBalanceUsd: d.physicalCashBalanceUsd ?? 0,
    physicalCashBalanceEur: d.physicalCashBalanceEur ?? 0,
    physicalCashBalanceGbp: d.physicalCashBalanceGbp ?? 0,
    expectedGold: d.expectedGold ?? 0,
    goldGapOrSurplus: d.goldGapOrSurplus ?? 0,
    customerGoldDebt: d.totalCustomerGoldDebt ?? 0,
    customerGoldReceivable: d.totalCustomerGoldReceivable ?? 0,
    personalGoldDebt: d.totalPersonalGoldDebt ?? 0,
    personalGoldReceivable: d.totalPersonalGoldReceivable ?? 0,
    sahisGoldLiabilitiesHasGram: d.sahisGoldLiabilitiesHasGram ?? 0,
    netPhysicalEquityHasGram: d.netPhysicalEquityHasGram ?? 0,
    netGoldPosition: d.netGoldPositionHasGram ?? 0,
    netCashPosition: d.netCashPositionTl ?? 0,
    netCashPositionUsd: d.netCashPositionUsd ?? 0,
    netCashPositionEur: d.netCashPositionEur ?? 0,
    netCashPositionGbp: d.netCashPositionGbp ?? 0,
    profitHasGram: d.profitHasGram ?? 0,
    cumulativePeggingProfitHasGram: d.cumulativePeggingProfitHasGram ?? 0,
    peggingCount: d.peggingCount ?? 0,
  };
}

@Component({
  selector: 'app-safe-status-widget',
  standalone: true,
  imports: [MatCardModule, MatIconModule, DecimalPipe, NgClass],
  template: `
    <mat-card class="safe-widget">
      <mat-card-header>
        <div class="widget-header">
          <mat-icon class="widget-icon">account_balance_wallet</mat-icon>
          <div>
            <h2 class="widget-title">Kasa & Finansal Durum</h2>
            <p class="widget-subtitle">Fiziksel bakiye ve net pozisyon</p>
            @if (liveMid()) {
              <p class="widget-live">
                @if (ratesFromManual()) {
                  <span>Manuel kur {{ liveMid() | number:'1.2-2' }} ₺/gr</span>
                } @else if (ratesFromDefault()) {
                  <span>Varsayılan kur {{ liveMid() | number:'1.2-2' }} ₺/gr</span>
                } @else {
                  <span>Canlı has {{ liveMid() | number:'1.2-2' }} ₺/gr</span>
                }
                @if (netGoldTl()) {
                  <span> · Net Altın ≈ {{ netGoldTl() | number:'1.0-0' }} ₺</span>
                }
              </p>
            } @else if (showHasRateNa()) {
              <p class="widget-live widget-live--muted">Has kuru: N/A</p>
            }
          </div>
        </div>
      </mat-card-header>
      <mat-card-content>
          <div class="sections">

            <!-- ─── SECTION 1: Physical Balance (Brüt Kasa) ─── -->
            <div class="section">
              <h3 class="section-title">
                <mat-icon>inventory_2</mat-icon>
                Brüt Kasa
                <span class="section-hint">Fiziksel bakiye</span>
              </h3>

              <div class="metric-grid">
                <div class="metric metric--primary">
                  <div class="metric-icon gold"><mat-icon>stars</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Altın</span>
                    <span class="metric-value">{{ statusView()!.physicalGoldBalance | number:'1.2-2' }} <small>Has Gr</small></span>
                  </div>
                </div>

                <div class="metric metric--primary">
                  <div class="metric-icon cash"><mat-icon>payments</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Nakit</span>
                    <div class="metric-cash-stack">
                      <span class="metric-value-line">{{ statusView()!.physicalCashBalance | number:'1.2-2' }} <small>₺</small></span>
                      <span class="metric-value-line">{{ statusView()!.physicalCashBalanceUsd | number:'1.2-2' }} <small>USD</small></span>
                      <span class="metric-value-line">{{ statusView()!.physicalCashBalanceEur | number:'1.2-2' }} <small>EUR</small></span>
                      <span class="metric-value-line">{{ statusView()!.physicalCashBalanceGbp | number:'1.2-2' }} <small>GBP</small></span>
                    </div>
                  </div>
                </div>

                <div class="metric" [ngClass]="gapClass()">
                  <div class="metric-icon gap"><mat-icon>{{ gapIcon() }}</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Açık satış (FIFO)</span>
                    <span class="metric-value">{{ statusView()!.goldGapOrSurplus >= 0 ? '+' : '' }}{{ statusView()!.goldGapOrSurplus | number:'1.2-2' }} <small>Has Gr</small></span>
                    @if (statusView()!.goldGapOrSurplus > 0.001) {
                      <span class="metric-badge badge--surplus">
                        <mat-icon>check_circle</mat-icon> Alış Fazlası
                      </span>
                    } @else if (statusView()!.goldGapOrSurplus < -0.001) {
                      <span class="metric-badge badge--shortage">
                        <mat-icon>warning</mat-icon> Satış Açığı
                      </span>
                    }
                  </div>
                </div>
              </div>

              @if (vaultChartPolyline()) {
                <div class="vault-chart-block">
                  <span class="vault-chart-label">Fiziki kasa — kümülatif Has (tüm hareketler)</span>
                  <svg class="vault-chart-svg" viewBox="0 0 100 36" preserveAspectRatio="none">
                    <polyline
                      class="vault-chart-line"
                      fill="none"
                      stroke="rgba(255,255,255,0.92)"
                      stroke-width="0.9"
                      [attr.points]="vaultChartPolyline()!" />
                  </svg>
                  <div class="vault-chart-foot">
                    <span>{{ vaultChartStartLabel() }}</span>
                    <span>{{ vaultChartEndLabel() }}</span>
                  </div>
                </div>
              }
            </div>

            <!-- ─── Şahıs emanetleri (Has) & öz sermaye ─── -->
            <div class="section section--equity">
              <h3 class="section-title">
                <mat-icon>handshake</mat-icon>
                Şahıs emanetleri
                <span class="section-hint">Fiziki altın − şahıs yükümlülükleri</span>
              </h3>
              <div class="metric-grid metric-grid--tight">
                <div class="metric">
                  <div class="metric-icon gold"><mat-icon>inventory</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Fiziki altın (brüt)</span>
                    <span class="metric-value">{{ statusView()!.physicalGoldBalance | number:'1.2-2' }} <small>Has Gr</small></span>
                  </div>
                </div>
                <div class="metric">
                  <div class="metric-icon debt"><mat-icon>person_pin</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Şahıs emanet yükümlülüğü</span>
                    <span class="metric-value">{{ statusView()!.sahisGoldLiabilitiesHasGram | number:'1.2-2' }} <small>Has Gr</small></span>
                  </div>
                </div>
                <div class="metric metric--primary">
                  <div class="metric-icon cash"><mat-icon>balance</mat-icon></div>
                  <div class="metric-body">
                    <span class="metric-label">Öz sermaye (Has)</span>
                    <span class="metric-value">{{ statusView()!.netPhysicalEquityHasGram | number:'1.2-2' }} <small>Has Gr</small></span>
                  </div>
                </div>
              </div>
            </div>

            <!-- ─── SECTION 2: Net Position (Finansal Durum) ─── -->
            <div class="section section--financial">
              <h3 class="section-title">
                <mat-icon>account_balance</mat-icon>
                Finansal Durum
                <span class="section-hint">Kasa + Alacaklar − Borçlar</span>
              </h3>

              <!-- Net Position highlight — yan yana, kompakt -->
              <div class="net-row">
                <div class="net-position-card">
                  <div class="net-icon"><mat-icon>trending_up</mat-icon></div>
                  <div class="net-body">
                    <span class="net-label">Net Altın</span>
                    <span class="net-value">{{ statusView()!.netGoldPosition | number:'1.2-2' }} <small>Has Gr</small></span>
                  </div>
                </div>
                <div class="net-position-card net-position-card--cash">
                  <div class="net-icon net-icon--cash"><mat-icon>payments</mat-icon></div>
                  <div class="net-body">
                    <span class="net-label">Net Nakit</span>
                    <div class="net-cash-stack">
                      <span class="net-value-line">{{ statusView()!.netCashPosition | number:'1.0-0' }} <small>₺</small></span>
                      <span class="net-value-line">{{ statusView()!.netCashPositionUsd | number:'1.2-2' }} <small>USD</small></span>
                      <span class="net-value-line">{{ statusView()!.netCashPositionEur | number:'1.2-2' }} <small>EUR</small></span>
                      <span class="net-value-line">{{ statusView()!.netCashPositionGbp | number:'1.2-2' }} <small>GBP</small></span>
                    </div>
                  </div>
                </div>
              </div>

              <div class="debt-grid">
                <div class="debt-item">
                  <mat-icon class="debt-icon receivable">store</mat-icon>
                  <div class="debt-body">
                    <span class="debt-label">Carilerden Alacak</span>
                    <span class="debt-value">+{{ statusView()!.customerGoldReceivable | number:'1.2-2' }}</span>
                  </div>
                </div>
                <div class="debt-item">
                  <mat-icon class="debt-icon debt">business</mat-icon>
                  <div class="debt-body">
                    <span class="debt-label">Carilere Borç</span>
                    <span class="debt-value">−{{ statusView()!.customerGoldDebt | number:'1.2-2' }}</span>
                  </div>
                </div>
                <div class="debt-item">
                  <mat-icon class="debt-icon receivable">people_alt</mat-icon>
                  <div class="debt-body">
                    <span class="debt-label">Şahıslardan Alacak</span>
                    <span class="debt-value">+{{ statusView()!.personalGoldReceivable | number:'1.2-2' }}</span>
                  </div>
                </div>
                <div class="debt-item">
                  <mat-icon class="debt-icon debt">person</mat-icon>
                  <div class="debt-body">
                    <span class="debt-label">Şahıslara Borç</span>
                    <span class="debt-value">−{{ statusView()!.personalGoldDebt | number:'1.2-2' }}</span>
                  </div>
                </div>
              </div>

              <!-- Profit (reporting only) -->
              <div class="profit-strip" [ngClass]="profitClass()">
                <mat-icon>{{ statusView()!.profitHasGram >= 0 ? 'trending_up' : 'trending_down' }}</mat-icon>
                <span class="profit-label">Kâr / Zarar</span>
                <span class="profit-value">
                  {{ statusView()!.profitHasGram >= 0 ? '+' : '' }}{{ statusView()!.profitHasGram | number:'1.2-2' }} Has Gr
                </span>
                <span class="profit-hint">(performans göstergesi)</span>
              </div>
            </div>

            <!-- ─── SECTION 3: Net Performance (Mali Analiz) ─── -->
            @if (statusView()!.peggingCount > 0) {
              <div class="section performance-section">
                <h3 class="section-title">
                  <mat-icon>insights</mat-icon>
                  Mali Performans
                  <span class="section-hint">Tüm bağlama işlemlerinden</span>
                </h3>

                <div class="performance-card" [ngClass]="perfClass()">
                  <div class="perf-main">
                    <div class="perf-icon-wrap">
                      <mat-icon>{{ statusView()!.cumulativePeggingProfitHasGram >= 0 ? 'emoji_events' : 'warning' }}</mat-icon>
                    </div>
                    <div class="perf-body">
                      <span class="perf-label">Toplam Gerçekleşen Kâr</span>
                      <span class="perf-value">
                        {{ statusView()!.cumulativePeggingProfitHasGram >= 0 ? '+' : '' }}{{ statusView()!.cumulativePeggingProfitHasGram | number:'1.4-4' }}
                        <small>Has Gr</small>
                      </span>
                    </div>
                  </div>
                  <div class="perf-meta">
                    <mat-icon>receipt_long</mat-icon>
                    <span>{{ statusView()!.peggingCount }} bağlama işlemi</span>
                  </div>
                </div>
              </div>
            }

          </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .safe-widget {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border-radius: 16px;
      box-shadow: 0 8px 32px rgba(102, 126, 234, 0.3);
    }

    :host ::ng-deep .mat-mdc-card-header {
      padding: 0.75rem 1rem 0.25rem 1rem;
    }
    :host ::ng-deep .mat-mdc-card-content {
      padding: 0.5rem 1rem 0.75rem 1rem !important;
    }

    .widget-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.15rem 0 0;
    }
    .widget-icon { font-size: 2rem; width: 2rem; height: 2rem; color: rgba(255,255,255,.9); }
    .widget-title { margin: 0; font-size: 1.5rem; font-weight: 600; color: white; }
    .widget-subtitle { margin: .25rem 0 0; font-size: .85rem; color: rgba(255,255,255,.7); }
    .widget-live { margin: .35rem 0 0; font-size: .78rem; color: rgba(255,255,255,.85); font-weight: 500; }
    .widget-live--muted { color: rgba(255,255,255,.65); font-style: italic; }

    .sections { display: flex; flex-direction: column; gap: 0.85rem; margin-top: 0.35rem; }

    /* ── Section title ── */
    .section-title {
      display: flex; align-items: center; gap: .5rem;
      margin: 0 0 .5rem; font-size: 0.95rem; font-weight: 600; color: rgba(255,255,255,.95);
      mat-icon { font-size: 1.15rem; width: 1.15rem; height: 1.15rem; }
    }
    .section-hint {
      font-size: .7rem; font-weight: 400; color: rgba(255,255,255,.55);
      margin-left: auto;
    }

    /* ── Metric grid (Physical) ── */
    .metric-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: .5rem;
    }
    .metric-grid--tight {
      grid-template-columns: repeat(3, 1fr);
    }
    .section--equity .metric-grid--tight .metric:last-child { grid-column: auto; }
    .metric {
      background: rgba(255,255,255,.12);
      border-radius: 10px; padding: 0.65rem 0.75rem;
      display: flex; align-items: flex-start; gap: .75rem;
      border: 1px solid rgba(255,255,255,.15);
      transition: background .2s;
    }
    .metric:hover { background: rgba(255,255,255,.18); }
    .metric--primary {}
    .metric:last-child { grid-column: span 2; }

    .metric-icon {
      width: 38px; height: 38px; border-radius: 8px;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
      mat-icon { font-size: 1.25rem; width: 1.25rem; height: 1.25rem; color: white; }
    }
    .metric-icon.gold { background: linear-gradient(135deg,#ffa000,#ffc107); }
    .metric-icon.cash { background: linear-gradient(135deg,#2e7d32,#4caf50); }
    .metric-icon.gap  { background: linear-gradient(135deg,#1565c0,#42a5f5); }
    .metric-icon.debt { background: linear-gradient(135deg,#c62828,#e57373); }

    .metric-body { flex: 1; display: flex; flex-direction: column; gap: .2rem; }
    .metric-label { font-size: .75rem; color: rgba(255,255,255,.7); text-transform: uppercase; letter-spacing: .5px; }
    .metric-value { font-size: 1.4rem; font-weight: 700; color: white; small { font-size: .8rem; font-weight: 500; color: rgba(255,255,255,.7); } }
    .metric-cash-stack {
      display: flex; flex-direction: column; align-items: flex-start; gap: 0.15rem;
      margin-top: 0.15rem;
    }
    .metric-value-line {
      font-size: 1.05rem; font-weight: 700; color: white;
      small { font-size: .68rem; font-weight: 500; color: rgba(255,255,255,.65); margin-left: 0.2rem; }
    }

    .metric-badge {
      display: inline-flex; align-items: center; gap: .25rem;
      font-size: .75rem; font-weight: 600; padding: .2rem .5rem; border-radius: 6px; margin-top: .25rem; width: fit-content;
      mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    }
    .badge--surplus { background: rgba(76,175,80,.35); color: #c8e6c9; }
    .badge--shortage { background: rgba(244,67,54,.35); color: #ffcdd2; }

    .metric--shortage { border-color: rgba(244,67,54,.5); }
    .metric--surplus  { border-color: rgba(76,175,80,.5); }

    .vault-chart-block {
      margin-top: 0.5rem;
      padding: 0.45rem 0.55rem;
      background: rgba(0,0,0,.12);
      border-radius: 8px;
      border: 1px solid rgba(255,255,255,.12);
    }
    .vault-chart-label {
      display: block;
      font-size: 0.68rem;
      color: rgba(255,255,255,.65);
      text-transform: uppercase;
      letter-spacing: 0.4px;
      margin-bottom: 0.35rem;
    }
    .vault-chart-svg {
      width: 100%;
      height: 72px;
      display: block;
    }
    .vault-chart-foot {
      display: flex;
      justify-content: space-between;
      font-size: 0.65rem;
      color: rgba(255,255,255,.55);
      margin-top: 0.25rem;
    }

    .net-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.45rem;
      margin-bottom: 0.45rem;
    }

    /* ── Net position card ── */
    .net-position-card {
      background: rgba(255,255,255,.18);
      border: 2px solid rgba(255,255,255,.25);
      border-radius: 10px; padding: 0.55rem 0.65rem; margin-bottom: 0;
      display: flex; align-items: center; gap: 0.55rem;
    }
    .net-position-card--cash {
      border-color: rgba(76,175,80,.4);
    }
    .net-icon {
      width: 38px; height: 38px; border-radius: 8px;
      background: rgba(255,255,255,.2);
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
      mat-icon { font-size: 1.35rem; width: 1.35rem; height: 1.35rem; color: white; }
    }
    .net-icon--cash {
      background: rgba(76,175,80,.3);
    }
    .net-body { flex: 1; display: flex; flex-direction: column; gap: 0.1rem; min-width: 0; }
    .net-label { font-size: .65rem; color: rgba(255,255,255,.75); text-transform: uppercase; letter-spacing: .5px; }
    .net-value { font-size: 1.35rem; font-weight: 700; color: white; line-height: 1.15; small { font-size: .72rem; color: rgba(255,255,255,.7); } }
    .net-cash-stack {
      display: flex; flex-direction: column; align-items: flex-start; gap: 0.12rem;
      margin-top: 0.2rem;
    }
    .net-value-line {
      font-size: 1.05rem; font-weight: 700; color: white; line-height: 1.2;
      small { font-size: .65rem; color: rgba(255,255,255,.7); margin-left: 0.2rem; }
    }

    /* ── Debt / Receivable grid ── */
    .debt-grid {
      display: grid; grid-template-columns: 1fr 1fr; gap: .4rem;
    }
    .debt-item {
      background: rgba(255,255,255,.08); border-radius: 8px; padding: .5rem .55rem;
      display: flex; align-items: center; gap: .6rem;
    }
    .debt-icon {
      font-size: 1.25rem; width: 1.25rem; height: 1.25rem;
      &.receivable { color: #81c784; }
      &.debt { color: #ef9a9a; }
    }
    .debt-body { flex: 1; display: flex; flex-direction: column; gap: .1rem; }
    .debt-label { font-size: .7rem; color: rgba(255,255,255,.6); text-transform: uppercase; }
    .debt-value { font-size: 1rem; font-weight: 600; color: white; }

    /* ── Profit strip ── */
    .profit-strip {
      display: flex; align-items: center; gap: .5rem;
      margin-top: .45rem; padding: .45rem .55rem; border-radius: 8px;
      background: rgba(255,255,255,.1); font-size: .875rem;
      mat-icon { font-size: 1.25rem; width: 1.25rem; height: 1.25rem; }
    }
    .profit-strip.profit--gain  { background: rgba(76,175,80,.2);  color: #c8e6c9; }
    .profit-strip.profit--loss  { background: rgba(244,67,54,.2); color: #ffcdd2; }
    .profit-label { font-weight: 600; }
    .profit-value { margin-left: auto; font-weight: 700; }
    .profit-hint  { font-size: .7rem; color: rgba(255,255,255,.5); margin-left: .25rem; }

    /* ── Performance section (Mali Analiz) ── */
    .performance-section {
      border-top: 1px solid rgba(255,255,255,.15);
      padding-top: 0.65rem;
    }

    .performance-card {
      border-radius: 10px;
      padding: 1rem;
      border: 1px solid rgba(255,255,255,.2);
    }
    .performance-card.performance--gain {
      background: linear-gradient(135deg, rgba(76,175,80,.25) 0%, rgba(56,142,60,.15) 100%);
      border-color: rgba(76,175,80,.4);
    }
    .performance-card.performance--loss {
      background: linear-gradient(135deg, rgba(244,67,54,.25) 0%, rgba(211,47,47,.15) 100%);
      border-color: rgba(244,67,54,.4);
    }

    .perf-main {
      display: flex; align-items: center; gap: .75rem;
    }
    .perf-icon-wrap {
      width: 44px; height: 44px; border-radius: 10px;
      display: flex; align-items: center; justify-content: center; flex-shrink: 0;
      background: rgba(255,255,255,.15);
      mat-icon { font-size: 1.5rem; width: 1.5rem; height: 1.5rem; color: white; }
    }
    .perf-body { flex: 1; display: flex; flex-direction: column; gap: .2rem; }
    .perf-label {
      font-size: .7rem; color: rgba(255,255,255,.7);
      text-transform: uppercase; letter-spacing: .6px; font-weight: 500;
    }
    .perf-value {
      font-size: 1.5rem; font-weight: 700; color: white;
      small { font-size: .8rem; font-weight: 500; color: rgba(255,255,255,.7); }
    }

    .perf-meta {
      display: flex; align-items: center; gap: .35rem;
      margin-top: .6rem; padding-top: .6rem;
      border-top: 1px solid rgba(255,255,255,.1);
      font-size: .75rem; color: rgba(255,255,255,.6);
      mat-icon { font-size: 1rem; width: 1rem; height: 1rem; }
    }

    .loading-state, .error-state {
      text-align: center; padding: 2rem; color: rgba(255,255,255,.8);
    }

    @media (max-width: 768px) {
      .metric-grid { grid-template-columns: 1fr; }
      .metric-grid--tight { grid-template-columns: 1fr; }
      .metric:last-child { grid-column: span 1; }
      .debt-grid { grid-template-columns: 1fr; }
      .net-row { grid-template-columns: 1fr; }
    }
  `]
})
export class SafeStatusWidgetComponent {
  /** Panel özeti — her zaman dolu (üst bileşen varsayılan nesne verir); async yükleme yok. */
  dashboard = input.required<DashboardSummary>();

  statusView = computed(() => safeStatusFromDashboard(this.dashboard()));

  /** SVG polyline points for cumulative physical vault history (normalized 0–100 × 0–36). */
  vaultChartPolyline = computed((): string | null => {
    const hist = this.dashboard().physicalVaultHistory ?? [];
    if (hist.length < 2) return null;
    const vals = hist.map((h) => h.cumulativeHasGram);
    const min = Math.min(...vals);
    const max = Math.max(...vals);
    const range = Math.max(max - min, 1e-9);
    const n = vals.length - 1;
    return vals
      .map((v, i) => {
        const x = n === 0 ? 0 : (i / n) * 100;
        const y = 34 - ((v - min) / range) * 30;
        return `${x.toFixed(2)},${y.toFixed(2)}`;
      })
      .join(' ');
  });

  vaultChartStartLabel = computed(() => {
    const hist = this.dashboard().physicalVaultHistory ?? [];
    if (hist.length === 0) return '';
    const h = hist[0];
    return `${new Date(h.at).toLocaleDateString('tr-TR')} → ${h.cumulativeHasGram.toFixed(2)} Has`;
  });

  vaultChartEndLabel = computed(() => {
    const hist = this.dashboard().physicalVaultHistory ?? [];
    if (hist.length === 0) return '';
    const h = hist[hist.length - 1];
    return `${new Date(h.at).toLocaleDateString('tr-TR')} → ${h.cumulativeHasGram.toFixed(2)} Has`;
  });

  liveMid = computed(() => this.dashboard().liveHasTryPerGramMid ?? null);
  netGoldTl = computed(() => this.dashboard().netGoldPositionTlApprox ?? null);
  ratesFromManual = computed(() => this.dashboard().ratesFromManualOverride === true);
  ratesFromDefault = computed(() => this.dashboard().ratesFromDefaultFallback === true);
  showHasRateNa = computed(() => !this.dashboard().ratesAvailable && this.liveMid() == null);

  gapClass(): string {
    const gap = this.statusView().goldGapOrSurplus ?? 0;
    if (gap > 0.001) return 'metric--surplus';
    if (gap < -0.001) return 'metric--shortage';
    return '';
  }

  gapIcon(): string {
    const gap = this.statusView().goldGapOrSurplus ?? 0;
    if (gap > 0.001) return 'trending_up';
    if (gap < -0.001) return 'trending_down';
    return 'horizontal_rule';
  }

  profitClass(): string {
    const p = this.statusView().profitHasGram ?? 0;
    return p >= 0 ? 'profit--gain' : 'profit--loss';
  }

  perfClass(): string {
    const v = this.statusView().cumulativePeggingProfitHasGram ?? 0;
    return v >= 0 ? 'performance--gain' : 'performance--loss';
  }
}
