import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { catchError, finalize, forkJoin, of, Subscription, timeout } from 'rxjs';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Customer, CustomersService } from '../../core/services/customers.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';
import {
  createDefaultDashboardSummary,
  DashboardService,
  DashboardSummary,
  normalizeDashboardSummary,
} from '../../core/services/dashboard.service';
import { GoldRatesSignalRService } from '../../core/services/gold-rates-signalr.service';
import { SafeStatusWidgetComponent } from './safe-status-widget.component';
import { ProfitWidgetComponent } from './profit-widget.component';
import { ManualGoldRateDialogComponent } from './manual-gold-rate-dialog.component';
import { ProfitAnalysisDialogComponent } from '../profit-analysis/profit-analysis-dialog.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DecimalPipe,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    MatTooltipModule,
    RouterLink,
    SafeStatusWidgetComponent,
    ProfitWidgetComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit, OnDestroy {
  private customers = inject(CustomersService);
  private dashboardService = inject(DashboardService);
  private dialog = inject(MatDialog);
  private refreshService = inject(DashboardRefreshService);
  private goldHub = inject(GoldRatesSignalRService);

  private refreshSub?: Subscription;

  customerCount = signal<number | null>(null);
  /** Panel özet verisi; başlangıçta varsayılan nesne — mor kutular hemen 0 ile render olur. */
  dashboardData = signal<DashboardSummary>(createDefaultDashboardSummary());
  /** Arka plan isteği için (tam sayfa spinner kullanılmıyor; yine de finalize + sert zaman ağı). */
  isLoading = signal(false);

  ngOnInit(): void {
    this.goldHub.start();
    this.isLoading.set(true);
    setTimeout(() => this.isLoading.set(false), 4000);
    forkJoin({
      summary: this.dashboardService
        .getSummary()
        .pipe(timeout(25000), catchError(() => of(null))),
      customers: this.customers
        .getAll()
        .pipe(timeout(25000), catchError(() => of([] as Customer[]))),
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe(({ summary, customers }) => {
        const data = normalizeDashboardSummary(summary ?? undefined);
        this.dashboardData.set(data);
        this.customerCount.set(customers?.length ?? 0);
      });

    this.refreshSub = this.refreshService.refresh$.subscribe(() => this.loadDashboard());
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
    this.goldHub.stop();
  }

  private loadDashboard(): void {
    this.isLoading.set(true);
    setTimeout(() => this.isLoading.set(false), 4000);
    this.dashboardService
      .getSummary()
      .pipe(
        timeout(25000),
        catchError(() => of(null)),
        finalize(() => this.isLoading.set(false)),
      )
      .subscribe((s) => {
        if (s != null && typeof s === 'object') {
          this.dashboardData.set(normalizeDashboardSummary(s));
        }
      });
  }

  openManualGoldRate(): void {
    this.dialog.open(ManualGoldRateDialogComponent, { width: '420px', maxWidth: '95vw' }).afterClosed().subscribe((saved) => {
      if (saved)
        this.loadDashboard();
    });
  }

  openProfitAnalysis(): void {
    const ref = this.dialog.open(ProfitAnalysisDialogComponent, {
      width: '1100px',
      maxWidth: '95vw',
      height: 'auto',
      maxHeight: '90vh',
      disableClose: false,
      panelClass: 'profit-analysis-dialog',
    });
    ref.afterClosed().subscribe((result) => {
      if (result) {
        this.refreshService.triggerRefresh();
      }
    });
  }
}
