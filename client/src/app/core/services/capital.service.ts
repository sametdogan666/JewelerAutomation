import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface CapitalSummary {
  totalGoldInSafe: number;
  totalCashInSafe: number;
  totalCustomerGoldDebt: number;
  totalCustomerGoldReceivable: number;
  netGoldCapital: number;
}

@Injectable({ providedIn: 'root' })
export class CapitalService {
  constructor(private api: ApiService) {}

  getSummary(): Observable<CapitalSummary> {
    return this.api.get<CapitalSummary>('capital/summary');
  }
}
