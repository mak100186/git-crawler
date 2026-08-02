import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Subject, of, throwError } from 'rxjs';

import { PagedResult, RepositoryCardDto } from '../../core/models/repository.model';
import { FacetOptionsService } from '../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../core/api/repository-api.service';
import { BookmarkApiService } from '../../core/api/bookmark-api.service';
import { DiscoveryFeed } from './discovery-feed';

const repo: RepositoryCardDto = {
  id: 1,
  owner: 'octocat',
  name: 'hello-world',
  url: 'https://github.com/octocat/hello-world',
  primaryLanguage: 'TypeScript',
  starCount: 42,
  forkCount: 5,
  licenseIdentifier: 'MIT',
  licenseName: 'MIT License',
  topics: ['cli'],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'A tool.',
  isBookmarked: false,
};

describe('DiscoveryFeed', () => {
  let fixture: ComponentFixture<DiscoveryFeed>;
  let getDiscoveryFeed: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getDiscoveryFeed = vi.fn();

    await TestBed.configureTestingModule({
      imports: [DiscoveryFeed],
      providers: [
        provideNoopAnimations(),
        { provide: RepositoryApiService, useValue: { getDiscoveryFeed } },
        {
          provide: FacetOptionsService,
          useValue: {
            ensureLanguageOptionsLoaded: vi.fn(),
            recordRepositories: vi.fn(),
            languageOptions$: of([]),
            licenseOptions$: of([]),
            topicOptions$: of([]),
          },
        },
        {
          provide: BookmarkApiService,
          useValue: { addBookmark: vi.fn(), removeBookmark: vi.fn() },
        },
      ],
    }).compileComponents();
  });

  it('shows the loading state while the initial request is in flight', () => {
    const subject = new Subject<PagedResult<RepositoryCardDto>>();
    getDiscoveryFeed.mockReturnValue(subject.asObservable());

    fixture = TestBed.createComponent(DiscoveryFeed);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading repositories');
  });

  it('renders the populated grid once the request resolves', () => {
    getDiscoveryFeed.mockReturnValue(of({ items: [repo], page: 1, pageSize: 24, totalCount: 1 }));

    fixture = TestBed.createComponent(DiscoveryFeed);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('hello-world');
  });

  it('shows the empty state, not an error, when a page is requested beyond the last page', () => {
    getDiscoveryFeed.mockReturnValue(of({ items: [], page: 5, pageSize: 24, totalCount: 1 }));

    fixture = TestBed.createComponent(DiscoveryFeed);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No repositories match these filters');
    expect(fixture.nativeElement.textContent).not.toContain('Something went wrong');
  });

  it('shows the empty state, not an error, when bookmarkedOnly yields zero results', () => {
    getDiscoveryFeed.mockReturnValue(of({ items: [], page: 1, pageSize: 24, totalCount: 0 }));

    fixture = TestBed.createComponent(DiscoveryFeed);
    (
      fixture.componentInstance as unknown as { onFilterStateChange: (s: unknown) => void }
    ).onFilterStateChange({
      language: [],
      topic: [],
      license: [],
      bookmarkedOnly: true,
      sort: 'Newest',
      direction: 'Desc',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No repositories match these filters');
  });

  it('shows the error state when the request fails', () => {
    getDiscoveryFeed.mockReturnValue(throwError(() => new Error('network error')));

    fixture = TestBed.createComponent(DiscoveryFeed);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Something went wrong');
  });
});
