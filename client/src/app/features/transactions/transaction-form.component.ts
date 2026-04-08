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
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { DecimalPipe, DatePipe, NgClass } from '@angular/common';
import { NgxMaskDirective } from 'ngx-mask';
import {
  TransactionsService,
  BasketCreate,
  BasketItemCreate,
  Transaction,
  TransactionDirection,
  PaymentCurrency,
} from '../../core/services/transactions.service';
import { CustomersService, Customer } from '../../core/services/customers.service';
import { DashboardService } from '../../core/services/dashboard.service';
import {
  ProductTemplate,
  ProductTemplatesService,
} from '../../core/services/product-templates.service';
import { catchError, of, timeout } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ThermalReceiptService } from '../../core/services/thermal-receipt.service';

const MILYEM_FACTOR = 0.001;
const LABOUR_FACTOR = 0.01;

type PriceInputMode = 'unit' | 'total';

interface CashBucket {
  try: number;
  usd: number;
  eur: number;
  gbp: number;
}

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
    MatSlideToggleModule,
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
  private dashboardApi = inject(DashboardService);
  private productTemplatesApi = inject(ProductTemplatesService);
  private snackBar = inject(MatSnackBar);
  private thermalReceipt = inject(ThermalReceiptService);

  /** Panel özeti ile aynı kaynak; sadece bilgi — satır fiyatına yazılmaz, kullanıcı girdisi esastır. */
  referenceHasMid = signal<number | null>(null);

  /** Ürün şablonları (Ayarlar); sepet satırı seçimi. */
  templates = signal<ProductTemplate[]>([]);

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
    isSahisEmanet: [false],
    /** 1 = emanet satış, 2 = emanet alış */
    sahisEmanetMode: [1 as 1 | 2],
    kasaHareketli: [true],
  });

  itemsArray = this.fb.array<FormGroup>([]);

  /** Reactive snapshot of items for computed summaries */
  itemsSnapshot = signal<any[]>([]);

  totalBuy = computed(() => {
    const peg = this.nakitBaglamaDetail();
    if (peg) {
      const cash: CashBucket = { try: peg.cash, usd: 0, eur: 0, gbp: 0 };
      return {
        hasGram: peg.equivalentHasGram,
        cash,
      };
    }
    const items = this.itemsSnapshot();
    let has = 0;
    const cash: CashBucket = { try: 0, usd: 0, eur: 0, gbp: 0 };
    for (const it of items) {
      if (it.direction === 1) {
        has += this.calcHasGram(it);
        this.addCashToBucket(cash, it.paymentCurrency, this.effectiveLineTotalTl(it));
      }
    }
    return {
      hasGram: Math.round(has * 1e6) / 1e6,
      cash: this.roundCashBucket(cash),
    };
  });

  totalSell = computed(() => {
    if (this.nakitBaglamaDetail()) {
      return { hasGram: 0, cash: { try: 0, usd: 0, eur: 0, gbp: 0 } };
    }
    const items = this.itemsSnapshot();
    let has = 0;
    const cash: CashBucket = { try: 0, usd: 0, eur: 0, gbp: 0 };
    for (const it of items) {
      if (it.direction === 0) {
        has += this.calcHasGram(it);
        this.addCashToBucket(cash, it.paymentCurrency, this.effectiveLineTotalTl(it));
      }
    }
    return {
      hasGram: Math.round(has * 1e6) / 1e6,
      cash: this.roundCashBucket(cash),
    };
  });

  netResult = computed(() => {
    const peg = this.nakitBaglamaDetail();
    if (peg) {
      return {
        hasGram: peg.equivalentHasGram,
        cash: {
          try: Math.round(-peg.cash * 1000) / 1000,
          usd: 0,
          eur: 0,
          gbp: 0,
        },
      };
    }
    const buy = this.totalBuy();
    const sell = this.totalSell();
    return {
      hasGram: Math.round((buy.hasGram - sell.hasGram) * 1e6) / 1e6,
      cash: {
        try: Math.round((sell.cash.try - buy.cash.try) * 100) / 100,
        usd: Math.round((sell.cash.usd - buy.cash.usd) * 100) / 100,
        eur: Math.round((sell.cash.eur - buy.cash.eur) * 100) / 100,
        gbp: Math.round((sell.cash.gbp - buy.cash.gbp) * 100) / 100,
      },
    };
  });

  /** TL gösterimi: nakit bağlama özetinde daha fazla ondalık. */
  summaryCashDigits = computed(() => (this.nakitBaglamaDetail() ? '1.2-3' : '1.0-0'));

  private addCashToBucket(bucket: CashBucket, cur: unknown, amount: number): void {
    const c =
      cur === 1 ? 'usd' : cur === 2 ? 'eur' : cur === 3 ? 'gbp' : 'try';
    bucket[c] += amount;
  }

  private roundCashBucket(b: CashBucket): CashBucket {
    return {
      try: Math.round(b.try * 100) / 100,
      usd: Math.round(b.usd * 100) / 100,
      eur: Math.round(b.eur * 100) / 100,
      gbp: Math.round(b.gbp * 100) / 100,
    };
  }

  ngOnInit(): void {
    this.loadReferenceHasMid();
    this.productTemplatesApi.getAll().subscribe({
      next: (list) => this.templates.set(list),
      error: () => this.templates.set([]),
    });

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
            isSahisEmanet: tx.isSahisEmanet ?? false,
            sahisEmanetMode: (tx.sahisEmanetMode === 2 ? 2 : 1) as 1 | 2,
            kasaHareketli: tx.kasaHareketli !== false,
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
            const lineTotal = item.price ?? 0;
            const unitPrice =
              item.hasGram > 1e-9
                ? Math.round((lineTotal / item.hasGram) * 1e6) / 1e6
                : lineTotal;
            this.addItem(
              item.direction,
              item.quantity,
              item.milyem,
              item.pieceCount ?? 0,
              item.unitLabour ?? 0,
              unitPrice,
              item.description ?? '',
              item.id,
              item.productTemplateId ?? null,
              lineTotal,
              'total',
              (item.paymentCurrency ?? 0) as PaymentCurrency
            );
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

    this.headerForm.get('customerId')?.valueChanges.subscribe(() => {
      if (!this.isSelectedCustomerSahis()) {
        this.headerForm.patchValue(
          { isSahisEmanet: false, sahisEmanetMode: 1, kasaHareketli: true },
          { emitEvent: false }
        );
      }
    });
  }

  isSelectedCustomerSahis(): boolean {
    const id = this.headerForm.getRawValue().customerId;
    if (!id) return false;
    const c = this.customers().find((x) => x.id === id);
    return c?.type === 1;
  }

  private loadReferenceHasMid(): void {
    this.dashboardApi
      .getSummary()
      .pipe(timeout(3000), catchError(() => of(null)))
      .subscribe((s) => {
        const mid = s?.liveHasTryPerGramMid;
        this.referenceHasMid.set(typeof mid === 'number' && mid > 0 ? mid : null);
      });
  }

  /** Referans kur ile satır fiyatı arasında anlamlı fark var mı (engel değil, uyarı). */
  showPriceWarning(item: Record<string, unknown>): boolean {
    if (item['priceInputMode'] === 'total') return false;
    const ref = this.referenceHasMid();
    if (ref == null || ref <= 0) return false;
    const p = this.parseNum(item['price']);
    if (p <= 0) return false;
    return Math.abs(p - ref) / ref > 0.08;
  }

  createItemGroup(
    direction: TransactionDirection = 1,
    quantity = 0,
    milyem = 916,
    pieceCount = 0,
    unitLabour = 0,
    price: number = 0,
    description = '',
    itemId: string | null = null,
    productTemplateId: string | null = null,
    lineTotalTl: number | null = null,
    priceInputMode: PriceInputMode = 'unit',
    paymentCurrency: PaymentCurrency = 0
  ): FormGroup {
    const g = this.fb.group({
      itemId: [itemId],
      direction: [direction as TransactionDirection, Validators.required],
      quantity: [quantity, [Validators.required, Validators.min(0.001)]],
      milyem: [milyem, [Validators.required, Validators.min(0), Validators.max(1000)]],
      productTemplateId: [productTemplateId],
      pieceCount: [pieceCount],
      unitLabour: [unitLabour],
      price: [price as number | null],
      lineTotalTl: [lineTotalTl as number | null],
      priceInputMode: [priceInputMode],
      paymentCurrency: [paymentCurrency as PaymentCurrency],
      description: [description],
    });
    g.get('direction')?.valueChanges.subscribe(() => this.applyRowRulesForDirection(g));
    g.get('price')?.valueChanges.subscribe(() => {
      g.get('priceInputMode')?.setValue('unit', { emitEvent: false });
      this.syncLineTotalsForRow(g);
    });
    g.get('lineTotalTl')?.valueChanges.subscribe(() => {
      g.get('priceInputMode')?.setValue('total', { emitEvent: false });
      this.syncLineTotalsForRow(g);
    });
    for (const name of ['quantity', 'milyem', 'pieceCount', 'unitLabour'] as const) {
      g.get(name)?.valueChanges.subscribe(() => this.syncLineTotalsForRow(g));
    }
    g.valueChanges.subscribe(() => this.syncSnapshot());
    this.applyRowRulesForDirection(g);
    this.syncLineTotalsForRow(g);
    return g;
  }

  /**
   * unit: Toplam = Has × birim (otomatik).
   * total: Toplam elle; birim yalnızca referans (Has/Toplam), toplam ezilmez.
   */
  private syncLineTotalsForRow(g: FormGroup): void {
    const raw = g.getRawValue();
    const mode = raw.priceInputMode as PriceInputMode;
    const has = this.calcHasGram(raw);
    if (mode === 'unit') {
      const u = this.parseNum(raw.price);
      const t = Math.round(has * u * 100) / 100;
      const cur = this.parseNum(g.get('lineTotalTl')?.value);
      if (Math.abs(cur - t) > 0.009) {
        g.get('lineTotalTl')?.setValue(t, { emitEvent: false });
      }
    } else {
      const t = this.parseNum(raw.lineTotalTl);
      if (has > 1e-9) {
        const u = Math.round((t / has) * 1e6) / 1e6;
        const curU = this.parseNum(g.get('price')?.value);
        if (Math.abs(curU - u) > 1e-6) {
          g.get('price')?.setValue(u, { emitEvent: false });
        }
      }
    }
    this.syncSnapshot();
  }

  /** Alış: adet ve birim işçilik kapalı ve sıfır; satış: açık. Şablon varsa türe göre milyem güncellenir. */
  applyRowRulesForDirection(g: FormGroup): void {
    const dir = g.get('direction')?.value as TransactionDirection;
    const isPurchase = dir === 1;
    const pc = g.get('pieceCount');
    const ul = g.get('unitLabour');
    if (isPurchase) {
      pc?.disable({ emitEvent: false });
      ul?.disable({ emitEvent: false });
      pc?.setValue(0, { emitEvent: false });
      ul?.setValue(0, { emitEvent: false });
    } else {
      pc?.enable({ emitEvent: false });
      ul?.enable({ emitEvent: false });
    }
    this.syncTemplateMilyemIfSelected(g);
    this.syncLineTotalsForRow(g);
  }

  /** Şablon seçiliyse satır milyemini türe göre şablondan yazar (Tür değişince has anında güncellenir). */
  private syncTemplateMilyemIfSelected(g: FormGroup): void {
    const tid = g.get('productTemplateId')?.value as string | null | undefined;
    if (tid == null || String(tid).trim() === '') return;
    const t = this.templates().find((x) => x.id === tid);
    if (!t) return;
    const dir = g.get('direction')?.value as TransactionDirection;
    const m = dir === 0 ? t.milyemSatis : t.milyemAlis;
    g.get('milyem')?.setValue(m, { emitEvent: true });
  }

  milyemFromTemplate(t: ProductTemplate, direction: TransactionDirection): number {
    return direction === 0 ? t.milyemSatis : t.milyemAlis;
  }

  onTemplatePicked(rowIndex: number, templateId: string | null): void {
    const g = this.itemsArray.at(rowIndex) as FormGroup;
    if (!g) return;
    if (!templateId) return;
    const t = this.templates().find((x) => x.id === templateId);
    if (!t) return;
    const dir = g.get('direction')?.value as TransactionDirection;
    const isSale = dir === 0;
    const patch: Record<string, unknown> = {
      milyem: this.milyemFromTemplate(t, dir),
      description: t.name,
      unitLabour: isSale ? t.defaultLaborPrice : 0,
    };
    if (t.defaultGram > 0) {
      patch['quantity'] = t.defaultGram;
    }
    g.patchValue(patch);
    this.applyRowRulesForDirection(g);
  }

  addItem(
    direction: TransactionDirection = 1,
    quantity = 0,
    milyem = 916,
    pieceCount = 0,
    unitLabour = 0,
    price: number = 0,
    description = '',
    itemId: string | null = null,
    productTemplateId: string | null = null,
    lineTotalTl: number | null = null,
    priceInputMode: PriceInputMode = 'unit',
    paymentCurrency: PaymentCurrency = 0
  ): void {
    this.itemsArray.push(
      this.createItemGroup(
        direction,
        quantity,
        milyem,
        pieceCount,
        unitLabour,
        price,
        description,
        itemId,
        productTemplateId,
        lineTotalTl,
        priceInputMode,
        paymentCurrency
      )
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

  /** Brüt gr × ayar → has gr (milyem ≤ 1 ondalık saflık; üzeri binlik ayar). */
  gramMilyemToHas(gram: number, milyem: number): number {
    const g = Number(gram) || 0;
    const m = Number(milyem) || 0;
    if (m <= 1) {
      return Math.round(g * m * 1e6) / 1e6;
    }
    return Math.round(g * m * MILYEM_FACTOR * 1e6) / 1e6;
  }

  calcHasGram(item: any): number {
    const q = item.quantity ?? 0;
    const m = item.milyem ?? 0;
    const dir = item.direction as TransactionDirection;
    if (dir === 1) {
      return this.gramMilyemToHas(q, m);
    }
    let has = this.gramMilyemToHas(q, m);
    const rawPc = item.pieceCount;
    const pcNum = typeof rawPc === 'number' ? rawPc : parseFloat(String(rawPc ?? '')) || 0;
    const labourPieces = pcNum < 1 ? 1 : pcNum;
    const ul = this.parseNum(item.unitLabour);
    has += labourPieces * ul * LABOUR_FACTOR;
    return Math.round(has * 1e6) / 1e6;
  }

  /** Özet / satır: toplam modunda kayıtlı toplam; birim modunda Has × birim. */
  effectiveLineTotalTl(item: any): number {
    const mode = item.priceInputMode as PriceInputMode | undefined;
    if (mode === 'total') {
      return Math.round(this.parseNum(item.lineTotalTl) * 100) / 100;
    }
    return this.calcLineTotalTlFromUnit(item);
  }

  /** Has × birim (gösterim; birim modunda toplam alanı ile aynı). */
  calcLineTotalTlFromUnit(item: any): number {
    return Math.round(this.calcHasGram(item) * this.parseNum(item.price) * 100) / 100;
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
    if (header.isSahisEmanet) {
      if (!this.isSelectedCustomerSahis()) {
        this.snackBar.open('Emanet sepeti için şahıs cari seçin.', 'Tamam', { duration: 5000 });
        return;
      }
      const mode = header.sahisEmanetMode;
      const snap = this.itemsSnapshot();
      if (mode === 1 && snap.some((i) => Number(i['direction']) !== 0)) {
        this.snackBar.open('Emanet satış: tüm kalemler satış olmalı.', 'Tamam', { duration: 5000 });
        return;
      }
      if (mode === 2 && snap.some((i) => Number(i['direction']) !== 1)) {
        this.snackBar.open('Emanet alış: tüm kalemler alış olmalı.', 'Tamam', { duration: 5000 });
        return;
      }
    }
    const items: BasketItemCreate[] = this.itemsArray.controls.map((c) => {
      const v = c.getRawValue();
      const rawId = v.itemId as string | null | undefined;
      const id =
        this.editMode() && rawId && String(rawId).trim().length > 0
          ? String(rawId).trim()
          : undefined;
      const isPurchase = v.direction === 1;
      const mode = v.priceInputMode as PriceInputMode;
      const unit = this.parseNum(v.price);
      const lt = this.parseNum(v.lineTotalTl);
      return {
        ...(id ? { id } : {}),
        direction: v.direction,
        quantity: v.quantity,
        milyem: v.milyem,
        pieceCount: isPurchase ? undefined : v.pieceCount && v.pieceCount > 0 ? v.pieceCount : undefined,
        unitLabour: isPurchase ? undefined : this.parseNum(v.unitLabour) || undefined,
        price: mode === 'unit' ? (unit || undefined) : undefined,
        lineTotal: mode === 'total' ? lt : undefined,
        description: v.description || undefined,
        productTemplateId: (v.productTemplateId as string | null) || undefined,
        paymentCurrency: ((): PaymentCurrency => {
          const pc = Math.round(Number(v.paymentCurrency ?? 0));
          if (pc === 1) return 1;
          if (pc === 2) return 2;
          if (pc === 3) return 3;
          return 0;
        })(),
      };
    });

    const emanet = header.isSahisEmanet && this.isSelectedCustomerSahis();
    const dto: BasketCreate = {
      transactionDate: new Date(header.transactionDate).toISOString(),
      description: header.description || undefined,
      customerId: header.customerId || undefined,
      items,
      isSahisEmanet: emanet,
      sahisEmanetMode: emanet ? header.sahisEmanetMode : 0,
      kasaHareketli: header.kasaHareketli,
    };

    this.saving.set(true);
    const op = this.editMode() && this.transactionId()
      ? this.transactionsApi.update(this.transactionId()!, dto)
      : this.transactionsApi.create(dto);

    op.subscribe({
      next: async (saved) => {
        this.saving.set(false);
        try {
          await this.thermalReceipt.openReceipt(saved);
        } catch (err) {
          console.error(err);
          this.snackBar.open('Fiş PDF açılamadı; kayıt tamamlandı.', 'Tamam', { duration: 6000 });
        }
        this.router.navigate(['/transactions']);
      },
      error: () => {
        this.saving.set(false);
        this.snackBar.open('Kayıt başarısız; sepet korundu. Tekrar deneyebilirsiniz.', 'Tamam', { duration: 7000 });
      },
    });
  }
}
