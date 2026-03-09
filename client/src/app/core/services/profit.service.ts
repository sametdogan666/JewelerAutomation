import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface ProfitSummary {
  totalGoldSalesHas: number;
  totalGoldPurchasesHas: number;
  netProfitHas: number;
  startDate: string;
  endDate: string;
}

@Injectable({ providedIn: 'root' })
export class ProfitService {
  constructor(private api: ApiService) {}

  calculate(startDate: string, endDate: string): Observable<ProfitSummary> {
    return this.api.get<ProfitSummary>(`profit/calculate?startDate=${startDate}&endDate=${endDate}`);
  }
}
