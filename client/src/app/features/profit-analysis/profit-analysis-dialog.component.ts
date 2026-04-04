import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatTableModule } from '@angular/material/table';
import { MatSliderModule } from '@angular/material/slider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ProfitAnalysisService, PeggingSimulation } from '../../core/services/profit-analysis.service';
import { GoldPriceService } from '../../core/services/gold-price.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';
import { LinkingService, FifoLinkingSimulation } from '../../core/services/linking.service';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-profit-analysis-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatTableModule,
    MatSliderModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatSnackBarModule,
    RouterLink
  ],
  template: `
    <div class="dialog-container">
      <!-- Header (Fixed) -->
      <div class="dialog-header">
        <div class="header-content">
          <mat-icon class="header-icon">attach_money</mat-icon>
          <div class="header-text">
            <h1>Nakit Bağlama ve Dönemsel Analiz</h1>
            <p>Seçili dönemdeki nakit hareketlerinizi has altına çevirin</p>
          </div>
        </div>
        <button mat-icon-button mat-dialog-close class="close-btn">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <!-- Body (Scrollable) -->
      <mat-dialog-content class="dialog-body">
        <div class="grid-layout">
          <!-- LEFT COLUMN: Form and Transactions -->
          <div class="left-column">
            <form [formGroup]="peggingForm">
              <!-- Date Selection Card -->
              <div class="card">
                <div class="card-header">
                  <mat-icon>calendar_month</mat-icon>
                  <h3>Dönem Seçimi</h3>
                </div>
                <div class="card-content">
                  <div class="form-row">
                    <mat-form-field appearance="outline">
                      <mat-label>Başlangıç Tarihi</mat-label>
                      <input matInput [matDatepicker]="startPicker" formControlName="periodStart">
                      <mat-datepicker-toggle matIconSuffix [for]="startPicker"></mat-datepicker-toggle>
                      <mat-datepicker #startPicker></mat-datepicker>
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Bitiş Tarihi</mat-label>
                      <input matInput [matDatepicker]="endPicker" formControlName="periodEnd">
                      <mat-datepicker-toggle matIconSuffix [for]="endPicker"></mat-datepicker-toggle>
                      <mat-datepicker #endPicker></mat-datepicker>
                    </mat-form-field>
                  </div>
                </div>
              </div>

              <!-- Price Input Card -->
              <div class="card">
                <div class="card-header">
                  <mat-icon>sell</mat-icon>
                  <h3>Has Bağlama Fiyatı</h3>
                </div>
                <div class="card-content">
                  <mat-form-field appearance="outline" class="full-width">
                    <mat-label>Fiyat (TL/gram)</mat-label>
                    <input matInput type="number" formControlName="goldPrice" min="1" step="50">
                    <span matTextPrefix>₺&nbsp;</span>
                    <mat-hint>Nakitinizi bu fiyattan has'a çevireceksiniz</mat-hint>
                  </mat-form-field>

                  <div class="slider-container">
                    <div class="slider-label">Simülasyon Fiyatı (Hibrit + FIFO)</div>
                    <mat-slider min="5000" max="10000" step="50" discrete [displayWith]="formatSliderLabel" class="full-width">
                      <input matSliderThumb [value]="simulationPrice()" (valueChange)="onSimulationPriceChange($event)">
                    </mat-slider>
                    <div class="slider-value">{{ simulationPrice() | number:'1.0-0' }} ₺/gram</div>
                  </div>

                  <div class="form-row hybrid-row">
                    <mat-form-field appearance="outline">
                      <mat-label>Kasadan bağlanacak TL (opsiyonel)</mat-label>
                      <input matInput type="number" formControlName="pegCashFromSafe" min="0" step="0.01">
                      <mat-hint>Boşsa dönem nakdi. Kasadaki nakitle sınırlı.</mat-hint>
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Hedef has gr (opsiyonel)</mat-label>
                      <input matInput type="number" formControlName="pegHasGram" min="0" step="0.0001">
                      <mat-hint>TL alanı ile aynı anda kullanmayın.</mat-hint>
                    </mat-form-field>
                  </div>
                </div>
              </div>
            </form>

            <!-- FIFO Nakit Bağlama -->
            <div class="card fifo-card">
              <div class="card-header">
                <mat-icon>account_tree</mat-icon>
                <h3>FIFO Nakit Bağlama</h3>
              </div>
              <div class="card-content">
                <div class="fifo-open">
                  <div class="fifo-open-row">
                    <span class="fifo-open-label">Kapanmamış FIFO (hibrit sonrası tahmini)</span>
                    <span class="fifo-open-value">{{ openPositionGram() | number:'1.4-4' }} gr</span>
                  </div>
                  <span class="fifo-open-sub">Dönem açığı (bağlama öncesi): {{ openPositionBeforeHybrid() | number:'1.4-4' }} gr</span>
                </div>
                <div class="form-row fifo-row" [formGroup]="fifoForm">
                  <mat-form-field appearance="outline">
                    <mat-label>Bağlanacak Has (gr)</mat-label>
                    <input matInput type="number" formControlName="amount" min="0.0001" step="0.0001">
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Has fiyatı (TL/gr)</mat-label>
                    <input matInput type="number" formControlName="price" min="1" step="1">
                  </mat-form-field>
                </div>
                <div class="fifo-actions">
                  <span class="fifo-hint">Dönem, fiyat veya bağlanacak has değiştikçe tahmini kâr güncellenir.</span>
                  <a mat-button routerLink="/linking-history" class="fifo-link">Bağlantı Geçmişi</a>
                </div>
                @if (fifoSim(); as fs) {
                  <div class="fifo-sim" [class.fifo-sim--bad]="!fs.sufficientOpenPosition">
                    <div>Tahmini kâr/zarar (TL): <strong>{{ fs.estimatedProfitTl | number:'1.2-2' }}</strong></div>
                    <div>Yaklaşık Has karşılığı: <strong>{{ fifoProfitHasGram(fs) | number:'1.2-2' }}</strong> gr</div>
                    <div>Açık pozisyon yeterli: <strong>{{ fs.sufficientOpenPosition ? 'Evet' : 'Hayır' }}</strong></div>
                  </div>
                }
                <button mat-raised-button color="accent" type="button" class="fifo-confirm"
                        (click)="onConfirmFifo()" [disabled]="fifoSaving() || !canSubmitFifo()">
                  @if (fifoSaving()) { <mat-spinner diameter="20"></mat-spinner> }
                  @else { <mat-icon>check</mat-icon> }
                  FIFO ile Bağla
                </button>
              </div>
            </div>

            <!-- Transactions Card -->
            <div class="card">
              <div class="card-header">
                <mat-icon>receipt_long</mat-icon>
                <h3>Dönem İçi İşlemler</h3>
              </div>
              <div class="card-content">
                @if (periodSummary()) {
                  <div class="stats-grid">
                    <div class="stat-box stat-box--purchase">
                      <div class="stat-label">Toplam Alış</div>
                      <div class="stat-value">{{ periodSummary()!.totalPurchasesCash | number:'1.2-2' }} ₺</div>
                    </div>
                    <div class="stat-box stat-box--sale">
                      <div class="stat-label">Toplam Satış</div>
                      <div class="stat-value">{{ periodSummary()!.totalSalesCash | number:'1.2-2' }} ₺</div>
                    </div>
                    <div class="stat-box stat-box--net">
                      <div class="stat-label">Net Nakit</div>
                      <div class="stat-value">{{ periodSummary()!.netCashChange | number:'1.2-2' }} ₺</div>
                    </div>
                  </div>

                  <div class="transactions-scroll">
                    @for (tx of periodSummary()!.transactions; track tx.id) {
                      <div class="tx-item" [class.tx-item--sale]="tx.direction === 'Satış'" [class.tx-item--purchase]="tx.direction === 'Alış'">
                        <div class="tx-icon-wrapper">
                          <mat-icon>{{ tx.direction === 'Satış' ? 'arrow_upward' : 'arrow_downward' }}</mat-icon>
                        </div>
                        <div class="tx-body">
                          <div class="tx-header">
                            <span class="tx-type">{{ tx.direction }}</span>
                            <span class="tx-date">{{ tx.date | date:'dd.MM.yyyy' }}</span>
                          </div>
                          <div class="tx-info">
                            <span class="tx-amount-label">{{ tx.hasGram | number:'1.2-2' }} Has Gr</span>
                            @if (tx.customerName) {
                              <span class="tx-customer">{{ tx.customerName }}</span>
                            }
                          </div>
                        </div>
                        <div class="tx-cash" [class.positive]="tx.cashImpact > 0" [class.negative]="tx.cashImpact < 0">
                          {{ tx.cashImpact > 0 ? '+' : '' }}{{ tx.cashImpact | number:'1.2-2' }} ₺
                        </div>
                      </div>
                    }
                  </div>
                } @else if (loadingPeriod()) {
                  <div class="state-message">
                    <mat-spinner diameter="40"></mat-spinner>
                    <span>İşlemler yükleniyor...</span>
                  </div>
                } @else {
                  <div class="state-message">
                    <mat-icon>inbox</mat-icon>
                    <p>Bu dönemde işlem bulunamadı</p>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- RIGHT COLUMN: Summary Panel (Sticky) -->
          <div class="right-column">
            <div class="summary-panel">
              @if (simulationResult(); as result) {
                <!-- Period Net Profit Card (Main) -->
                <div class="profit-card" [class.profit-card--gain]="result.netProfitHasGram >= 0" [class.profit-card--loss]="result.netProfitHasGram < 0">
                  <div class="profit-header">
                    <mat-icon class="profit-icon">{{ result.netProfitHasGram >= 0 ? 'trending_up' : 'trending_down' }}</mat-icon>
                    <span class="profit-label">Dönem net (kasadaki nakitle)</span>
                  </div>
                  <div class="profit-amount">
                    {{ result.netProfitHasGram >= 0 ? '+' : '' }}{{ result.netProfitHasGram | number:'1.2-2' }}
                  </div>
                  <div class="profit-unit">Has Gr · tüm kasa fiyata göre</div>
                  <div class="profit-tl">
                    {{ result.netProfitTL >= 0 ? '+' : '' }}{{ result.netProfitTL | number:'1.2-2' }} ₺
                  </div>
                  <div class="profit-split">
                    <div class="profit-split-row" [class.profit-split-row--gain]="result.realizedNetProfitHasGram >= 0" [class.profit-split-row--loss]="result.realizedNetProfitHasGram < 0">
                      <span class="profit-split-label">Mühürlenen (bu bağlama)</span>
                      <span class="profit-split-val">{{ result.realizedNetProfitHasGram >= 0 ? '+' : '' }}{{ result.realizedNetProfitHasGram | number:'1.2-2' }} Has · {{ result.realizedNetProfitTl >= 0 ? '+' : '' }}{{ result.realizedNetProfitTl | number:'1.2-2' }} ₺</span>
                    </div>
                    <div class="profit-split-row" [class.profit-split-row--gain]="result.pendingEstimatedNetHasGram >= 0" [class.profit-split-row--loss]="result.pendingEstimatedNetHasGram < 0">
                      <span class="profit-split-label">Bekleyen (kalan {{ result.remainingSafeCashTl | number:'1.0-0' }} ₺)</span>
                      <span class="profit-split-val">{{ result.pendingEstimatedNetHasGram >= 0 ? '+' : '' }}{{ result.pendingEstimatedNetHasGram | number:'1.2-2' }} Has · {{ result.pendingEstimatedNetTl >= 0 ? '+' : '' }}{{ result.pendingEstimatedNetTl | number:'1.2-2' }} ₺</span>
                    </div>
                    @if (result.unbackedGoldDebtHasGram > 0.0001) {
                      <div class="profit-split-row profit-split-row--warn">
                        <span class="profit-split-label">Nakitle karşılanmayan işlem açığı</span>
                        <span class="profit-split-val">{{ result.unbackedGoldDebtHasGram | number:'1.2-2' }} Has Gr</span>
                      </div>
                    }
                  </div>
                </div>

                <!-- Profit Breakdown -->
                <div class="card details-card">
                  <div class="detail-row">
                    <span class="detail-label">Dönem Satış</span>
                    <span class="detail-value detail-value--sale">{{ result.totalSalesHasGram | number:'1.2-2' }} Has Gr</span>
                  </div>
                  <div class="detail-row">
                    <span class="detail-label">Dönem Alış</span>
                    <span class="detail-value detail-value--purchase">{{ result.totalPurchasesHasGram | number:'1.2-2' }} Has Gr</span>
                  </div>
                  <div class="detail-row detail-row--highlight">
                    <span class="detail-label">İşlem Kârı (Satış - Alış)</span>
                    <span class="detail-value" [class.positive]="result.transactionProfitHasGram >= 0" [class.negative]="result.transactionProfitHasGram < 0">
                      {{ result.transactionProfitHasGram >= 0 ? '+' : '' }}{{ result.transactionProfitHasGram | number:'1.2-2' }} Has Gr
                    </span>
                  </div>
                  <div class="detail-row detail-row--highlight">
                    <span class="detail-label">Nakit Bağlama Kârı</span>
                    <span class="detail-value detail-value--cash">{{ result.cashEquivalentHasGram >= 0 ? '+' : '' }}{{ result.cashEquivalentHasGram | number:'1.2-2' }} Has Gr</span>
                  </div>
                </div>

                <!-- Financial Details -->
                <div class="card">
                  <div class="card-header">
                    <mat-icon>account_balance</mat-icon>
                    <h3>Finansal Detaylar</h3>
                  </div>
                  <div class="card-content">
                    <div class="info-row">
                      <div class="info-icon info-icon--cash">
                        <mat-icon>account_balance_wallet</mat-icon>
                      </div>
                      <div class="info-content">
                        <div class="info-label">Bağlamada kullanılacak nakit</div>
                        <div class="info-value">{{ result.periodCashBalance | number:'1.2-2' }} ₺</div>
                        <div class="info-hint">Kasada: {{ result.safeCashBalance | number:'1.2-2' }} ₺ · Bağlama sonrası kasada: {{ result.remainingSafeCashTl | number:'1.2-2' }} ₺ · Defter dönem nakdi: {{ result.ledgerPeriodCashBalance | number:'1.2-2' }} ₺</div>
                      </div>
                    </div>

                    <div class="info-row">
                      <div class="info-icon info-icon--gold">
                        <mat-icon>stars</mat-icon>
                      </div>
                      <div class="info-content">
                        <div class="info-label">Nakit Karşılığı Has</div>
                        <div class="info-value">{{ result.cashEquivalentHasGram | number:'1.2-2' }} Gr</div>
                        <div class="info-hint">{{ result.periodCashBalance | number:'1.0-0' }} ÷ {{ simulationPrice() | number:'1.0-0' }}</div>
                      </div>
                    </div>

                    <div class="info-row">
                      <div class="info-icon info-icon--safe">
                        <mat-icon>account_balance</mat-icon>
                      </div>
                      <div class="info-content">
                        <div class="info-label">Kasadaki Fiziksel Altın</div>
                        <div class="info-value">{{ result.goldBalanceInSafe | number:'1.2-2' }} Has Gr</div>
                        <div class="info-hint">Kâr hesabına dahil değil</div>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Action Buttons -->
                <div class="actions">
                  <button mat-raised-button color="primary" 
                          (click)="onConfirmPegging()" 
                          [disabled]="peggingForm.invalid || saving()"
                          class="btn-primary"
                          type="button">
                    @if (saving()) {
                      <mat-spinner diameter="20"></mat-spinner>
                    } @else {
                      <mat-icon>check_circle</mat-icon>
                    }
                    Bağlamayı Onayla ve Kaydet
                  </button>
                  <button mat-stroked-button mat-dialog-close type="button" class="btn-secondary">
                    İptal
                  </button>
                </div>
              } @else if (simulating()) {
                <div class="state-message">
                  <mat-spinner diameter="50"></mat-spinner>
                  <span>Hesaplanıyor...</span>
                </div>
              } @else {
                <div class="state-message">
                  <mat-icon>insights</mat-icon>
                  <p>Tarih ve fiyat bilgisi girildikçe<br>sonuçlar burada görünecek</p>
                </div>
              }
            </div>
          </div>
        </div>
      </mat-dialog-content>
    </div>
  `,
  styles: [`
    /* ========== MATERIAL DIALOG OVERRIDE ========== */
    :host ::ng-deep .mat-mdc-dialog-container .mdc-dialog__surface {
      overflow: hidden !important;
      display: flex !important;
      flex-direction: column !important;
      max-height: 90vh !important;
    }

    :host ::ng-deep .mat-mdc-dialog-container .mat-mdc-dialog-content {
      padding: 0 !important;
      margin: 0 !important;
      max-height: none !important;
    }

    /* ========== CONTAINER ========== */
    .dialog-container {
      display: flex;
      flex-direction: column;
      height: 100%;
      max-height: 90vh;
      width: 100%;
      overflow: hidden;
    }

    /* ========== HEADER (FIXED) ========== */
    .dialog-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1.25rem 1.75rem;
      background: linear-gradient(135deg, #7b2cbf 0%, #9d4edd 100%);
      color: white;
      flex-shrink: 0;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
      z-index: 10;
    }

    .header-content {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .header-icon {
      font-size: 2.25rem;
      width: 2.25rem;
      height: 2.25rem;
    }

    .header-text h1 {
      margin: 0;
      font-size: 1.375rem;
      font-weight: 600;
      line-height: 1.2;
    }

    .header-text p {
      margin: 0.25rem 0 0 0;
      font-size: 0.813rem;
      opacity: 0.9;
    }

    .close-btn {
      color: white;
      
      &:hover {
        background: rgba(255, 255, 255, 0.1);
      }
    }

    /* ========== BODY (SCROLLABLE) ========== */
    ::ng-deep .dialog-body {
      padding: 0 !important;
      margin: 0 !important;
      overflow-y: auto !important;
      overflow-x: hidden !important;
      flex: 1;
      background: #f5f7fa;
      min-height: 0;
    }

    /* ========== GRID LAYOUT ========== */
    .grid-layout {
      display: grid;
      grid-template-columns: 1fr 400px;
      gap: 0;
      min-height: 100%;
    }

    /* ========== LEFT COLUMN ========== */
    .left-column {
      padding: 1.25rem;
      background: #f5f7fa;
    }

    /* ========== RIGHT COLUMN (STICKY) ========== */
    .right-column {
      background: #ffffff;
      border-left: 1px solid #e5e7eb;
      padding: 1.25rem;
    }

    .summary-panel {
      position: sticky;
      top: 20px;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    /* ========== CARD COMPONENT ========== */
    .card {
      background: white;
      border-radius: 10px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      margin-bottom: 1rem;
      overflow: hidden;
    }

    .card-header {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      padding: 0.875rem 1rem;
      background: #fafbfc;
      border-bottom: 1px solid #e5e7eb;

      mat-icon {
        color: #7b2cbf;
        font-size: 1.375rem;
        width: 1.375rem;
        height: 1.375rem;
      }

      h3 {
        margin: 0;
        font-size: 0.938rem;
        font-weight: 600;
        color: #1f2937;
      }
    }

    .card-content {
      padding: 1rem;
    }

    /* ========== FORM ELEMENTS ========== */
    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }

    .full-width {
      width: 100%;
    }

    mat-form-field {
      width: 100%;
    }

    /* ========== CARD COMPONENT ========== */
    .card {
      background: white;
      border-radius: 10px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      margin-bottom: 1rem;
      overflow: hidden;
    }

    .fifo-card {
      border: 1px solid #e1bee7;
      background: linear-gradient(180deg, #faf5ff 0%, #fff 100%);
    }

    .fifo-open {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      margin-bottom: 0.75rem;
    }

    .fifo-open-row {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .fifo-open-sub {
      font-size: 0.75rem;
      color: #6b7280;
    }

    .fifo-open-label {
      font-size: 0.85rem;
      color: #4b5563;
    }

    .fifo-open-value {
      font-size: 1.2rem;
      font-weight: 600;
      color: #6a1b9a;
    }

    .fifo-row mat-form-field {
      flex: 1;
      min-width: 120px;
    }

    .fifo-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin: 0.5rem 0;
    }

    .fifo-sim {
      margin: 0.75rem 0;
      padding: 0.75rem;
      border-radius: 8px;
      background: #f3e5f5;
      font-size: 0.9rem;
      color: #374151;
    }

    .fifo-sim--bad {
      border: 1px solid #e57373;
      background: #ffebee;
    }

    .fifo-confirm {
      width: 100%;
      margin-top: 0.5rem;
    }

    .fifo-link {
      font-size: 0.85rem;
    }

    .fifo-hint {
      font-size: 0.8rem;
      color: #6b7280;
      flex: 1;
      min-width: 200px;
    }

    .hybrid-row {
      margin-top: 0.75rem;
    }

    .card-header {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      padding: 0.75rem 1rem;
      background: #fafbfc;
      border-bottom: 1px solid #e5e7eb;

      mat-icon {
        color: #7b2cbf;
        font-size: 1.25rem;
        width: 1.25rem;
        height: 1.25rem;
      }

      h3 {
        margin: 0;
        font-size: 0.938rem;
        font-weight: 600;
        color: #1f2937;
      }
    }

    .card-content {
      padding: 1rem;
    }

    /* ========== FORM ELEMENTS ========== */
    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.875rem;
    }

    .full-width {
      width: 100%;
    }

    mat-form-field {
      width: 100%;
    }

    /* ========== SLIDER ========== */
    .slider-container {
      margin-top: 1rem;
      padding: 0.875rem;
      background: #f9fafb;
      border-radius: 8px;
      border: 1px solid #e5e7eb;
    }

    .slider-label {
      display: block;
      font-size: 0.813rem;
      font-weight: 600;
      color: #4b5563;
      margin-bottom: 0.625rem;
    }

    .slider-value {
      margin-top: 0.5rem;
      text-align: center;
      font-size: 1rem;
      font-weight: 700;
      color: #7b2cbf;
    }

    /* ========== STATS GRID ========== */
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .stat-box {
      padding: 0.875rem;
      border-radius: 8px;
      text-align: center;

      &--purchase {
        background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
        border: 1px solid #fbbf24;
      }

      &--sale {
        background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%);
        border: 1px solid #10b981;
      }

      &--net {
        background: linear-gradient(135deg, #e0e7ff 0%, #c7d2fe 100%);
        border: 1px solid #7b2cbf;
      }
    }

    .stat-label {
      display: block;
      font-size: 0.75rem;
      font-weight: 500;
      color: #4b5563;
      margin-bottom: 0.375rem;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .stat-value {
      display: block;
      font-size: 1rem;
      font-weight: 700;
      color: #1f2937;
    }

    /* ========== TRANSACTIONS SCROLL ========== */
    .transactions-scroll {
      max-height: 240px;
      overflow-y: auto;
      margin-top: 0.875rem;
      padding-right: 0.5rem;

      &::-webkit-scrollbar {
        width: 6px;
      }

      &::-webkit-scrollbar-track {
        background: #f1f1f1;
        border-radius: 10px;
      }

      &::-webkit-scrollbar-thumb {
        background: #cbd5e1;
        border-radius: 10px;

        &:hover {
          background: #94a3b8;
        }
      }
    }

    .tx-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem;
      margin-bottom: 0.5rem;
      background: #fafafa;
      border-radius: 8px;
      border-left: 3px solid transparent;
      transition: all 0.2s ease;

      &:hover {
        background: #f3f4f6;
        transform: translateX(2px);
      }

      &--sale {
        border-left-color: #10b981;
      }

      &--purchase {
        border-left-color: #f59e0b;
      }
    }

    .tx-icon-wrapper {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;

      mat-icon {
        font-size: 1.125rem;
        width: 1.125rem;
        height: 1.125rem;
      }
    }

    .tx-item--sale .tx-icon-wrapper {
      background: #d1fae5;
      color: #10b981;
    }

    .tx-item--purchase .tx-icon-wrapper {
      background: #fed7aa;
      color: #f59e0b;
    }

    .tx-body {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .tx-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }

    .tx-type {
      font-weight: 600;
      font-size: 0.813rem;
      color: #1f2937;
    }

    .tx-date {
      font-size: 0.75rem;
      color: #6b7280;
    }

    .tx-info {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.75rem;
    }

    .tx-amount-label {
      color: #7b2cbf;
      font-weight: 600;
    }

    .tx-customer {
      color: #6b7280;
    }

    .tx-cash {
      font-weight: 700;
      font-size: 0.938rem;
      flex-shrink: 0;

      &.positive {
        color: #10b981;
      }

      &.negative {
        color: #ef4444;
      }
    }

    /* ========== PROFIT CARD (MAIN HIGHLIGHT) ========== */
    .profit-card {
      position: relative;
      padding: 1.5rem;
      border-radius: 12px;
      text-align: center;
      overflow: hidden;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);

      &--gain {
        background: linear-gradient(135deg, #10b981 0%, #059669 100%);
        color: white;
      }

      &--loss {
        background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
        color: white;
      }
    }

    .profit-header {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
    }

    .profit-icon {
      font-size: 1.75rem;
      width: 1.75rem;
      height: 1.75rem;
      opacity: 0.9;
    }

    .profit-label {
      font-size: 0.813rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 1px;
      opacity: 0.95;
    }

    .profit-amount {
      font-size: 2.5rem;
      font-weight: 700;
      line-height: 1;
      margin-bottom: 0.25rem;
    }

    .profit-unit {
      font-size: 1rem;
      font-weight: 500;
      opacity: 0.9;
    }

    .profit-tl {
      margin-top: 0.375rem;
      font-size: 0.875rem;
      font-weight: 500;
      opacity: 0.85;
    }

    .profit-split {
      margin-top: 1rem;
      padding-top: 1rem;
      border-top: 1px solid rgba(255, 255, 255, 0.35);
      text-align: left;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .profit-split-row {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      font-size: 0.78rem;
      opacity: 0.95;
    }

    .profit-split-row--gain .profit-split-val {
      color: rgba(255, 255, 255, 0.98);
    }

    .profit-split-row--loss .profit-split-val {
      color: #fecaca;
    }

    .profit-split-row--warn .profit-split-val {
      color: #fef08a;
    }

    .profit-split-label {
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      opacity: 0.85;
      font-size: 0.65rem;
    }

    .profit-split-val {
      font-weight: 600;
      font-size: 0.85rem;
    }

    /* ========== DETAILS CARD ========== */
    .details-card {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      margin-bottom: 0;
    }

    .detail-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1rem;

      &:not(:last-child) {
        border-bottom: 1px solid #e5e7eb;
      }
    }

    .detail-label {
      font-size: 0.813rem;
      color: #6b7280;
      font-weight: 500;
    }

    .detail-value {
      font-size: 0.938rem;
      font-weight: 700;
      color: #1f2937;

      &--sale {
        color: #10b981;
      }

      &--purchase {
        color: #f59e0b;
      }

      &--cash {
        color: #3b82f6;
      }

      &.positive {
        color: #10b981;
      }

      &.negative {
        color: #ef4444;
      }
    }

    .detail-row--highlight {
      background: #f9fafb;
      font-weight: 600;
    }

    /* ========== INFO ROWS ========== */
    .info-row {
      display: flex;
      align-items: flex-start;
      gap: 0.875rem;
      padding: 0.75rem 0;

      &:not(:last-child) {
        border-bottom: 1px solid #f3f4f6;
      }
    }

    .info-icon {
      width: 40px;
      height: 40px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;

      mat-icon {
        font-size: 1.375rem;
        width: 1.375rem;
        height: 1.375rem;
        color: white;
      }

      &--cash {
        background: linear-gradient(135deg, #06b6d4 0%, #0891b2 100%);
      }

      &--gold {
        background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
      }

      &--safe {
        background: linear-gradient(135deg, #8b5cf6 0%, #7c3aed 100%);
      }
    }

    .info-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }

    .info-label {
      font-size: 0.75rem;
      color: #6b7280;
      font-weight: 500;
    }

    .info-value {
      font-size: 1.125rem;
      font-weight: 700;
      color: #1f2937;
    }

    .info-hint {
      font-size: 0.688rem;
      color: #9ca3af;
      font-style: italic;
    }

    /* ========== ACTIONS ========== */
    .actions {
      display: flex;
      flex-direction: column;
      gap: 0.625rem;
    }

    .btn-primary {
      width: 100%;
      height: 44px;
      font-size: 0.938rem;
      font-weight: 600;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      box-shadow: 0 2px 6px rgba(123, 44, 191, 0.3);

      mat-icon {
        font-size: 1.125rem;
        width: 1.125rem;
        height: 1.125rem;
      }
    }

    .btn-secondary {
      width: 100%;
      height: 40px;
      font-weight: 500;
    }

    /* ========== STATE MESSAGES ========== */
    .state-message {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 2.5rem 1rem;
      gap: 0.875rem;
      color: #9ca3af;
      text-align: center;

      mat-icon {
        font-size: 3.5rem;
        width: 3.5rem;
        height: 3.5rem;
        opacity: 0.4;
      }

      p {
        margin: 0;
        font-size: 0.875rem;
        line-height: 1.5;
      }
    }

    /* ========== RESPONSIVE ========== */
    @media (max-width: 1200px) {
      .grid-layout {
        grid-template-columns: 1fr;
      }

      .right-column {
        border-left: none;
        border-top: 1px solid #e5e7eb;
      }

      .summary-panel {
        position: relative;
        top: 0;
      }

      .dialog-header {
        padding: 1rem 1.25rem;
      }

      .header-text h1 {
        font-size: 1.25rem;
      }

      .header-text p {
        font-size: 0.75rem;
      }

      .left-column,
      .right-column {
        padding: 1rem;
      }
    }

    @media (max-width: 768px) {
      .form-row {
        grid-template-columns: 1fr;
      }

      .stats-grid {
        grid-template-columns: 1fr;
      }

      .profit-amount {
        font-size: 2rem;
      }

      .dialog-container {
        height: 95vh;
        max-height: 95vh;
      }
    }
  `]
})
export class ProfitAnalysisDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly profitService = inject(ProfitAnalysisService);
  private readonly goldPriceService = inject(GoldPriceService);
  private readonly dialogRef = inject(MatDialogRef<ProfitAnalysisDialogComponent>);
  private readonly snackBar = inject(MatSnackBar);
  private readonly refreshService = inject(DashboardRefreshService);
  private readonly linkingService = inject(LinkingService);

  peggingForm = this.fb.group({
    periodStart: [new Date(Date.now() - 7 * 24 * 60 * 60 * 1000), Validators.required],
    periodEnd: [new Date(), Validators.required],
    goldPrice: [7000, [Validators.required, Validators.min(1)]],
    pegCashFromSafe: [null as number | null],
    pegHasGram: [null as number | null],
    notes: ['']
  });

  fifoForm = this.fb.group({
    amount: [null as number | null],
    price: [7000, [Validators.required, Validators.min(1)]],
  });

  openPositionGram = signal(0);
  openPositionBeforeHybrid = signal(0);
  fifoSim = signal<FifoLinkingSimulation | null>(null);
  fifoSaving = signal(false);

  simulationPrice = signal(7000);
  simulationResult = signal<PeggingSimulation | null>(null);
  simulating = signal(false);
  saving = signal(false);
  
  periodSummary = signal<any>(null);
  loadingPeriod = signal(false);

  formatSliderLabel = (value: number) => `${value.toLocaleString('tr-TR')} TL`;

  ngOnInit(): void {
    this.loadGoldPrice();

    this.loadPeriodSummary();
    this.runUnifiedSimulation();

    this.peggingForm.get('goldPrice')?.valueChanges.subscribe((v) => {
      if (v != null && v > 0) {
        this.simulationPrice.set(v);
        this.fifoForm.patchValue({ price: v }, { emitEvent: false });
      }
    });

    this.peggingForm.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged())
      .subscribe(() => {
        this.runUnifiedSimulation();
        this.loadPeriodSummary();
      });

    this.fifoForm.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged())
      .subscribe(() => this.runUnifiedSimulation());
  }

  loadGoldPrice(): void {
    this.goldPriceService.getCurrentPrice().subscribe({
      next: (priceData) => {
        const price = priceData.selling;
        this.peggingForm.patchValue({ goldPrice: price }, { emitEvent: false });
        this.fifoForm.patchValue({ price }, { emitEvent: false });
        this.simulationPrice.set(price);
        this.runUnifiedSimulation();
      },
      error: () => {
        this.peggingForm.patchValue({ goldPrice: 7000 }, { emitEvent: false });
        this.fifoForm.patchValue({ price: 7000 }, { emitEvent: false });
        this.simulationPrice.set(7000);
        this.runUnifiedSimulation();
      }
    });
  }

  onSimulationPriceChange(value: number): void {
    this.simulationPrice.set(value);
    this.peggingForm.patchValue({ goldPrice: value }, { emitEvent: false });
    this.fifoForm.patchValue({ price: value }, { emitEvent: false });
    this.runUnifiedSimulation();
  }

  runUnifiedSimulation(): void {
    const start = this.peggingForm.value.periodStart;
    const end = this.peggingForm.value.periodEnd;
    if (!start || !end) return;

    const price = this.simulationPrice();
    const pf = this.peggingForm.value;
    const ff = this.fifoForm.getRawValue();
    const fifoTarget =
      ff.amount != null && ff.amount > 0 ? ff.amount : null;

    this.simulating.set(true);
    this.profitService
      .simulateUnified({
        periodStart: start.toISOString(),
        periodEnd: end.toISOString(),
        goldPricePerGram: price,
        pegCashFromSafe: pf.pegCashFromSafe ?? undefined,
        pegHasGram: pf.pegHasGram ?? undefined,
        fifoTargetAmountGram: fifoTarget ?? undefined,
      })
      .subscribe({
        next: (r) => {
          this.simulationResult.set(r.hybrid);
          this.openPositionBeforeHybrid.set(r.openHasPositionInPeriodGram);
          this.openPositionGram.set(r.estimatedOpenHasAfterHybridGram ?? r.openHasPositionInPeriodGram);
          this.fifoSim.set(r.fifo);
          this.simulating.set(false);
        },
        error: (err) => {
          console.error('Unified simulation error:', err);
          this.simulating.set(false);
        },
      });
  }

  fifoProfitHasGram(fs: FifoLinkingSimulation): number {
    const p = this.fifoForm.getRawValue().price;
    if (!p || p <= 0) return 0;
    return fs.estimatedProfitTl / p;
  }

  canSubmitFifo(): boolean {
    const ff = this.fifoForm.getRawValue();
    const p = ff.price;
    return ff.amount != null && ff.amount > 0 && p != null && p > 0;
  }

  loadPeriodSummary(): void {
    const start = this.peggingForm.value.periodStart;
    const end = this.peggingForm.value.periodEnd;
    
    if (!start || !end) return;

    this.loadingPeriod.set(true);
    this.profitService.getPeriodSummary(
      start.toISOString(),
      end.toISOString()
    ).subscribe({
      next: (summary) => {
        this.periodSummary.set(summary);
        this.loadingPeriod.set(false);
      },
      error: (err) => {
        console.error('Period summary error:', err);
        this.loadingPeriod.set(false);
      }
    });
  }

  async onConfirmFifo(): Promise<void> {
    const v = this.fifoForm.getRawValue();
    if (!this.canSubmitFifo() || v.amount == null) return;
    const price = v.price;
    if (price == null || price <= 0) return;

    const start = this.peggingForm.value.periodStart;
    const end = this.peggingForm.value.periodEnd;
    if (!start || !end) return;

    const fs = this.fifoSim();
    const cashTotal = Math.round(v.amount * price * 1000) / 1000;
    const profitG = fs && price > 0 ? fs.estimatedProfitTl / price : 0;
    const profitSign = profitG >= 0 ? '+' : '';

    const confirm = await Swal.fire({
      title: 'FIFO nakit bağlama',
      html:
        `<p>Seçili dönem FIFO ile <strong>${v.amount.toLocaleString('tr-TR', { maximumFractionDigits: 4 })} Has</strong> bağlanacak.</p>` +
        `<p>Toplam <strong>${cashTotal.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} TL</strong> ` +
        `<strong>${price.toLocaleString('tr-TR')}</strong> TL/gr fiyatıyla.</p>` +
        `<p>Beklenen kâr/zarar: yaklaşık <strong>${profitSign}${profitG.toFixed(2)}</strong> Has gr ` +
        `(${fs ? fs.estimatedProfitTl.toLocaleString('tr-TR', { maximumFractionDigits: 2 }) : '—'} TL).</p>` +
        `<p>Onaylıyor musunuz?</p>`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Evet, bağla',
      cancelButtonText: 'Vazgeç',
    });
    if (!confirm.isConfirmed) return;

    this.fifoSaving.set(true);
    const notes = this.peggingForm.value.notes || undefined;
    this.linkingService
      .process({
        targetAmountGram: v.amount,
        targetPricePerGram: price,
        notes,
        periodStart: start.toISOString(),
        periodEnd: end.toISOString(),
      })
      .subscribe({
        next: (r) => {
          this.fifoSaving.set(false);
          this.snackBar.open(
            `FIFO bağlama tamamlandı. Tahmini kâr: ${r.totalProfit.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} TL.`,
            'Tamam',
            { duration: 8000, panelClass: 'snackbar-success', horizontalPosition: 'center', verticalPosition: 'top' }
          );
          this.refreshService.triggerRefresh();
          this.fifoSim.set(null);
          this.dialogRef.close(true);
        },
        error: (err) => {
          this.fifoSaving.set(false);
          this.snackBar.open(
            err?.error?.message || err?.message || 'FIFO bağlama hatası',
            'Kapat',
            { duration: 6000, panelClass: 'snackbar-error', horizontalPosition: 'center', verticalPosition: 'top' }
          );
        },
      });
  }

  async onConfirmPegging(): Promise<void> {
    if (this.peggingForm.invalid) return;

    const formValue = this.peggingForm.value;
    const sim = this.simulationResult();
    const price = this.simulationPrice();
    const cash = sim?.periodCashBalance ?? 0;
    const totalNet = sim?.netProfitHasGram ?? 0;
    const realized = sim?.realizedNetProfitHasGram ?? totalNet;
    const ts = totalNet >= 0 ? '+' : '';
    const rs = realized >= 0 ? '+' : '';

    const confirm = await Swal.fire({
      title: 'Hibrit nakit bağlama',
      html:
        `<p>Toplam <strong>${cash.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} TL</strong> ` +
        `<strong>${price.toLocaleString('tr-TR')}</strong> TL/gr fiyatından hasa çevrilecek.</p>` +
        `<p>Mühürlenen (bu tutar): <strong>${rs}${realized.toFixed(2)}</strong> Has gr.</p>` +
        `<p>Kasadaki nakitle birlikte dönem neti: <strong>${ts}${totalNet.toFixed(2)}</strong> Has gr.</p>` +
        `<p>Onaylıyor musunuz?</p>`,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Evet, kaydet',
      cancelButtonText: 'Vazgeç',
    });
    if (!confirm.isConfirmed) return;

    this.saving.set(true);

    this.profitService
      .pegCash({
        periodStart: formValue.periodStart!.toISOString(),
        periodEnd: formValue.periodEnd!.toISOString(),
        goldPricePerGram: price,
        notes: formValue.notes || undefined,
        pegCashFromSafe: formValue.pegCashFromSafe ?? undefined,
        pegHasGram: formValue.pegHasGram ?? undefined,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);

          const cashStr = sim ? sim.periodCashBalance.toLocaleString('tr-TR', { maximumFractionDigits: 0 }) : '?';
          const hasStr = sim ? sim.cashEquivalentHasGram.toFixed(2) : '?';
          const rs = (sim?.realizedNetProfitHasGram ?? sim?.netProfitHasGram ?? 0) >= 0 ? '+' : '';
          const realizedStr = sim ? (sim.realizedNetProfitHasGram ?? sim.netProfitHasGram).toFixed(2) : '?';

          this.snackBar.open(
            `İşlem başarılı: ${cashStr} TL → ${hasStr} Has gr. Mühürlenmiş net: ${rs}${realizedStr} gr.`,
            'Tamam',
            { duration: 8000, panelClass: 'snackbar-success', horizontalPosition: 'center', verticalPosition: 'top' }
          );

          this.refreshService.triggerRefresh();
          this.dialogRef.close(true);
        },
        error: (err) => {
          console.error('Pegging error:', err);
          this.saving.set(false);
          this.snackBar.open(
            'Nakit bağlama hatası: ' + (err.error?.message || 'Bilinmeyen hata'),
            'Kapat',
            { duration: 6000, panelClass: 'snackbar-error', horizontalPosition: 'center', verticalPosition: 'top' }
          );
        },
      });
  }
}
