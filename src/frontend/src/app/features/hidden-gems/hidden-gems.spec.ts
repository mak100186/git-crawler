import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { HiddenGemCardDto } from '../../core/models/repository.model';
import { FacetOptionsService } from '../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../core/api/repository-api.service';
import { BookmarkApiService } from '../../core/api/bookmark-api.service';
import { HiddenGems } from './hidden-gems';

const hiddenGem: HiddenGemCardDto = {
  id: 1,
  owner: 'octocat',
  name: 'hello-world',
  url: 'https://github.com/octocat/hello-world',
  primaryLanguage: 'TypeScript',
  starCount: 42,
  forkCount: 5,
  licenseIdentifier: 'MIT',
  licenseName: 'MIT License',
  topics: [],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'A tool.',
  isBookmarked: false,
  trendGrowth: '▲ +18% vs. last period',
  scoreBreakdown: {
    hasLicense: true,
    licenseType: 'MIT',
    licenseWeight: 0.18,
    commitsPerWeek: 3,
    commitsPerWeekWeight: 0.27,
    contributorCount: 4,
    contributorCountWeight: 0.225,
    forkCount: 5,
    forkCountWeight: 0.225,
    starCount: 42,
    starCountWeight: 0.1,
    totalScore: 55,
  },
};

describe('HiddenGems', () => {
  let fixture: ComponentFixture<HiddenGems>;
  let getHiddenGems: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getHiddenGems = vi.fn();

    await TestBed.configureTestingModule({
      imports: [HiddenGems],
      providers: [
        provideNoopAnimations(),
        { provide: RepositoryApiService, useValue: { getHiddenGems } },
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

  it('requests Score/Desc by default', () => {
    getHiddenGems.mockReturnValue(of({ items: [hiddenGem], page: 1, pageSize: 24, totalCount: 1 }));

    fixture = TestBed.createComponent(HiddenGems);
    fixture.detectChanges();

    expect(getHiddenGems).toHaveBeenCalledWith(
      expect.objectContaining({ sort: 'Score', direction: 'Desc' }),
    );
  });

  it('renders the populated grid with the score badge', () => {
    getHiddenGems.mockReturnValue(of({ items: [hiddenGem], page: 1, pageSize: 24, totalCount: 1 }));

    fixture = TestBed.createComponent(HiddenGems);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('hello-world');
    expect(fixture.nativeElement.querySelector('.repo-card__score-badge')).toBeTruthy();
  });

  it('renders each card\'s trend-growth chip (merged in from the old standalone Trending view)', () => {
    getHiddenGems.mockReturnValue(of({ items: [hiddenGem], page: 1, pageSize: 24, totalCount: 1 }));

    fixture = TestBed.createComponent(HiddenGems);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.repo-card__trend-chip')?.textContent?.trim()).toBe(
      '▲ +18% vs. last period',
    );
  });

  it('shows the empty state when there are zero hidden gems', () => {
    getHiddenGems.mockReturnValue(of({ items: [], page: 1, pageSize: 24, totalCount: 0 }));

    fixture = TestBed.createComponent(HiddenGems);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No repositories match these filters');
  });

  it('shows the error state when the request fails', () => {
    getHiddenGems.mockReturnValue(throwError(() => new Error('network error')));

    fixture = TestBed.createComponent(HiddenGems);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Something went wrong');
  });
});
