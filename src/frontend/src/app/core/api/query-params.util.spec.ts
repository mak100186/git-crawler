import { RepositoryFilterCriteria } from '../models/repository.model';
import { buildRepositoryQueryParams } from './query-params.util';

const baseFilter: RepositoryFilterCriteria = {
  bookmarkedOnly: false,
  sort: 'Newest',
  direction: 'Desc',
  page: 1,
  pageSize: 24,
};

describe('buildRepositoryQueryParams', () => {
  it('always sets sort, direction, page, pageSize, and bookmarkedOnly', () => {
    const params = buildRepositoryQueryParams(baseFilter);

    expect(params.get('sort')).toBe('Newest');
    expect(params.get('direction')).toBe('Desc');
    expect(params.get('page')).toBe('1');
    expect(params.get('pageSize')).toBe('24');
    expect(params.get('bookmarkedOnly')).toBe('false');
  });

  it('repeats one query param per array value for language/topic/license', () => {
    const params = buildRepositoryQueryParams({
      ...baseFilter,
      language: ['TypeScript', 'Rust'],
      topic: ['cli'],
      license: ['MIT', 'Apache-2.0'],
    });

    expect(params.getAll('language')).toEqual(['TypeScript', 'Rust']);
    expect(params.getAll('topic')).toEqual(['cli']);
    expect(params.getAll('license')).toEqual(['MIT', 'Apache-2.0']);
  });

  it('omits language params when the omitLanguage option is set (Category drill-down)', () => {
    const params = buildRepositoryQueryParams(
      { ...baseFilter, language: ['Rust'] },
      { omitLanguage: true },
    );

    // Angular's HttpParams.getAll() is documented to return null (not []) when the parameter was
    // never set at all — appendAll is skipped entirely for `language` when omitLanguage is set, so
    // the key is absent from `params`, not present-with-zero-values.
    expect(params.getAll('language')).toBeNull();
  });

  it('only sets minStars/maxStars when present', () => {
    const withoutRange = buildRepositoryQueryParams(baseFilter);
    expect(withoutRange.has('minStars')).toBe(false);
    expect(withoutRange.has('maxStars')).toBe(false);

    const withRange = buildRepositoryQueryParams({ ...baseFilter, minStars: 10, maxStars: 500 });
    expect(withRange.get('minStars')).toBe('10');
    expect(withRange.get('maxStars')).toBe('500');
  });

  it('reflects changed sort/direction/bookmarkedOnly in the outgoing params', () => {
    const params = buildRepositoryQueryParams({
      ...baseFilter,
      sort: 'Score',
      direction: 'Asc',
      bookmarkedOnly: true,
    });

    expect(params.get('sort')).toBe('Score');
    expect(params.get('direction')).toBe('Asc');
    expect(params.get('bookmarkedOnly')).toBe('true');
  });
});
