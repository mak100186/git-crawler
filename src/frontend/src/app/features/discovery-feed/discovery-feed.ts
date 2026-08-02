import { AsyncPipe } from '@angular/common';
import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';

import {
  DEFAULT_PAGE_SIZE,
  RepositoryCardDto,
  RepositoryFilterCriteria,
} from '../../core/models/repository.model';
import { FacetOptionsService } from '../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../core/api/repository-api.service';
import {
  FilterSortBar,
  FilterSortState,
} from '../../shared/components/filter-sort-bar/filter-sort-bar';
import { RepositoryGrid } from '../../shared/components/repository-grid/repository-grid';

// Default landing view (dashboard-ux-brief.md §4.1) - every repository that has cleared baseline
// discovery criteria, newest first.
@Component({
  selector: 'app-discovery-feed',
  imports: [AsyncPipe, FilterSortBar, RepositoryGrid],
  templateUrl: './discovery-feed.html',
  styleUrl: './discovery-feed.scss',
})
export class DiscoveryFeed implements OnInit {
  private readonly repositoryApi = inject(RepositoryApiService);
  protected readonly facetOptions = inject(FacetOptionsService);

  @ViewChild(FilterSortBar) private filterBar?: FilterSortBar;

  protected readonly items = signal<RepositoryCardDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  private filter: RepositoryFilterCriteria = {
    bookmarkedOnly: false,
    sort: 'Newest',
    direction: 'Desc',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  };

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
    this.loading.set(true);
    this.error.set(false);

    this.repositoryApi.getDiscoveryFeed(this.filter).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
        this.facetOptions.recordRepositories(result.items);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }
}
