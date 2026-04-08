import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export type CustomerTransactionType = 0 | 1 | 2 | 3 | 20 | 21;

export interface CustomerBalance {
  customerId: string;
  customerName: string;
  goldBalance: number;
  cashBalanceTry: number;
  cashBalanceUsd: number;
  cashBalanceEur: number;
  cashBalanceGbp: number;
}

export interface CustomerTransactionDto {
  id: string;
  transactionDate: string;
  transactionType: CustomerTransactionType;
  goldGram: number;
  goldMilyem: number;
  goldHas: number;
  cashAmount: number;
  cashCurrency: number;
  postToLedger: boolean;
  openingAssetKind: number | null;
  openingCustomerIsCreditor: boolean | null;
  sourceBasketTransactionId: string | null;
  description?: string;
}

/** Hesap ekstresi satırı — sepet emanetleri tek grupta. */
export interface CustomerStatementEntryDto {
  entryId: string;
  primaryTransactionId: string | null;
  isBasketGroup: boolean;
  sourceBasketTransactionId: string | null;
  transactionDate: string;
  transactionType: CustomerTransactionType;
  totalGoldHas: number;
  sumGoldGram: number;
  displayMilyem: number | null;
  netCashTry: number;
  netCashUsd: number;
  netCashEur: number;
  netCashGbp: number;
  postToLedger: boolean;
  openingAssetKind: number | null;
  openingCustomerIsCreditor: boolean | null;
  description?: string;
  canDelete: boolean;
  canEdit: boolean;
  lineItems: CustomerTransactionDto[];
}

export interface CreateCustomerTransactionRequest {
  transactionDate: string;
  transactionType: CustomerTransactionType;
  goldGram: number;
  goldMilyem: number;
  goldHas: number;
  cashAmount: number;
  description?: string;
  cashCurrency?: number;
  postToLedger?: boolean;
}

/** Şahıs devir — API: SahisOpeningAssetKind 0..4 */
export interface SahisOpeningBalanceRequest {
  transactionDate: string;
  assetKind: number;
  amount: number;
  customerIsCreditor: boolean;
  description?: string;
}

@Injectable({ providedIn: 'root' })
export class CustomerAccountService {
  constructor(private api: ApiService) {}

  getBalance(customerId: string): Observable<CustomerBalance> {
    return this.api.get<CustomerBalance>(`customers/${customerId}/account/balance`);
  }

  getStatement(customerId: string, from?: string, to?: string): Observable<CustomerStatementEntryDto[]> {
    let path = `customers/${customerId}/account/statement`;
    const params: string[] = [];
    if (from) params.push(`from=${encodeURIComponent(from)}`);
    if (to) params.push(`to=${encodeURIComponent(to)}`);
    if (params.length) path += '?' + params.join('&');
    return this.api.get<CustomerStatementEntryDto[]>(path);
  }

  createTransaction(customerId: string, dto: CreateCustomerTransactionRequest): Observable<CustomerTransactionDto> {
    return this.api.post<CustomerTransactionDto>(`customers/${customerId}/account/transactions`, dto);
  }

  postSahisOpeningBalance(
    customerId: string,
    dto: SahisOpeningBalanceRequest
  ): Observable<CustomerTransactionDto> {
    return this.api.post<CustomerTransactionDto>(
      `customers/${customerId}/account/sahis/opening-balance`,
      {
        transactionDate: dto.transactionDate,
        assetKind: dto.assetKind,
        amount: dto.amount,
        customerIsCreditor: dto.customerIsCreditor,
        description: dto.description,
      }
    );
  }

  deleteTransaction(transactionId: string): Observable<void> {
    return this.api.delete(`/customer-transactions/${transactionId}`);
  }

  updateTransaction(transactionId: string, dto: CreateCustomerTransactionRequest): Observable<CustomerTransactionDto> {
    return this.api.put<CustomerTransactionDto>(`/customer-transactions/${transactionId}`, dto);
  }
}
