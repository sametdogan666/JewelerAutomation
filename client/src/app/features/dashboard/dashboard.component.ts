import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CustomersService } from '../../core/services/customers.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';
import { SafeStatusWidgetComponent } from './safe-status-widget.component';
import { ProfitWidgetComponent } from './profit-widget.component';
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
export class DashboardComponent implements OnInit {
  private customers = inject(CustomersService);
  private dialog = inject(MatDialog);
  private refreshService = inject(DashboardRefreshService);

  customerCount = signal<number | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loading.set(true);
    this.customers.getAll().subscribe({
      next: (list) => { this.customerCount.set(list.length); this.loading.set(false); },
      error: () => this.loading.set(false),
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
