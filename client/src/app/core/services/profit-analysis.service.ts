import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PeggingSimulation {
  periodCashBalance: number;
  goldBalanceInSafe: number;
  cashEquivalentHasGram: number;
  totalSalesHasGram: number;
  totalPurchasesHasGram: number;
  transactionProfitHasGram: number;
  netProfitHasGram: number;
  netProfitTL: number;
}

export interface CashPeggingLog {
  id: string;
  peggingDate: string;
  cashAmount: number;
  goldPricePerGram: number;
  equivalentHasGram: number;
  physicalGoldAtTime: number;
  totalCapitalHasGram: number;
  periodStartDate: string;
  periodEndDate: string;
  netProfitHasGram: number;
  notes?: string;
}

export interface TransactionDetail {
  id: string;
  date: string;
  direction: string;
  quantity: number;
  milyem: number;
  hasGram: number;
  price: number;
  cashImpact: number;
  customerName?: string;
  description?: string;
}

export interface PeriodSummary {
  periodStart: string;
  periodEnd: string;
  transactions: TransactionDetail[];
  totalPurchasesHasGram: number;
  totalSalesHasGram: number;
  totalPurchasesCash: number;
  totalSalesCash: number;
  netCashChange: number;
  netGoldChange: number;
}

export interface SimulatePeggingRequest {
  periodStart: string;
  periodEnd: string;
  goldPricePerGram: number;
}

export interface CreatePeggingRequest {
  periodStart: string;
  periodEnd: string;
  goldPricePerGram: number;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class ProfitAnalysisService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/profit-analysis';

  simulatePegging(request: SimulatePeggingRequest): Observable<PeggingSimulation> {
    return this.http.post<PeggingSimulation>(`${this.baseUrl}/simulate`, request);
  }

  pegCash(request: CreatePeggingRequest): Observable<CashPeggingLog> {
    return this.http.post<CashPeggingLog>(`${this.baseUrl}/peg-cash`, request);
  }

  getPeggingHistory(from?: string, to?: string): Observable<CashPeggingLog[]> {
    const params: any = {};
    if (from) params.from = from;
    if (to) params.to = to;
    return this.http.get<CashPeggingLog[]>(`${this.baseUrl}/pegging-history`, { params });
  }

  getLatestPegging(): Observable<CashPeggingLog> {
    return this.http.get<CashPeggingLog>(`${this.baseUrl}/latest-pegging`);
  }

  getPeriodSummary(periodStart: string, periodEnd: string): Observable<PeriodSummary> {
    return this.http.get<PeriodSummary>(`${this.baseUrl}/period-summary`, {
      params: { periodStart, periodEnd }
    });
  }
}
