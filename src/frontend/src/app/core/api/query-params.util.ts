import { HttpParams } from '@angular/common/http';

import { RepositoryFilterCriteria } from '../models/repository.model';

// D4's filter/sort/pagination contract, translated to query params the way ASP.NET Core's minimal
// API model binding expects: repeatable params (one `key=value` pair per array entry) for
// `string[]?` parameters (language, topic, license), plain scalars for everything else. Pulled out
// as a standalone function (no HttpClient dependency) specifically so filter-state -> query-param
// translation can be unit tested without mocking HTTP (Task Packet Test Expectations).
export function buildRepositoryQueryParams(
  filter: RepositoryFilterCriteria,
  options: { omitLanguage?: boolean } = {},
): HttpParams {
  let params = new HttpParams()
    .set('sort', filter.sort)
    .set('direction', filter.direction)
    .set('page', String(filter.page))
    .set('pageSize', String(filter.pageSize))
    .set('bookmarkedOnly', String(filter.bookmarkedOnly));

  if (!options.omitLanguage) {
    params = appendAll(params, 'language', filter.language);
  }
  params = appendAll(params, 'topic', filter.topic);
  params = appendAll(params, 'license', filter.license);

  if (filter.minStars != null) {
    params = params.set('minStars', String(filter.minStars));
  }
  if (filter.maxStars != null) {
    params = params.set('maxStars', String(filter.maxStars));
  }

  return params;
}

function appendAll(params: HttpParams, key: string, values: string[] | undefined): HttpParams {
  return (values ?? []).reduce((acc, value) => acc.append(key, value), params);
}
