import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CustomersService, CustomerCreate } from '../../core/services/customers.service';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [
    NgIf,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './customer-form.component.html',
  styleUrl: './customer-form.component.scss',
})
export class CustomerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private api = inject(CustomersService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  isEdit = signal(false);
  id = signal<string | null>(null);
  loading = signal(false);
  saving = signal(false);

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(256)]],
    phone: [''],
    address: [''],
    type: [0 as 0 | 1, Validators.required],
    description: [''],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.id.set(id);
      this.isEdit.set(true);
      this.loading.set(true);
      this.api.getById(id).subscribe({
        next: (c) => {
          this.form.patchValue({
            name: c.name,
            phone: c.phone ?? '',
            address: c.address ?? '',
            type: c.type,
            description: c.description ?? '',
          });
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid || this.saving()) return;
    const v = this.form.getRawValue();
    const dto: CustomerCreate = {
      name: v.name,
      phone: v.phone || undefined,
      address: v.address || undefined,
      type: v.type,
      description: v.description || undefined,
    };
    this.saving.set(true);
    const id = this.id();
    if (id) {
      this.api.update(id, dto).subscribe({
        next: () => { this.saving.set(false); this.router.navigate(['/customers']); },
        error: () => this.saving.set(false),
      });
    } else {
      this.api.create(dto).subscribe({
        next: () => { this.saving.set(false); this.router.navigate(['/customers']); },
        error: () => this.saving.set(false),
      });
    }
  }
}
