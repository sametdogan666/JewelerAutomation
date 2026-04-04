import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export type TransactionDirection = 0 | 1; // 0 Sale, 1 Purchase

export interface TransactionItem {
  id: string;
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
  productTemplateId?: string | null;
}

export interface Transaction {
  id: string;
  transactionDate: string;
  direction: TransactionDirection;
  netHasGram: number;
  netCashAmount: number;
  hasGram: number;
  price?: number;
  /** Nakit bağlama: bağlanan TL (pozitif tutar, API). */
  cashAmount?: number | null;
  /** Nakit bağlama: karşılık has gr (API). */
  equivalentHasGram?: number | null;
  description?: string;
  customerId?: string;
  customerName?: string;
  correlationId?: string;
  createdAt: string;
  items: TransactionItem[];
}

export interface BasketItemCreate {
  /** Düzenlemede mevcut satırı eşlemek için (yeni satırda gönderilmez). */
  id?: string;
  direction: TransactionDirection;
  quantity: number;
  milyem: number;
  pieceCount?: number;
  unitLabour?: number;
  /** Has başına TL; birim modunda kullanılır. */
  price?: number;
  /** Doğrudan satır toplamı TL (hesap makinesi); gönderilirse API bunu nakit olarak alır. */
  lineTotal?: number;
  description?: string;
  productTemplateId?: string | null;
}

export interface BasketCreate {
  transactionDate: string;
  description?: string;
  customerId?: string;
  items: BasketItemCreate[];
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

  create(dto: BasketCreate): Observable<Transaction> {
    return this.api.post<Transaction>('transactions', dto);
  }

  update(id: string, dto: BasketCreate): Observable<Transaction> {
    return this.api.put<Transaction>(`transactions/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`transactions/${id}`);
  }
}
