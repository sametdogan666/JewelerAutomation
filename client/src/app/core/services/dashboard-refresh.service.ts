import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DashboardRefreshService {
  private _refresh$ = new Subject<void>();
  refresh$ = this._refresh$.asObservable();

  triggerRefresh(): void {
    this._refresh$.next();
  }
}
