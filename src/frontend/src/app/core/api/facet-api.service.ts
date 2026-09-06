import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { FacetOptionsDto } from '../models/facet.model';

@Injectable({ providedIn: 'root' })
export class FacetApiService {
  private readonly http = inject(HttpClient);

  getFacetOptions(): Observable<FacetOptionsDto> {
    return this.http.get<FacetOptionsDto>('/api/facets');
  }
}
