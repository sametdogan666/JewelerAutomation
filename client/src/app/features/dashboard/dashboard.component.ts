import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { SafeService } from '../../core/services/safe.service';
import { CustomersService } from '../../core/services/customers.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, MatCardModule, MatIconModule, MatButtonModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private safe = inject(SafeService);
  private customers = inject(CustomersService);

  balance = signal<number | null>(null);
  customerCount = signal<number | null>(null);
  loading = signal(true);
  private doneCount = 0;

  ngOnInit(): void {
    this.loading.set(true);
    this.doneCount = 0;
    this.safe.getBalance().subscribe({
      next: (v) => { this.balance.set(v); this.done(); },
      error: () => this.done(),
    });
    this.customers.getAll().subscribe({
      next: (list) => { this.customerCount.set(list.length); this.done(); },
      error: () => this.done(),
    });
  }

  private done(): void {
    this.doneCount++;
    if (this.doneCount >= 2) this.loading.set(false);
  }
}
