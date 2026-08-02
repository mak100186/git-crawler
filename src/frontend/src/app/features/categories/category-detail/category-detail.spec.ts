import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { FacetOptionsService } from '../../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../../core/api/repository-api.service';
import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { CategoryDetail } from './category-detail';

describe('CategoryDetail', () => {
  let fixture: ComponentFixture<CategoryDetail>;
  let getCategoryRepositories: ReturnType<typeof vi.fn>;

  function setUp(): void {
    fixture = TestBed.createComponent(CategoryDetail);
    fixture.detectChanges();
  }

  function configureFor(category: string) {
    return TestBed.configureTestingModule({
      imports: [CategoryDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ category })) },
        },
        { provide: RepositoryApiService, useValue: { getCategoryRepositories } },
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
  }

  beforeEach(() => {
    getCategoryRepositories = vi
      .fn()
      .mockReturnValue(of({ items: [], page: 1, pageSize: 24, totalCount: 0 }));
  });

  it('fetches repositories for the decoded category from the route and pre-applies it as a filter chip', async () => {
    await configureFor('C++ Systems');
    setUp();

    expect(getCategoryRepositories).toHaveBeenCalledWith('C++ Systems', expect.any(Object));

    const chip = (fixture.nativeElement as HTMLElement).querySelector('mat-chip');
    expect(chip?.textContent).toContain('category: C++ Systems');
    expect(chip?.querySelector('button[matChipRemove]')).toBeNull();
  });

  it('shows the empty state when the category has zero repositories', async () => {
    await configureFor('Rust');
    setUp();

    expect(fixture.nativeElement.textContent).toContain('No repositories match these filters');
  });
});
