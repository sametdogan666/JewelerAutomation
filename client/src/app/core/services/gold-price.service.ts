import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

export interface GoldPrice {
  selling: number; // Satış fiyatı
  buying: number;  // Alış fiyatı
  updated: Date;
}

@Injectable({ providedIn: 'root' })
export class GoldPriceService {
  
  /**
   * Haremaltin.com'dan canlı has altın fiyatını çeker
   * Not: CORS sorunu nedeniyle şimdilik mock data kullanıyoruz
   * Gerçek implementasyon için backend'den proxy gerekir
   */
  getCurrentPrice(): Observable<GoldPrice> {
    // TODO: Backend'e API endpoint ekleyerek haremaltin.com'dan veri çek
    // Şimdilik ortalama bir fiyat döndürüyoruz
    const mockPrice: GoldPrice = {
      selling: 7000, // Has altın satış fiyatı (TL/gram)
      buying: 6900,
      updated: new Date()
    };
    
    return of(mockPrice);
  }
  
  /**
   * Backend üzerinden has altın fiyatını çek (CORS bypass)
   */
  getCurrentPriceViaBackend(): Observable<GoldPrice> {
    // Backend'de /api/gold-price endpoint'i oluşturulmalı
    // O endpoint haremaltin.com'a istek atıp veriyi döndürmeli
    return this.getCurrentPrice(); // Şimdilik mock
  }
}
