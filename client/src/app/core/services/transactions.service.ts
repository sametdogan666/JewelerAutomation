import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export type TransactionDirection = 0 | 1; // 0 Sale, 1 Purchase

/** API: 0=TL, 1=USD, 2=EUR, 3=GBP */
export type PaymentCurrency = 0 | 1 | 2 | 3;

/** 0=sepet, 1=döviz (Borsa) */
export type TransactionKind = 0 | 1;

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
  paymentCurrency?: PaymentCurrency;
}

export interface Transaction {
  id: string;
  transactionDate: string;
  kind?: TransactionKind;
  direction: TransactionDirection;
  isSahisEmanet?: boolean;
  /** 1=emanet satış, 2=emanet alış */
  sahisEmanetMode?: number;
  kasaHareketli?: boolean;
  netHasGram: number;
  netCashAmount: number;
  netCashAmountUsd?: number;
  netCashAmountEur?: number;
  netCashAmountGbp?: number;
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
  forexBaseCurrency?: number;
  forexIsBuy?: boolean;
  forexAmountBase?: number;
  forexRateTryPerUnit?: number;
  forexCounterTry?: number;
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
  /** 0=TL, 1=USD, 2=EUR */
  paymentCurrency?: PaymentCurrency;
}

export interface BasketCreate {
  transactionDate: string;
  description?: string;
  customerId?: string;
  items: BasketItemCreate[];
  isSahisEmanet?: boolean;
  sahisEmanetMode?: number;
  kasaHareketli?: boolean;
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
