import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export type TransactionDirection = 0 | 1; // 0 Sale, 1 Purchase

export interface Transaction {
  id: string;
  transactionDate: string;
  direction: TransactionDirection;
  quantity: number;
  milyem: number;
  pieceCount?: number;
  unitLabour?: number;
  totalLabour: number;
  hasGram: number;
  price?: number;
  description?: string;
  milyemLabour: number;
  customerId?: string;
  customer?: { id: string; name: string };
  createdAt: string;
}

export interface TransactionCreate {
  transactionDate: string;
  direction: TransactionDirection;
  quantity: number;
  milyem: number;
  pieceCount?: number;
  unitLabour?: number;
  price?: number;
  description?: string;
  customerId?: string;
}

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  constructor(private api: ApiService) {}

  getAll(params?: { from?: string; to?: string }): Observable<Transaction[]> {
    const q: Record<string, string> = {};
    if (params?.from) q['from'] = params.from;
    if (params?.to) q['to'] = params.to;
    return this.api.get<Transaction[]>('transactions', q);
  }

  getById(id: string): Observable<Transaction> {
    return this.api.get<Transaction>(`transactions/${id}`);
  }

  create(dto: TransactionCreate): Observable<Transaction> {
    return this.api.post<Transaction>('transactions', dto);
  }

  update(id: string, dto: TransactionCreate): Observable<Transaction> {
    return this.api.put<Transaction>(`transactions/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.api.delete<void>(`transactions/${id}`);
  }
}
