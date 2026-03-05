import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
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
    MatTooltipModule,
  ],
  templateUrl: './transactions-list.component.html',
  styleUrl: './transactions-list.component.scss',
})
export class TransactionsListComponent implements OnInit {
  private api = inject(TransactionsService);

  dataSource = new MatTableDataSource<Transaction>([]);
  loading = signal(true);
  deleting = signal<string | null>(null);
  displayedColumns = ['transactionDate', 'direction', 'quantity', 'milyem', 'hasGram', 'price', 'customer', 'description', 'actions'];

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

  onDelete(transaction: Transaction): void {
    if (!confirm(`"${transaction.description || 'Bu işlem'}" kaydını silmek istediğinizden emin misiniz? İlişkili kasa hareketi de silinecektir.`)) {
      return;
    }
    this.deleting.set(transaction.id);
    this.api.delete(transaction.id).subscribe({
      next: () => {
        this.deleting.set(null);
        this.dataSource.data = this.dataSource.data.filter(t => t.id !== transaction.id);
      },
      error: () => this.deleting.set(null),
    });
  }
}
