import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface AccountingProfit {
  initialCapitalHasGram: number;
  currentGoldInSafeHasGram: number;
  currentCashBalanceTL: number;
  cashEquivalentHasGram: number;
  netCapitalHasGram: number;
  netProfitHasGram: number;
  goldPriceUsed: number;
}

export interface CashBalance {
  totalSalesCash: number;
  totalPurchasesCash: number;
  netCashBalance: number;
}

@Injectable({ providedIn: 'root' })
export class AccountingService {
  constructor(private api: ApiService) {}

  calculateProfit(goldPrice: number, startDate?: string, endDate?: string): Observable<AccountingProfit> {
    const params: Record<string, string | number> = { goldPrice };
    if (startDate) params['startDate'] = startDate;
    if (endDate) params['endDate'] = endDate;
    return this.api.get<AccountingProfit>('accounting/profit', params);
  }

  getInitialCapital(): Observable<number> {
    return this.api.get<number>('accounting/initial-capital');
  }

  getCashBalance(): Observable<CashBalance> {
    return this.api.get<CashBalance>('accounting/cash-balance');
  }
}
