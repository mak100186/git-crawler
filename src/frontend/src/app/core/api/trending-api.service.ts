import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { TrendDto } from '../models/trend.model';

@Injectable({ providedIn: 'root' })
export class TrendingApiService {
  private readonly http = inject(HttpClient);

  getTrending(): Observable<{ trends: TrendDto[] }> {
    return this.http.get<{ trends: TrendDto[] }>('/api/trending');
  }
}
