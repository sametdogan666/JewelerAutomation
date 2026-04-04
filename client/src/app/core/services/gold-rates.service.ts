import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class GoldRatesService {
  constructor(private api: ApiService) {}

  setManualDayRate(hasTryPerGramMid: number, usdTryMid?: number | null): Observable<{ effectiveDate: string }> {
    return this.api.post<{ effectiveDate: string }>('gold-rates/manual', {
      hasTryPerGramMid,
      usdTryMid: usdTryMid ?? null,
    });
  }
}
