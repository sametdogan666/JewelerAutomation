import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { CustomersService, Customer } from '../../core/services/customers.service';
import { NotificationService } from '../../core/services/notification.service';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-customers-list',
  standalone: true,
  imports: [NgIf, RouterLink, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, MatChipsModule],
  templateUrl: './customers-list.component.html',
  styleUrl: './customers-list.component.scss',
})
export class CustomersListComponent implements OnInit {
  private api = inject(CustomersService);
  private notify = inject(NotificationService);

  dataSource = new MatTableDataSource<Customer>([]);
  loading = signal(true);
  displayedColumns = ['name', 'type', 'phone', 'actions'];

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

  async delete(item: Customer, event: Event): Promise<void> {
    event.stopPropagation();
    const confirmed = await this.notify.confirmDelete(`"${item.name}" müşteri kaydını silmek istediğinize emin misiniz?`);
    if (!confirmed) return;
    this.api.delete(item.id).subscribe({
      next: () => {
        this.dataSource.data = this.dataSource.data.filter((x) => x.id !== item.id);
        this.notify.success('Müşteri silindi');
      },
      error: (err) => {
        this.notify.error('Silme Hatası', err?.error?.message || 'Müşteri silinirken bir hata oluştu.');
      },
    });
  }

  typeLabel(type: 0 | 1): string {
    return type === 0 ? 'Cari' : 'Şahıs';
  }
}
