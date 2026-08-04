import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { RepositoryCardDto } from '../models/repository.model';
import { CategoryApiService } from '../api/category-api.service';
import { FacetOptionsService } from './facet-options.service';

function repo(overrides: Partial<RepositoryCardDto>): RepositoryCardDto {
  return {
    id: 1,
    owner: 'octocat',
    name: 'hello-world',
    url: 'https://github.com/octocat/hello-world',
    primaryLanguage: 'TypeScript',
    starCount: 1,
    forkCount: 0,
    licenseIdentifier: null,
    topics: [],
    firstDiscoveredAtUtc: new Date().toISOString(),
    summaryContent: null,
    detailedSummaryContent: null,
    isBookmarked: false,
    ...overrides,
  };
}

describe('FacetOptionsService', () => {
  let getCategories: ReturnType<typeof vi.fn>;
  let service: FacetOptionsService;

  beforeEach(() => {
    getCategories = vi.fn().mockReturnValue(
      of({
        categories: [{ category: 'Rust' }, { category: 'Go' }],
      }),
    );

    TestBed.configureTestingModule({
      providers: [{ provide: CategoryApiService, useValue: { getCategories } }],
    });
    service = TestBed.inject(FacetOptionsService);
  });

  it('loads language options from GET /api/categories, sorted, and only fetches once', () => {
    let latest: string[] = [];
    service.languageOptions$.subscribe((v) => (latest = v));

    service.ensureLanguageOptionsLoaded();
    service.ensureLanguageOptionsLoaded();

    expect(getCategories).toHaveBeenCalledTimes(1);
    expect(latest).toEqual(['Go', 'Rust']);
  });

  it('accumulates distinct licenses and topics from repositories seen across calls', () => {
    let licenses: string[] = [];
    let topics: string[] = [];
    service.licenseOptions$.subscribe((v) => (licenses = v));
    service.topicOptions$.subscribe((v) => (topics = v));

    service.recordRepositories([
      repo({ licenseIdentifier: 'MIT', topics: ['cli'] }),
      repo({ licenseIdentifier: 'Apache-2.0', topics: ['cli', 'tools'] }),
    ]);
    service.recordRepositories([repo({ licenseIdentifier: 'MIT', topics: ['mcp'] })]);

    expect(licenses).toEqual(['Apache-2.0', 'MIT']);
    expect(topics).toEqual(['cli', 'mcp', 'tools']);
  });
});
