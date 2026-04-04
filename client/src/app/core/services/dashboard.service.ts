import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/** GET api/dashboard/summary ile uyumlu (camelCase). */
export interface DashboardSummary {
  netGoldCapitalHasGram: number;
  totalGoldInSafe: number;
  totalCashInSafe: number;
  totalCustomerGoldDebt: number;
  totalCustomerGoldReceivable: number;
  totalPersonalGoldDebt: number;
  totalPersonalGoldReceivable: number;
  physicalGoldBalance: number;
  physicalCashBalance: number;
  netGoldPositionHasGram: number;
  netCashPositionTl: number;
  expectedGold: number;
  goldGapOrSurplus: number;
  profitHasGram: number;
  cumulativePeggingProfitHasGram: number;
  peggingCount: number;
  liveHasTryPerGramMid: number | null;
  liveUsdTryMid: number | null;
  ratesFetchedAtUtc: string | null;
  ratesAvailable: boolean;
  /** True when mid came from DailyGoldRates history (live API unavailable). */
  ratesFromHistoricalFallback?: boolean;
  /** True when configured placeholder rates are used (no live cache and no history). */
  ratesFromDefaultFallback?: boolean;
  ratesFromManualOverride?: boolean;
  netSermayeHasGramAtLivePrice: number | null;
  netGoldPositionTlApprox: number | null;
}

/** Boş / hatalı cevapta mor kutuların 0 ile dolması için taban özet. */
export function createDefaultDashboardSummary(): DashboardSummary {
  return {
    netGoldCapitalHasGram: 0,
    totalGoldInSafe: 0,
    totalCashInSafe: 0,
    totalCustomerGoldDebt: 0,
    totalCustomerGoldReceivable: 0,
    totalPersonalGoldDebt: 0,
    totalPersonalGoldReceivable: 0,
    physicalGoldBalance: 0,
    physicalCashBalance: 0,
    netGoldPositionHasGram: 0,
    netCashPositionTl: 0,
    expectedGold: 0,
    goldGapOrSurplus: 0,
    profitHasGram: 0,
    cumulativePeggingProfitHasGram: 0,
    peggingCount: 0,
    liveHasTryPerGramMid: null,
    liveUsdTryMid: null,
    ratesFetchedAtUtc: null,
    ratesAvailable: false,
    netSermayeHasGramAtLivePrice: null,
    netGoldPositionTlApprox: null,
  };
}

/** null, undefined veya boş cevap → varsayılan; eksik sayı alanları 0’a çekilir. */
export function normalizeDashboardSummary(raw: DashboardSummary | null | undefined): DashboardSummary {
  const z = createDefaultDashboardSummary();
  if (raw == null || typeof raw !== 'object') {
    return z;
  }
  return {
    netGoldCapitalHasGram: raw.netGoldCapitalHasGram ?? z.netGoldCapitalHasGram,
    totalGoldInSafe: raw.totalGoldInSafe ?? 0,
    totalCashInSafe: raw.totalCashInSafe ?? 0,
    totalCustomerGoldDebt: raw.totalCustomerGoldDebt ?? 0,
    totalCustomerGoldReceivable: raw.totalCustomerGoldReceivable ?? 0,
    totalPersonalGoldDebt: raw.totalPersonalGoldDebt ?? 0,
    totalPersonalGoldReceivable: raw.totalPersonalGoldReceivable ?? 0,
    physicalGoldBalance: raw.physicalGoldBalance ?? 0,
    physicalCashBalance: raw.physicalCashBalance ?? 0,
    netGoldPositionHasGram: raw.netGoldPositionHasGram ?? 0,
    netCashPositionTl: raw.netCashPositionTl ?? 0,
    expectedGold: raw.expectedGold ?? 0,
    goldGapOrSurplus: raw.goldGapOrSurplus ?? 0,
    profitHasGram: raw.profitHasGram ?? 0,
    cumulativePeggingProfitHasGram: raw.cumulativePeggingProfitHasGram ?? 0,
    peggingCount: raw.peggingCount ?? 0,
    liveHasTryPerGramMid: raw.liveHasTryPerGramMid ?? null,
    liveUsdTryMid: raw.liveUsdTryMid ?? null,
    ratesFetchedAtUtc: raw.ratesFetchedAtUtc ?? null,
    ratesAvailable: raw.ratesAvailable ?? false,
    ratesFromHistoricalFallback: raw.ratesFromHistoricalFallback,
    ratesFromDefaultFallback: raw.ratesFromDefaultFallback,
    ratesFromManualOverride: raw.ratesFromManualOverride,
    netSermayeHasGramAtLivePrice: raw.netSermayeHasGramAtLivePrice ?? null,
    netGoldPositionTlApprox: raw.netGoldPositionTlApprox ?? null,
  };
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private api: ApiService) {}

  getSummary(): Observable<DashboardSummary> {
    return this.api.get<DashboardSummary>('dashboard/summary');
  }
}
