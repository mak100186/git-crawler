import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CategoryDto } from '../models/category.model';

@Injectable({ providedIn: 'root' })
export class CategoryApiService {
  private readonly http = inject(HttpClient);

  getCategories(): Observable<{ categories: CategoryDto[] }> {
    return this.http.get<{ categories: CategoryDto[] }>('/api/categories');
  }
}
