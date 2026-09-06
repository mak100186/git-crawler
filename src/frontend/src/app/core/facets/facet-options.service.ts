import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

import { CategoryApiService } from '../api/category-api.service';
import { FacetApiService } from '../api/facet-api.service';

// Populates the option lists behind the mat-select multiple / mat-autocomplete filter controls
// (FR-004). All three facets now come from the catalog, not from what the session happens to have
// on screen:
//  - Language options come from GET /api/categories, since Category === Repository.PrimaryLanguage
//    server-side (F-010 D2) - the categories list already is the catalog's distinct-language set.
//  - License and topic options come from GET /api/facets.
//
// Until the code-review remediation pass (finding L-4), the latter two were instead accumulated
// client-side from every repository card the app had already fetched, which meant you could only
// filter by a license or topic that happened to be on a page you had already looked at. Two of the
// three facets were therefore useless for discovery, which is the product's whole point.
@Injectable({ providedIn: 'root' })
export class FacetOptionsService {
  private readonly categoryApi = inject(CategoryApiService);
  private readonly facetApi = inject(FacetApiService);

  private readonly languagesSubject = new BehaviorSubject<string[]>([]);
  private readonly licensesSubject = new BehaviorSubject<string[]>([]);
  private readonly topicsSubject = new BehaviorSubject<string[]>([]);

  readonly languageOptions$: Observable<string[]> = this.languagesSubject.asObservable();
  readonly licenseOptions$: Observable<string[]> = this.licensesSubject.asObservable();
  readonly topicOptions$: Observable<string[]> = this.topicsSubject.asObservable();

  private languagesLoaded = false;
  private facetsLoaded = false;

  ensureLanguageOptionsLoaded(): void {
    if (this.languagesLoaded) {
      return;
    }
    this.languagesLoaded = true;
    this.categoryApi.getCategories().subscribe({
      next: (result) => {
        const languages = [...new Set(result.categories.map((c) => c.category))].sort((a, b) =>
          a.localeCompare(b),
        );
        this.languagesSubject.next(languages);
      },
      error: () => {
        // Facet options are a convenience, not a critical path - a failed load just leaves the
        // language select empty rather than blocking the view. Resetting the flag lets the next
        // caller retry.
        this.languagesLoaded = false;
      },
    });
  }

  ensureFacetOptionsLoaded(): void {
    if (this.facetsLoaded) {
      return;
    }
    this.facetsLoaded = true;
    this.facetApi.getFacetOptions().subscribe({
      next: (result) => {
        // The backend already returns these distinct and ordinally sorted; re-sorting here uses the
        // viewer's locale, which is what a human reading a dropdown expects.
        this.licensesSubject.next([...result.licenses].sort((a, b) => a.localeCompare(b)));
        this.topicsSubject.next([...result.topics].sort((a, b) => a.localeCompare(b)));
      },
      error: () => {
        this.facetsLoaded = false;
      },
    });
  }
}
