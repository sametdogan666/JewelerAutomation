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
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatRadioModule } from '@angular/material/radio';
import { DecimalPipe } from '@angular/common';
import { CustomersService, Customer } from '../../core/services/customers.service';
import { NotificationService } from '../../core/services/notification.service';
import { DashboardRefreshService } from '../../core/services/dashboard-refresh.service';
import {
  CustomerAccountService,
  CustomerBalance,
  CustomerStatementEntryDto,
  CustomerTransactionType,
  CreateCustomerTransactionRequest,
  SahisOpeningBalanceRequest,
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
    MatTooltipModule,
    MatRadioModule,
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
  private notify = inject(NotificationService);
  private refreshService = inject(DashboardRefreshService);

  customer = signal<Customer | null>(null);
  balance = signal<CustomerBalance | null>(null);
  statement = signal<CustomerStatementEntryDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  savingOpening = signal(false);
  showTransactionForm = signal(false);

  dataSource = new MatTableDataSource<CustomerStatementEntryDto>([]);
  displayedColumns = [
    'transactionDate',
    'transactionType',
    'goldHas',
    'milyem',
    'netCash',
    'description',
    'basket',
    'detail',
    'actions',
  ];
  deleting = signal<string | null>(null);
  expandedEntryId = signal<string | null>(null);

  transactionTypes = TRANSACTION_TYPES;
  customerId = computed(() => this.route.snapshot.paramMap.get('id'));

  form = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    transactionType: [0 as CustomerTransactionType, Validators.required],
    goldGram: [0],
    goldMilyem: [916],
    cashAmount: [0],
    cashCurrency: [0],
    description: [''],
  });

  openingForm = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    assetKind: [0, Validators.required],
    amount: [0, [Validators.required, Validators.min(0.000001)]],
    customerIsCreditor: [true],
    description: [''],
  });

  openingAssetOptions = [
    { value: 0, label: 'Altın (Has gr)' },
    { value: 1, label: 'TL' },
    { value: 2, label: 'USD' },
    { value: 3, label: 'EUR' },
    { value: 4, label: 'GBP' },
  ] as const;

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
        this.expandedEntryId.set(null);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  transactionTypeLabel(type: CustomerTransactionType): string {
    if (type === 20) return 'Eski bakiye (devir)';
    if (type === 21) return 'Emanet (sepet)';
    return TRANSACTION_TYPES.find((t) => t.value === type)?.label ?? '';
  }

  cashSuffix(cur: number): string {
    if (cur === 1) return 'USD';
    if (cur === 2) return 'EUR';
    if (cur === 3) return 'GBP';
    return '₺';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }

  formatNetCashLine(row: CustomerStatementEntryDto): string {
    const parts: string[] = [];
    const n = (x: number) =>
      x.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (row.netCashTry !== 0) parts.push(`${n(row.netCashTry)} ₺`);
    if (row.netCashUsd !== 0) parts.push(`${n(row.netCashUsd)} USD`);
    if (row.netCashEur !== 0) parts.push(`${n(row.netCashEur)} EUR`);
    if (row.netCashGbp !== 0) parts.push(`${n(row.netCashGbp)} GBP`);
    return parts.length > 0 ? parts.join(' · ') : '—';
  }

  toggleBasketDetail(row: CustomerStatementEntryDto): void {
    if (!row.isBasketGroup || !row.lineItems?.length) return;
    this.expandedEntryId.update((id) => (id === row.entryId ? null : row.entryId));
  }

  isDetailExpanded(row: CustomerStatementEntryDto): boolean {
    return this.expandedEntryId() === row.entryId;
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
      cashCurrency: Math.round(Number(v.cashCurrency ?? 0)),
      postToLedger: true,
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
          cashCurrency: 0,
          description: '',
        });
        this.showTransactionForm.set(false);
        this.loadAccount(id);
      },
      error: () => this.saving.set(false),
    });
  }

  async onDeleteStatementRow(row: CustomerStatementEntryDto): Promise<void> {
    if (!row.canDelete || !row.primaryTransactionId) return;
    const label = this.transactionTypeLabel(row.transactionType);
    const confirmed = await this.notify.confirmDelete(`"${label}" işlemini silmek istediğinize emin misiniz?`);
    if (!confirmed) return;

    const tid = row.primaryTransactionId;
    this.deleting.set(tid);
    this.accountApi.deleteTransaction(tid).subscribe({
      next: () => {
        this.deleting.set(null);
        this.notify.success('İşlem silindi');
        this.refreshService.triggerRefresh();
        const customerId = this.customerId();
        if (customerId) {
          this.loadAccount(customerId);
        }
      },
      error: () => {
        this.deleting.set(null);
        this.notify.error('Silme Hatası', 'Hareket silinirken bir hata oluştu.');
      }
    });
  }

  onSubmitOpening(): void {
    const id = this.customerId();
    if (!id || this.customer()?.type !== 1 || this.openingForm.invalid || this.savingOpening()) return;
    const v = this.openingForm.getRawValue();
    const dto: SahisOpeningBalanceRequest = {
      transactionDate: new Date(v.transactionDate).toISOString(),
      assetKind: v.assetKind,
      amount: Number(v.amount),
      customerIsCreditor: v.customerIsCreditor,
      description: v.description || undefined,
    };
    this.savingOpening.set(true);
    this.accountApi.postSahisOpeningBalance(id, dto).subscribe({
      next: () => {
        this.savingOpening.set(false);
        this.openingForm.reset({
          transactionDate: new Date().toISOString().slice(0, 10),
          assetKind: 0,
          amount: 0,
          customerIsCreditor: true,
          description: '',
        });
        this.loadAccount(id);
        this.refreshService.triggerRefresh();
        this.notify.success('Devir kaydı oluşturuldu');
      },
      error: () => {
        this.savingOpening.set(false);
        this.notify.error('Hata', 'Devir kaydı eklenemedi.');
      },
    });
  }
}
