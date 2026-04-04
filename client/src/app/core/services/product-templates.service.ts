import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface ProductTemplate {
  id: string;
  name: string;
  milyemSatis: number;
  milyemAlis: number;
  defaultGram: number;
  defaultLaborPrice: number;
  category?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProductTemplateCreate {
  name: string;
  milyemSatis: number;
  milyemAlis: number;
  defaultGram: number;
  defaultLaborPrice: number;
  category?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ProductTemplatesService {
  constructor(private api: ApiService) {}

  getAll(): Observable<ProductTemplate[]> {
    return this.api.get<ProductTemplate[]>('producttemplates');
  }

  create(dto: ProductTemplateCreate): Observable<ProductTemplate> {
    return this.api.post<ProductTemplate>('producttemplates', dto);
  }

  update(id: string, dto: ProductTemplateCreate): Observable<ProductTemplate> {
    return this.api.put<ProductTemplate>(`producttemplates/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`producttemplates/${id}`);
  }
}
