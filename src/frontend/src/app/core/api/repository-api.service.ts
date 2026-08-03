import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  HiddenGemCardDto,
  PagedResult,
  RepositoryFilterCriteria,
} from '../models/repository.model';
import { buildRepositoryQueryParams } from './query-params.util';

// Thin HTTP client wrapper for F-010's hidden-gems endpoint. Used to also wrap the now-removed
// Discovery Feed endpoint too (both shared the same filter/sort/pagination contract, D4) - see the
// changelog entry for that removal.
@Injectable({ providedIn: 'root' })
export class RepositoryApiService {
  private readonly http = inject(HttpClient);

  getHiddenGems(filter: RepositoryFilterCriteria): Observable<PagedResult<HiddenGemCardDto>> {
    return this.http.get<PagedResult<HiddenGemCardDto>>('/api/hidden-gems', {
      params: buildRepositoryQueryParams(filter),
    });
  }
}
