import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
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
  ],
  templateUrl: './transaction-form.component.html',
  styleUrl: './transaction-form.component.scss',
})
export class TransactionFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private transactionsApi = inject(TransactionsService);
  private customersApi = inject(CustomersService);
  private router = inject(Router);

  customers = signal<Customer[]>([]);
  saving = signal(false);

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
    this.customersApi.getAll().subscribe((list) => this.customers.set(list));
    this.form.valueChanges.subscribe(() => this.formSnapshot.set(this.form.getRawValue()));
  }

  get isSale(): boolean {
    return this.form.get('direction')?.value === 0;
  }

  onSubmit(): void {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const dto: TransactionCreate = {
      transactionDate: new Date(v.transactionDate).toISOString(),
      direction: v.direction,
      quantity: v.quantity,
      milyem: v.milyem,
      pieceCount: v.pieceCount && v.pieceCount > 0 ? v.pieceCount : undefined,
      unitLabour: v.unitLabour && v.unitLabour !== 0 ? v.unitLabour : undefined,
      price: v.price != null && v.price !== 0 ? v.price : undefined,
      description: v.description || undefined,
      customerId: v.customerId || undefined,
    };
    this.saving.set(true);
    this.transactionsApi.create(dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/transactions']);
      },
      error: () => this.saving.set(false),
    });
  }
}
