import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

/** GET api/dashboard/summary ile uyumlu (camelCase). */
export interface DashboardSummary {
  netGoldCapitalHasGram: number;
  totalGoldInSafe: number;
  /** Defter nakit (TL). */
  totalCashInSafe: number;
  totalCashInSafeUsd: number;
  totalCashInSafeEur: number;
  totalCashInSafeGbp: number;
  totalCustomerGoldDebt: number;
  totalCustomerGoldReceivable: number;
  totalPersonalGoldDebt: number;
  totalPersonalGoldReceivable: number;
  sahisGoldLiabilitiesHasGram: number;
  netPhysicalEquityHasGram: number;
  physicalGoldBalance: number;
  physicalCashBalance: number;
  physicalCashBalanceUsd: number;
  physicalCashBalanceEur: number;
  physicalCashBalanceGbp: number;
  netGoldPositionHasGram: number;
  netCashPositionTl: number;
  netCashPositionUsd: number;
  netCashPositionEur: number;
  netCashPositionGbp: number;
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
  /** Kronolojik kümülatif fiziki has (manuel + sepet hareketleri). */
  physicalVaultHistory?: { at: string; cumulativeHasGram: number }[];
}

/** Boş / hatalı cevapta mor kutuların 0 ile dolması için taban özet. */
export function createDefaultDashboardSummary(): DashboardSummary {
  return {
    netGoldCapitalHasGram: 0,
    totalGoldInSafe: 0,
    totalCashInSafe: 0,
    totalCashInSafeUsd: 0,
    totalCashInSafeEur: 0,
    totalCashInSafeGbp: 0,
    totalCustomerGoldDebt: 0,
    totalCustomerGoldReceivable: 0,
    totalPersonalGoldDebt: 0,
    totalPersonalGoldReceivable: 0,
    sahisGoldLiabilitiesHasGram: 0,
    netPhysicalEquityHasGram: 0,
    physicalGoldBalance: 0,
    physicalCashBalance: 0,
    physicalCashBalanceUsd: 0,
    physicalCashBalanceEur: 0,
    physicalCashBalanceGbp: 0,
    netGoldPositionHasGram: 0,
    netCashPositionTl: 0,
    netCashPositionUsd: 0,
    netCashPositionEur: 0,
    netCashPositionGbp: 0,
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
    physicalVaultHistory: [],
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
    totalCashInSafeUsd: raw.totalCashInSafeUsd ?? 0,
    totalCashInSafeEur: raw.totalCashInSafeEur ?? 0,
    totalCashInSafeGbp: raw.totalCashInSafeGbp ?? 0,
    totalCustomerGoldDebt: raw.totalCustomerGoldDebt ?? 0,
    totalCustomerGoldReceivable: raw.totalCustomerGoldReceivable ?? 0,
    totalPersonalGoldDebt: raw.totalPersonalGoldDebt ?? 0,
    totalPersonalGoldReceivable: raw.totalPersonalGoldReceivable ?? 0,
    sahisGoldLiabilitiesHasGram: raw.sahisGoldLiabilitiesHasGram ?? 0,
    netPhysicalEquityHasGram: raw.netPhysicalEquityHasGram ?? 0,
    physicalGoldBalance: raw.physicalGoldBalance ?? 0,
    physicalCashBalance: raw.physicalCashBalance ?? 0,
    physicalCashBalanceUsd: raw.physicalCashBalanceUsd ?? 0,
    physicalCashBalanceEur: raw.physicalCashBalanceEur ?? 0,
    physicalCashBalanceGbp: raw.physicalCashBalanceGbp ?? 0,
    netGoldPositionHasGram: raw.netGoldPositionHasGram ?? 0,
    netCashPositionTl: raw.netCashPositionTl ?? 0,
    netCashPositionUsd: raw.netCashPositionUsd ?? 0,
    netCashPositionEur: raw.netCashPositionEur ?? 0,
    netCashPositionGbp: raw.netCashPositionGbp ?? 0,
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
    physicalVaultHistory: Array.isArray(raw.physicalVaultHistory) ? raw.physicalVaultHistory : [],
  };
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private api: ApiService) {}

  getSummary(): Observable<DashboardSummary> {
    return this.api.get<DashboardSummary>('dashboard/summary');
  }
}
