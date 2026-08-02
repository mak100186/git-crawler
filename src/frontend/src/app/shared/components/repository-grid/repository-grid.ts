import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { HiddenGemCardDto, RepositoryCardDto } from '../../../core/models/repository.model';
import { RepositoryCard } from '../repository-card/repository-card';

// Cross-view Loading/Empty/Error/Populated state machine (dashboard-ux-brief.md §3, dashboard-
// handoff.md §6), plus the card-grid + mat-paginator layout (§4.1) shared by Discovery Feed, Hidden
// Gems, and the Category drill-down - "Category drill-down reuses the Discovery Feed's card-grid +
// mat-paginator layout exactly... do not build a second list component" (Task Packet Constraints).
// The four states are mutually exclusive here (error takes precedence over loading, which takes
// precedence over empty), matching the Task Packet's Test Expectations treating them as four
// distinct render states rather than overlapping ones.
@Component({
  selector: 'app-repository-grid',
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    RepositoryCard,
  ],
  templateUrl: './repository-grid.html',
  styleUrl: './repository-grid.scss',
})
export class RepositoryGrid {
  readonly items = input<(RepositoryCardDto | HiddenGemCardDto)[]>([]);
  readonly loading = input(false);
  readonly error = input(false);
  readonly totalCount = input(0);
  readonly page = input(1);
  readonly pageSize = input(24);
  readonly isHiddenGems = input(false);

  readonly pageChange = output<{ page: number; pageSize: number }>();
  readonly retry = output<void>();
  readonly clearFilters = output<void>();

  // "First load" (nothing to show yet) gets the full centered spinner; a refetch while results are
  // already on screen instead dims them and shows the pinned progress bar (handoff §6).
  protected readonly isFirstLoad = computed(() => this.loading() && this.items().length === 0);
  protected readonly isRefreshing = computed(() => this.loading() && this.items().length > 0);
  protected readonly isEmpty = computed(
    () => !this.loading() && !this.error() && this.items().length === 0,
  );

  protected scoreBreakdownOf(item: RepositoryCardDto | HiddenGemCardDto) {
    return this.isHiddenGems() ? (item as HiddenGemCardDto).scoreBreakdown : null;
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
