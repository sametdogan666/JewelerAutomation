import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface Customer {
  id: string;
  name: string;
  phone?: string;
  address?: string;
  type: 0 | 1; // 0 Cari, 1 Sahis
  description?: string;
  createdAt: string;
}

export interface CustomerCreate {
  name: string;
  phone?: string;
  address?: string;
  type: 0 | 1;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class CustomersService {
  constructor(private api: ApiService) {}

  getAll(): Observable<Customer[]> {
    return this.api.get<Customer[]>('customers');
  }

  getById(id: string): Observable<Customer> {
    return this.api.get<Customer>(`customers/${id}`);
  }

  create(dto: CustomerCreate): Observable<Customer> {
    return this.api.post<Customer>('customers', dto);
  }

  update(id: string, dto: CustomerCreate): Observable<void> {
    return this.api.put<void>(`customers/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`customers/${id}`);
  }
}
