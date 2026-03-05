import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatRadioModule } from '@angular/material/radio';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DecimalPipe } from '@angular/common';
import { NgxMaskDirective } from 'ngx-mask';
import { TransactionsService, TransactionCreate, TransactionDirection } from '../../core/services/transactions.service';
import { CustomersService, Customer } from '../../core/services/customers.service';

const MILYEM_FACTOR = 0.001;
const LABOUR_FACTOR = 0.01;

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
    DecimalPipe,
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

  form = this.fb.nonNullable.group({
    direction: [0 as TransactionDirection, Validators.required],
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    quantity: [0, [Validators.required, Validators.min(0.001)]],
    milyem: [916, [Validators.required, Validators.min(0), Validators.max(1000)]],
    pieceCount: [0],
    unitLabour: [0],
    price: [0 as number | null],
    description: [''],
    customerId: [null as string | null],
  });

  /** Snapshot of form values for reactive preview (updated on valueChanges) */
  formSnapshot = signal(this.form.getRawValue());

  /** Live preview: Has Gram from purity (quantity * milyem / 1000) */
  hasFromPurity = computed(() => {
    const s = this.formSnapshot();
    const q = s.quantity ?? 0;
    const m = s.milyem ?? 0;
    return Math.round((q * m * MILYEM_FACTOR) * 1e6) / 1e6;
  });

  /** Live preview: Total labour (sale only, negative) */
  totalLabourPreview = computed(() => {
    const s = this.formSnapshot();
    if (s.direction !== 0) return 0;
    const pc = s.pieceCount ?? 0;
    const ul = s.unitLabour ?? 0;
    const labour = pc * ul * LABOUR_FACTOR;
    return Math.round(-labour * 1e6) / 1e6;
  });

  /** Live preview: Final Has Gram (with labour for sale) */
  hasGramPreview = computed(() => {
    const base = this.hasFromPurity();
    const labour = this.totalLabourPreview();
    return Math.round((base + labour) * 1e6) / 1e6;
  });

  /** Whether form has valid numbers for preview */
  canShowPreview = computed(() => {
    const s = this.formSnapshot();
    const q = s.quantity ?? 0;
    const m = s.milyem ?? 0;
    return q > 0 && m >= 0 && m <= 1000;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.editMode.set(true);
      this.transactionId.set(id);
      this.transactionsApi.getById(id).subscribe({
        next: (transaction) => {
          this.form.patchValue({
            direction: transaction.direction,
            transactionDate: new Date(transaction.transactionDate).toISOString().slice(0, 10),
            quantity: transaction.quantity,
            milyem: transaction.milyem,
            pieceCount: transaction.pieceCount ?? 0,
            unitLabour: transaction.unitLabour ?? 0,
            price: transaction.price ?? 0,
            description: transaction.description ?? '',
            customerId: transaction.customerId ?? null,
          });
          this.formSnapshot.set(this.form.getRawValue());
        },
      });
    }
    this.customersApi.getAll().subscribe((list) => this.customers.set(list));
    this.form.valueChanges.subscribe(() => this.formSnapshot.set(this.form.getRawValue()));
  }

  get isSale(): boolean {
    return this.form.get('direction')?.value === 0;
  }

  onSubmit(): void {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    
    // Parse masked values (ngx-mask returns string with dots, convert to number)
    const parsePrice = (val: any): number | undefined => {
      if (val == null || val === '' || val === 0) return undefined;
      const str = String(val).replace(/\./g, '').replace(/,/g, '.');
      const num = parseFloat(str);
      return isNaN(num) || num === 0 ? undefined : num;
    };

    const dto: TransactionCreate = {
      transactionDate: new Date(v.transactionDate).toISOString(),
      direction: v.direction,
      quantity: v.quantity,
      milyem: v.milyem,
      pieceCount: v.pieceCount && v.pieceCount > 0 ? v.pieceCount : undefined,
      unitLabour: parsePrice(v.unitLabour),
      price: parsePrice(v.price),
      description: v.description || undefined,
      customerId: v.customerId || undefined,
    };
    
    this.saving.set(true);
    const operation = this.editMode() && this.transactionId()
      ? this.transactionsApi.update(this.transactionId()!, dto)
      : this.transactionsApi.create(dto);

    operation.subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/transactions']);
      },
      error: () => this.saving.set(false),
    });
  }
}
