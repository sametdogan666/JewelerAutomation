import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormArray, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DecimalPipe, DatePipe, NgClass } from '@angular/common';
import { NgxMaskDirective } from 'ngx-mask';
import {
  TransactionsService,
  BasketCreate,
  BasketItemCreate,
  Transaction,
  TransactionDirection,
} from '../../core/services/transactions.service';
import { CustomersService, Customer } from '../../core/services/customers.service';

const MILYEM_FACTOR = 0.001;
const LABOUR_FACTOR = 0.01;

interface ItemSummary {
  direction: 'Satış' | 'Alış';
  quantity: number;
  milyem: number;
  hasGram: number;
  price: number;
  totalValue: number;
  description: string;
}

@Component({
  selector: 'app-transaction-form',
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
    MatRadioModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    DecimalPipe,
    DatePipe,
    NgClass,
    NgxMaskDirective,
  ],
  templateUrl: './transaction-form.component.html',
  styleUrl: './transaction-form.component.scss',
})
export class TransactionFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private transactionsApi = inject(TransactionsService);
  private customersApi = inject(CustomersService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  customers = signal<Customer[]>([]);
  saving = signal(false);
  editMode = signal(false);
  transactionId = signal<string | null>(null);

  /** Nakit bağlama (CorrelationId + kalem yok): sanal kalem + özet bu kayıttan beslenir. */
  nakitBaglamaDetail = signal<{
    cash: number;
    equivalentHasGram: number;
    goldPricePerGram: number;
  } | null>(null);

  headerForm = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    description: [''],
    customerId: [null as string | null],
  });

  itemsArray = this.fb.array<FormGroup>([]);

  /** Reactive snapshot of items for computed summaries */
  itemsSnapshot = signal<any[]>([]);

  totalBuy = computed(() => {
    const peg = this.nakitBaglamaDetail();
    if (peg) {
      return {
        hasGram: peg.equivalentHasGram,
        cash: peg.cash,
      };
    }
    const items = this.itemsSnapshot();
    let has = 0, cash = 0;
    for (const it of items) {
      if (it.direction === 1) {
        has += this.calcHasGram(it);
        cash += this.parseNum(it.price);
      }
    }
    return { hasGram: Math.round(has * 1e6) / 1e6, cash: Math.round(cash * 100) / 100 };
  });

  totalSell = computed(() => {
    if (this.nakitBaglamaDetail()) {
      return { hasGram: 0, cash: 0 };
    }
    const items = this.itemsSnapshot();
    let has = 0, cash = 0;
    for (const it of items) {
      if (it.direction === 0) {
        has += this.calcHasGram(it);
        cash += this.parseNum(it.price);
      }
    }
    return { hasGram: Math.round(has * 1e6) / 1e6, cash: Math.round(cash * 100) / 100 };
  });

  netResult = computed(() => {
    const peg = this.nakitBaglamaDetail();
    if (peg) {
      return {
        hasGram: peg.equivalentHasGram,
        cash: Math.round(-peg.cash * 1000) / 1000,
      };
    }
    const buy = this.totalBuy();
    const sell = this.totalSell();
    return {
      hasGram: Math.round((buy.hasGram - sell.hasGram) * 1e6) / 1e6,
      cash: Math.round((sell.cash - buy.cash) * 100) / 100,
    };
  });

  /** TL gösterimi: nakit bağlama özetinde daha fazla ondalık. */
  summaryCashDigits = computed(() => (this.nakitBaglamaDetail() ? '1.2-3' : '1.0-0'));

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editMode.set(true);
      this.transactionId.set(id);
      this.transactionsApi.getById(id).subscribe({
        next: (tx) => {
          this.headerForm.enable({ emitEvent: false });
          this.nakitBaglamaDetail.set(null);

          this.headerForm.patchValue({
            transactionDate: new Date(tx.transactionDate).toISOString().slice(0, 10),
            description: tx.description ?? '',
            customerId: tx.customerId ?? null,
          });

          this.itemsArray.clear();

          if (this.isNakitBaglamaTransaction(tx)) {
            const detail = this.buildNakitBaglamaDetail(tx);
            this.nakitBaglamaDetail.set(detail);
            this.headerForm.disable({ emitEvent: false });
            this.syncSnapshot();
            return;
          }

          for (const item of tx.items) {
            this.addItem(item.direction, item.quantity, item.milyem,
              item.pieceCount ?? 0, item.unitLabour ?? 0,
              item.price ?? 0, item.description ?? '');
          }
          if (tx.items.length === 0) {
            this.addItem();
          }
          this.syncSnapshot();
        },
      });
    } else {
      this.addItem();
    }
    this.customersApi.getAll().subscribe((list) => this.customers.set(list));
  }

  createItemGroup(
    direction: TransactionDirection = 1,
    quantity = 0,
    milyem = 916,
    pieceCount = 0,
    unitLabour = 0,
    price: number = 0,
    description = ''
  ): FormGroup {
    const g = this.fb.group({
      direction: [direction as TransactionDirection, Validators.required],
      quantity: [quantity, [Validators.required, Validators.min(0.001)]],
      milyem: [milyem, [Validators.required, Validators.min(0), Validators.max(1000)]],
      pieceCount: [pieceCount],
      unitLabour: [unitLabour],
      price: [price as number | null],
      description: [description],
    });
    g.valueChanges.subscribe(() => this.syncSnapshot());
    return g;
  }

  addItem(
    direction: TransactionDirection = 1,
    quantity = 0,
    milyem = 916,
    pieceCount = 0,
    unitLabour = 0,
    price: number = 0,
    description = ''
  ): void {
    this.itemsArray.push(
      this.createItemGroup(direction, quantity, milyem, pieceCount, unitLabour, price, description)
    );
    this.syncSnapshot();
  }

  removeItem(index: number): void {
    if (this.itemsArray.length <= 1) return;
    this.itemsArray.removeAt(index);
    this.syncSnapshot();
  }

  private syncSnapshot(): void {
    this.itemsSnapshot.set(this.itemsArray.controls.map(c => c.getRawValue()));
  }

  calcHasGram(item: any): number {
    const q = item.quantity ?? 0;
    const m = item.milyem ?? 0;
    let has = q * m * MILYEM_FACTOR;
    if (item.direction === 0) {
      const pc = item.pieceCount ?? 0;
      const ul = item.unitLabour ?? 0;
      const labour = -(pc * ul * LABOUR_FACTOR);
      has += labour;
    }
    return Math.round(has * 1e6) / 1e6;
  }

  isSaleItem(index: number): boolean {
    return this.itemsArray.at(index)?.get('direction')?.value === 0;
  }

  parseNum(val: any): number {
    if (val == null || val === '') return 0;
    const str = String(val).replace(/\./g, '').replace(/,/g, '.');
    const num = parseFloat(str);
    return isNaN(num) ? 0 : num;
  }

  isNakitBaglamaTransaction(tx: Transaction): boolean {
    return !!tx.correlationId && (!tx.items || tx.items.length === 0);
  }

  /**
   * İşlem kaydındaki CashAmount / EquivalentHasGram öncelikli;
   * birim has fiyatı işlemde saklanmadığı için cash / has ile türetilir (bağlama anındaki TL/gr).
   */
  private buildNakitBaglamaDetail(tx: Transaction): {
    cash: number;
    equivalentHasGram: number;
    goldPricePerGram: number;
  } {
    const fromCash = tx.cashAmount != null && tx.cashAmount !== undefined
      ? Math.abs(Number(tx.cashAmount))
      : null;
    const cash =
      fromCash != null && !Number.isNaN(fromCash) && fromCash > 1e-9
        ? fromCash
        : Math.abs(Number(tx.netCashAmount ?? 0)) > 1e-9
          ? Math.abs(Number(tx.netCashAmount))
          : Math.abs(Number(tx.price ?? 0));

    const fromEq = tx.equivalentHasGram != null && tx.equivalentHasGram !== undefined
      ? Math.abs(Number(tx.equivalentHasGram))
      : null;
    const equivalentHasGram =
      fromEq != null && !Number.isNaN(fromEq) && fromEq > 1e-9
        ? fromEq
        : Math.abs(Number(tx.netHasGram ?? tx.hasGram ?? 0));

    const goldPricePerGram =
      equivalentHasGram > 1e-9 ? Math.round((cash / equivalentHasGram) * 1e6) / 1e6 : 0;

    return {
      cash: Math.round(cash * 1000) / 1000,
      equivalentHasGram: Math.round(equivalentHasGram * 1e6) / 1e6,
      goldPricePerGram,
    };
  }

  onSubmit(): void {
    if (this.nakitBaglamaDetail() || this.itemsArray.length === 0 || this.saving()) return;

    const header = this.headerForm.getRawValue();
    const items: BasketItemCreate[] = this.itemsArray.controls.map(c => {
      const v = c.getRawValue();
      return {
        direction: v.direction,
        quantity: v.quantity,
        milyem: v.milyem,
        pieceCount: v.pieceCount && v.pieceCount > 0 ? v.pieceCount : undefined,
        unitLabour: this.parseNum(v.unitLabour) || undefined,
        price: this.parseNum(v.price) || undefined,
        description: v.description || undefined,
      };
    });

    const dto: BasketCreate = {
      transactionDate: new Date(header.transactionDate).toISOString(),
      description: header.description || undefined,
      customerId: header.customerId || undefined,
      items,
    };

    this.saving.set(true);
    const op = this.editMode() && this.transactionId()
      ? this.transactionsApi.update(this.transactionId()!, dto)
      : this.transactionsApi.create(dto);

    op.subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/transactions']);
      },
      error: () => this.saving.set(false),
    });
  }
}
