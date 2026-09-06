import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { CategoryApiService } from '../api/category-api.service';
import { FacetApiService } from '../api/facet-api.service';
import { FacetOptionsService } from './facet-options.service';

describe('FacetOptionsService', () => {
  let getCategories: ReturnType<typeof vi.fn>;
  let getFacetOptions: ReturnType<typeof vi.fn>;
  let service: FacetOptionsService;

  function configure(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: CategoryApiService, useValue: { getCategories } },
        { provide: FacetApiService, useValue: { getFacetOptions } },
      ],
    });
    service = TestBed.inject(FacetOptionsService);
  }

  beforeEach(() => {
    getCategories = vi.fn().mockReturnValue(
      of({
        categories: [{ category: 'Rust' }, { category: 'Go' }],
      }),
    );
    getFacetOptions = vi.fn().mockReturnValue(
      of({
        licenses: ['MIT', 'Apache-2.0'],
        topics: ['tools', 'cli'],
      }),
    );
    configure();
  });

  it('loads language options from GET /api/categories, sorted, and only fetches once', () => {
    let latest: string[] = [];
    service.languageOptions$.subscribe((v) => (latest = v));

    service.ensureLanguageOptionsLoaded();
    service.ensureLanguageOptionsLoaded();

    expect(getCategories).toHaveBeenCalledTimes(1);
    expect(latest).toEqual(['Go', 'Rust']);
  });

  it('loads license and topic options from GET /api/facets, sorted, and only fetches once', () => {
    let licenses: string[] = [];
    let topics: string[] = [];
    service.licenseOptions$.subscribe((v) => (licenses = v));
    service.topicOptions$.subscribe((v) => (topics = v));

    service.ensureFacetOptionsLoaded();
    service.ensureFacetOptionsLoaded();

    expect(getFacetOptions).toHaveBeenCalledTimes(1);
    expect(licenses).toEqual(['Apache-2.0', 'MIT']);
    expect(topics).toEqual(['cli', 'tools']);
  });

  it('covers the whole catalog, not only what a page happened to show', () => {
    // The point of review finding L-4: these options come from the backend's distinct query, so a
    // license or topic that appears on no currently-loaded card is still selectable.
    getFacetOptions = vi.fn().mockReturnValue(
      of({
        licenses: ['GPL-3.0'],
        topics: ['never-on-screen'],
      }),
    );
    configure();

    let licenses: string[] = [];
    let topics: string[] = [];
    service.licenseOptions$.subscribe((v) => (licenses = v));
    service.topicOptions$.subscribe((v) => (topics = v));

    service.ensureFacetOptionsLoaded();

    expect(licenses).toEqual(['GPL-3.0']);
    expect(topics).toEqual(['never-on-screen']);
  });

  it('leaves the option lists empty and allows a retry when the facets request fails', () => {
    getFacetOptions = vi.fn().mockReturnValue(throwError(() => new Error('offline')));
    configure();

    let licenses: string[] = [];
    service.licenseOptions$.subscribe((v) => (licenses = v));

    service.ensureFacetOptionsLoaded();
    expect(licenses).toEqual([]);

    // A failed load must not latch: the next caller retries rather than being stuck with an empty
    // dropdown for the rest of the session.
    service.ensureFacetOptionsLoaded();
    expect(getFacetOptions).toHaveBeenCalledTimes(2);
  });
});
