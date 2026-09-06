import { AsyncPipe } from '@angular/common';
import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Subject, catchError, switchMap, tap } from 'rxjs';

import {
  DEFAULT_PAGE_SIZE,
  HiddenGemCardDto,
  RepositoryFilterCriteria,
} from '../../core/models/repository.model';
import { FacetOptionsService } from '../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../core/api/repository-api.service';
import {
  FilterSortBar,
  FilterSortState,
} from '../../shared/components/filter-sort-bar/filter-sort-bar';
import { RepositoryGrid } from '../../shared/components/repository-grid/repository-grid';

// Hidden Gems (dashboard-ux-brief.md §4.2) - the scored subset, ranked by hidden-gem score by
// default, and now the dashboard's only repository-list view (Discovery Feed shared the same
// card-grid + filter-bar structure before it was removed as a standalone view - see the changelog
// entry for that removal). The score-breakdown/trend-growth data the grid passes down to each card
// is what's Hidden-Gems-specific (trendGrowth added when the standalone Trending tab was merged into
// Hidden Gems - see the changelog entry for that removal too).
@Component({
  selector: 'app-hidden-gems',
  imports: [AsyncPipe, FilterSortBar, RepositoryGrid],
  templateUrl: './hidden-gems.html',
  styleUrl: './hidden-gems.scss',
})
export class HiddenGems implements OnInit {
  private readonly repositoryApi = inject(RepositoryApiService);
  protected readonly facetOptions = inject(FacetOptionsService);

  @ViewChild(FilterSortBar) private filterBar?: FilterSortBar;

  protected readonly items = signal<HiddenGemCardDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  // Every fetch goes through this subject so switchMap can cancel the previous request. Without
  // it, two in-flight requests can settle out of order and leave the grid showing results for a
  // filter the user has already changed, with no indication anything is stale.
  private readonly fetchRequests = new Subject<RepositoryFilterCriteria>();

  private filter: RepositoryFilterCriteria = {
    bookmarkedOnly: false,
    sort: 'Score',
    direction: 'Desc',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  };

  constructor() {
    this.fetchRequests
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(false);
        }),
        switchMap((filter) =>
          this.repositoryApi.getHiddenGems(filter).pipe(
            catchError(() => {
              this.loading.set(false);
              this.error.set(true);
              // EMPTY, not a rethrow: an error on one request must not tear down the pipeline and
              // leave every later filter change dead.
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
        this.facetOptions.recordRepositories(result.items);
      });
  }

  ngOnInit(): void {
    this.facetOptions.ensureLanguageOptionsLoaded();
    this.fetch();
  }

  protected onFilterStateChange(state: FilterSortState): void {
    this.filter = { ...this.filter, ...state, page: 1, pageSize: this.pageSize() };
    this.fetch();
  }

  protected onPageChange(event: { page: number; pageSize: number }): void {
    this.filter = { ...this.filter, page: event.page, pageSize: event.pageSize };
    this.fetch();
  }

  protected onRetry(): void {
    this.fetch();
  }

  protected onClearFilters(): void {
    this.filterBar?.clearAll();
  }

  private fetch(): void {
    this.fetchRequests.next(this.filter);
  }
}
