import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

import { RepositoryCardDto } from '../models/repository.model';
import { CategoryApiService } from '../api/category-api.service';

// F-010's contract has no dedicated "distinct facet values" endpoint (no /api/languages,
// /api/licenses, /api/topics) - the mat-select multiple / mat-autocomplete controls (FR-004, dashboard
// -ux-brief.md §5.1) need *some* bounded option list to populate from, though. This service closes
// that gap client-side rather than inventing a new backend endpoint:
//  - Language options come from GET /api/categories, since Category === Repository.PrimaryLanguage
//    server-side (F-010 D2) - the categories list already is the catalog's distinct-language set.
//  - License and topic options have no equivalent existing endpoint to piggyback on, so they're
//    accumulated client-side from every repository card the app has already fetched this session
//    (hidden gems responses call recordRepositories(); discovery feed and category drill-down did
//    too before both were removed as standalone views - see the changelog entries for those
//    removals). This means the option list only grows to cover what's been seen, not the full
//    catalog upfront - an accepted approximation, called out as a deviation in the Task Packet
//    output.
@Injectable({ providedIn: 'root' })
export class FacetOptionsService {
  private readonly categoryApi = inject(CategoryApiService);

  private readonly languagesSubject = new BehaviorSubject<string[]>([]);
  private readonly licensesSubject = new BehaviorSubject<string[]>([]);
  private readonly topicsSubject = new BehaviorSubject<string[]>([]);

  readonly languageOptions$: Observable<string[]> = this.languagesSubject.asObservable();
  readonly licenseOptions$: Observable<string[]> = this.licensesSubject.asObservable();
  readonly topicOptions$: Observable<string[]> = this.topicsSubject.asObservable();

  private languagesLoaded = false;

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
        // language select empty rather than blocking the view.
        this.languagesLoaded = false;
      },
    });
  }

  recordRepositories(items: RepositoryCardDto[]): void {
    const licenses = new Set(this.licensesSubject.value);
    const topics = new Set(this.topicsSubject.value);

    for (const item of items) {
      if (item.licenseIdentifier) {
        licenses.add(item.licenseIdentifier);
      }
      for (const topic of item.topics) {
        topics.add(topic);
      }
    }

    this.licensesSubject.next([...licenses].sort((a, b) => a.localeCompare(b)));
    this.topicsSubject.next([...topics].sort((a, b) => a.localeCompare(b)));
  }
}
