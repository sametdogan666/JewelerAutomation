import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DecimalPipe } from '@angular/common';
import { CustomersService, Customer } from '../../core/services/customers.service';
import {
  CustomerAccountService,
  CustomerBalance,
  CustomerTransactionDto,
  CustomerTransactionType,
  CreateCustomerTransactionRequest,
} from '../../core/services/customer-account.service';

const TRANSACTION_TYPES: { value: CustomerTransactionType; label: string }[] = [
  { value: 0, label: 'Altın alış' },
  { value: 1, label: 'Altın satış' },
  { value: 2, label: 'Nakit ödeme' },
  { value: 3, label: 'Nakit tahsilat' },
];

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    DecimalPipe,
  ],
  templateUrl: './customer-detail.component.html',
  styleUrl: './customer-detail.component.scss',
})
export class CustomerDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private customersApi = inject(CustomersService);
  private accountApi = inject(CustomerAccountService);
  private fb = inject(FormBuilder);

  customer = signal<Customer | null>(null);
  balance = signal<CustomerBalance | null>(null);
  statement = signal<CustomerTransactionDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  showTransactionForm = signal(false);

  dataSource = new MatTableDataSource<CustomerTransactionDto>([]);
  displayedColumns = ['transactionDate', 'transactionType', 'goldHas', 'cashAmount', 'description'];

  transactionTypes = TRANSACTION_TYPES;
  customerId = computed(() => this.route.snapshot.paramMap.get('id'));

  form = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    transactionType: [0 as CustomerTransactionType, Validators.required],
    goldGram: [0],
    goldMilyem: [916],
    cashAmount: [0],
    description: [''],
  });

  get isGoldTransaction(): boolean {
    const t = this.form.get('transactionType')?.value;
    return t === 0 || t === 1;
  }
  get isCashTransaction(): boolean {
    const t = this.form.get('transactionType')?.value;
    return t === 2 || t === 3;
  }

  ngOnInit(): void {
    const id = this.customerId();
    if (!id) return;
    this.loading.set(true);
    this.customersApi.getById(id).subscribe({
      next: (c) => {
        this.customer.set(c);
        this.loadAccount(id);
      },
      error: () => this.loading.set(false),
    });
  }

  private loadAccount(id: string): void {
    this.accountApi.getBalance(id).subscribe({
      next: (b) => this.balance.set(b),
      error: () => {},
    });
    this.accountApi.getStatement(id).subscribe({
      next: (list) => {
        this.statement.set(list);
        this.dataSource.data = list;
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  transactionTypeLabel(type: CustomerTransactionType): string {
    return TRANSACTION_TYPES.find((t) => t.value === type)?.label ?? '';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }

  toggleTransactionForm(): void {
    this.showTransactionForm.update((v) => !v);
  }

  onSubmitTransaction(): void {
    const id = this.customerId();
    if (!id || this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const dto: CreateCustomerTransactionRequest = {
      transactionDate: new Date(v.transactionDate).toISOString(),
      transactionType: v.transactionType,
      goldGram: this.isGoldTransaction ? v.goldGram : 0,
      goldMilyem: this.isGoldTransaction ? v.goldMilyem : 0,
      goldHas: 0,
      cashAmount: this.isCashTransaction ? v.cashAmount : 0,
      description: v.description || undefined,
    };
    this.saving.set(true);
    this.accountApi.createTransaction(id, dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.reset({
          transactionDate: new Date().toISOString().slice(0, 10),
          transactionType: 0,
          goldGram: 0,
          goldMilyem: 916,
          cashAmount: 0,
          description: '',
        });
        this.showTransactionForm.set(false);
        this.loadAccount(id);
      },
      error: () => this.saving.set(false),
    });
  }
}
