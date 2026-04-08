import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/** API CashCurrency: 1=USD, 2=EUR, 3=GBP */
export type ForexBaseCurrencyCode = 1 | 2 | 3;

export interface ForexBorsaRequest {
  transactionDate: string;
  baseCurrency: ForexBaseCurrencyCode;
  isBuy: boolean;
  amountBase: number;
  rateTryPerUnit: number;
  description?: string;
}

export interface ForexBorsaResponse {
  transactionId: string;
}

@Injectable({ providedIn: 'root' })
export class CurrencyExchangeService {
  constructor(private api: ApiService) {}

  /** Borsa: döviz ↔ TRY; işlem listesine düşer. */
  createForexTrade(dto: ForexBorsaRequest): Observable<ForexBorsaResponse> {
    return this.api.post<ForexBorsaResponse>('currency-exchange', dto);
  }
}
