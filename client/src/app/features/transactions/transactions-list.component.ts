import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TransactionsService, Transaction, TransactionDirection } from '../../core/services/transactions.service';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-transactions-list',
  standalone: true,
  imports: [
    DecimalPipe,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './transactions-list.component.html',
  styleUrl: './transactions-list.component.scss',
})
export class TransactionsListComponent implements OnInit {
  private api = inject(TransactionsService);

  dataSource = new MatTableDataSource<Transaction>([]);
  loading = signal(true);
  displayedColumns = ['transactionDate', 'direction', 'quantity', 'milyem', 'hasGram', 'price', 'customer', 'description'];

  ngOnInit(): void {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: (list) => {
        this.dataSource.data = list;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  directionLabel(d: TransactionDirection): string {
    return d === 0 ? 'Satış' : 'Alış';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }
}
