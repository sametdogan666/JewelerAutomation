import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export type CustomerTransactionType = 0 | 1 | 2 | 3; // GoldPurchase, GoldSale, CashPayment, CashCollection

export interface CustomerBalance {
  customerId: string;
  customerName: string;
  goldBalance: number;
  cashBalance: number;
}

export interface CustomerTransactionDto {
  id: string;
  transactionDate: string;
  transactionType: CustomerTransactionType;
  goldGram: number;
  goldMilyem: number;
  goldHas: number;
  cashAmount: number;
  description?: string;
}

export interface CreateCustomerTransactionRequest {
  transactionDate: string;
  transactionType: CustomerTransactionType;
  goldGram: number;
  goldMilyem: number;
  goldHas: number;
  cashAmount: number;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class CustomerAccountService {
  constructor(private api: ApiService) {}

  getBalance(customerId: string): Observable<CustomerBalance> {
    return this.api.get<CustomerBalance>(`customers/${customerId}/account/balance`);
  }

  getStatement(customerId: string, from?: string, to?: string): Observable<CustomerTransactionDto[]> {
    let path = `customers/${customerId}/account/statement`;
    const params: string[] = [];
    if (from) params.push(`from=${encodeURIComponent(from)}`);
    if (to) params.push(`to=${encodeURIComponent(to)}`);
    if (params.length) path += '?' + params.join('&');
    return this.api.get<CustomerTransactionDto[]>(path);
  }

  createTransaction(customerId: string, dto: CreateCustomerTransactionRequest): Observable<CustomerTransactionDto> {
    return this.api.post<CustomerTransactionDto>(`customers/${customerId}/account/transactions`, dto);
  }
}
