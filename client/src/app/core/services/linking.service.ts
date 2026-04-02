import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FifoLinkingSimulation {
  targetAmountGram: number;
  targetPricePerGram: number;
  estimatedProfitTl: number;
  openHasPositionGram: number;
  sufficientOpenPosition: boolean;
}

export interface LinkingProcessResult {
  id: string;
  linkingDate: string;
  targetAmount: number;
  targetPrice: number;
  totalProfit: number;
  safeMovementId?: string | null;
  notes?: string | null;
}

export interface LinkingProcessListItem {
  id: string;
  linkingDate: string;
  targetAmount: number;
  targetPrice: number;
  totalProfit: number;
  safeMovementId?: string | null;
  notes?: string | null;
  /** "Fifo" | "Hybrid" (dönem nakit bağlama) */
  kind?: string;
  periodStartDate?: string | null;
  periodEndDate?: string | null;
  cashAmount?: number | null;
  netProfitHasGram?: number | null;
}

export interface LinkingProcessRequest {
  targetAmountGram: number;
  targetPricePerGram: number;
  notes?: string;
  periodStart?: string | null;
  periodEnd?: string | null;
}

@Injectable({ providedIn: 'root' })
export class LinkingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/linking';

  getOpenPosition(periodStart?: string, periodEnd?: string): Observable<number> {
    const params: Record<string, string> = {};
    if (periodStart) params['periodStart'] = periodStart;
    if (periodEnd) params['periodEnd'] = periodEnd;
    return this.http.get<number>(`${this.baseUrl}/open-position`, { params });
  }

  simulate(body: LinkingProcessRequest) {
    return this.http.post<FifoLinkingSimulation>(`${this.baseUrl}/simulate`, body);
  }

  process(body: LinkingProcessRequest) {
    return this.http.post<LinkingProcessResult>(`${this.baseUrl}/process`, body);
  }

  getHistory() {
    return this.http.get<LinkingProcessListItem[]>(`${this.baseUrl}/history`);
  }

  cancel(id: string) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }
}
