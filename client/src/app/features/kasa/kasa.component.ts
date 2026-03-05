import { Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SafeService, SafeMovement, SafeMovementCreate } from '../../core/services/safe.service';
import { NgIf, DecimalPipe } from '@angular/common';

const MOVEMENT_TYPES: { value: 0 | 1 | 2 | 3; label: string }[] = [
  { value: 0, label: 'Gelir' },
  { value: 1, label: 'Gider' },
  { value: 2, label: 'Ana sermaye' },
  { value: 3, label: 'Transfer' },
];

@Component({
  selector: 'app-kasa',
  standalone: true,
  imports: [
    NgIf,
    DecimalPipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
  ],
  templateUrl: './kasa.component.html',
  styleUrl: './kasa.component.scss',
})
export class KasaComponent implements OnInit {
  private api = inject(SafeService);
  private fb = inject(FormBuilder);

  balance = signal<number | null>(null);
  dataSource = new MatTableDataSource<SafeMovement>([]);
  loading = signal(true);
  saving = signal(false);
  showForm = signal(false);
  movementTypes = MOVEMENT_TYPES;

  displayedColumns = ['transactionDate', 'gram', 'milyem', 'hasGram', 'movementType', 'description'];

  form = this.fb.nonNullable.group({
    transactionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    gram: [0, [Validators.required, Validators.min(-999999), Validators.max(999999)]],
    milyem: [1000, [Validators.required, Validators.min(0), Validators.max(1000)]],
    description: [''],
    movementType: [2 as 0 | 1 | 2 | 3, Validators.required],
  });

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.doneCount = 0;
    this.api.getBalance().subscribe({
      next: (v) => { this.balance.set(v); this.done(); },
      error: () => this.done(),
    });
    this.api.getMovements().subscribe({
      next: (list) => { this.dataSource.data = list; this.done(); },
      error: () => this.done(),
    });
  }

  private doneCount = 0;
  private done(): void {
    this.doneCount++;
    if (this.doneCount >= 2) this.loading.set(false);
  }

  openForm(): void {
    this.showForm.set(true);
    this.form.reset({
      transactionDate: new Date().toISOString().slice(0, 10),
      gram: 0,
      milyem: 1000,
      description: '',
      movementType: 2,
    });
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  onSubmit(): void {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const dto: SafeMovementCreate = {
      transactionDate: new Date(v.transactionDate).toISOString(),
      gram: v.gram,
      milyem: v.milyem,
      description: v.description || undefined,
      movementType: v.movementType,
    };
    this.saving.set(true);
    this.api.addMovement(dto).subscribe({
      next: () => {
        this.saving.set(false);
        this.closeForm();
        this.refresh();
      },
      error: () => this.saving.set(false),
    });
  }

  movementTypeLabel(value: 0 | 1 | 2 | 3): string {
    return MOVEMENT_TYPES.find((t) => t.value === value)?.label ?? '';
  }

  formatDate(s: string): string {
    return new Date(s).toLocaleDateString('tr-TR');
  }
}
