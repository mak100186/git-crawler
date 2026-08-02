import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  HiddenGemCardDto,
  PagedResult,
  RepositoryCardDto,
  RepositoryFilterCriteria,
} from '../models/repository.model';
import { buildRepositoryQueryParams } from './query-params.util';

// Thin HTTP client wrapper for F-010's repository-list endpoints (discovery feed, hidden gems,
// category drill-down) - all three share the same filter/sort/pagination contract (D4), so they
// share this one service rather than three near-identical ones.
@Injectable({ providedIn: 'root' })
export class RepositoryApiService {
  private readonly http = inject(HttpClient);

  getDiscoveryFeed(filter: RepositoryFilterCriteria): Observable<PagedResult<RepositoryCardDto>> {
    return this.http.get<PagedResult<RepositoryCardDto>>('/api/discovery-feed', {
      params: buildRepositoryQueryParams(filter),
    });
  }

  getHiddenGems(filter: RepositoryFilterCriteria): Observable<PagedResult<HiddenGemCardDto>> {
    return this.http.get<PagedResult<HiddenGemCardDto>>('/api/hidden-gems', {
      params: buildRepositoryQueryParams(filter),
    });
  }

  // The category is forced onto the Language facet server-side (GetCategoryRepositoriesQueryHandler),
  // so the client-side filter's own `language` values are omitted from the outgoing request.
  getCategoryRepositories(
    category: string,
    filter: RepositoryFilterCriteria,
  ): Observable<PagedResult<RepositoryCardDto>> {
    return this.http.get<PagedResult<RepositoryCardDto>>(
      `/api/categories/${encodeURIComponent(category)}/repositories`,
      { params: buildRepositoryQueryParams(filter, { omitLanguage: true }) },
    );
  }
}
