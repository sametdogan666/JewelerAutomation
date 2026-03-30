import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { CustomersService } from '../../core/services/customers.service';
import { SafeStatusWidgetComponent } from './safe-status-widget.component';
import { ProfitWidgetComponent } from './profit-widget.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, MatCardModule, MatIconModule, MatButtonModule, RouterLink, SafeStatusWidgetComponent, ProfitWidgetComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private customers = inject(CustomersService);

  customerCount = signal<number | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.loading.set(true);
    this.customers.getAll().subscribe({
      next: (list) => { this.customerCount.set(list.length); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
