import { AsyncPipe } from '@angular/common';
import { Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import {
  DEFAULT_PAGE_SIZE,
  RepositoryCardDto,
  RepositoryFilterCriteria,
} from '../../../core/models/repository.model';
import { FacetOptionsService } from '../../../core/facets/facet-options.service';
import { RepositoryApiService } from '../../../core/api/repository-api.service';
import {
  FilterSortBar,
  FilterSortState,
} from '../../../shared/components/filter-sort-bar/filter-sort-bar';
import { RepositoryGrid } from '../../../shared/components/repository-grid/repository-grid';

// Category drill-down (dashboard-ux-brief.md §4.4 level 2, dashboard-handoff.md §5 "05") - reuses
// Discovery Feed's exact card-grid + mat-paginator + filter-bar layout (Constraints: "do not build a
// second list component"), with the category pre-applied as a forced, non-removable Language facet
// value (matching GetCategoryRepositoriesQueryHandler's own `filter.Filter with { Language =
// [Category] }` override server-side).
@Component({
  selector: 'app-category-detail',
  imports: [AsyncPipe, RouterLink, FilterSortBar, RepositoryGrid],
  templateUrl: './category-detail.html',
  styleUrl: './category-detail.scss',
})
export class CategoryDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly repositoryApi = inject(RepositoryApiService);
  protected readonly facetOptions = inject(FacetOptionsService);

  @ViewChild(FilterSortBar) private filterBar?: FilterSortBar;

  protected readonly category = signal('');
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
    // ActivatedRoute already URL-decodes the path segment (e.g. "C%2B%2B%20Systems" back to
    // "C++ Systems"), matching the encoded link Categories' tile grid produces via routerLink array
    // binding.
    this.route.paramMap.subscribe((params) => {
      const category = params.get('category') ?? '';
      this.category.set(category);
      this.filter = { ...this.filter, page: 1 };
      this.fetch(category);
    });
  }

  protected onFilterStateChange(state: FilterSortState): void {
    this.filter = { ...this.filter, ...state, page: 1, pageSize: this.pageSize() };
    this.fetch(this.category());
  }

  protected onPageChange(event: { page: number; pageSize: number }): void {
    this.filter = { ...this.filter, page: event.page, pageSize: event.pageSize };
    this.fetch(this.category());
  }

  protected onRetry(): void {
    this.fetch(this.category());
  }

  protected onClearFilters(): void {
    this.filterBar?.clearAll();
  }

  private fetch(category: string): void {
    if (!category) {
      return;
    }

    this.loading.set(true);
    this.error.set(false);

    this.repositoryApi.getCategoryRepositories(category, this.filter).subscribe({
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
