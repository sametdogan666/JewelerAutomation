import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface SafeMovement {
  id: string;
  transactionDate: string;
  gram: number;
  milyem: number;
  hasGram: number;
  description?: string;
  movementType: 0 | 1 | 2 | 3; // Income, Expense, Capital, Transfer
  createdAt: string;
}

export interface SafeMovementCreate {
  transactionDate: string;
  gram: number;
  milyem: number;
  description?: string;
  movementType: 0 | 1 | 2 | 3;
}

export interface SafeStatus {
  physicalGoldBalance: number;
  physicalCashBalance: number;
  expectedGold: number;
  goldGapOrSurplus: number;
  customerGoldDebt: number;
  customerGoldReceivable: number;
  personalGoldDebt: number;
  personalGoldReceivable: number;
  netGoldPosition: number;
  netCashPosition: number;
  profitHasGram: number;
}

@Injectable({ providedIn: 'root' })
export class SafeService {
  constructor(private api: ApiService) {}

  getBalance(): Observable<number> {
    return this.api.get<number>('safe/balance');
  }

  getMovements(): Observable<SafeMovement[]> {
    return this.api.get<SafeMovement[]>('safe/movements');
  }

  addMovement(dto: SafeMovementCreate): Observable<SafeMovement> {
    return this.api.post<SafeMovement>('safe/movements', dto);
  }

  updateMovement(id: string, dto: SafeMovementCreate): Observable<SafeMovement> {
    return this.api.put<SafeMovement>(`safe/movements/${id}`, dto);
  }

  deleteMovement(id: string): Observable<void> {
    return this.api.delete(`safe/movements/${id}`);
  }

  getStatus(): Observable<SafeStatus> {
    return this.api.get<SafeStatus>('safe/status');
  }
}
