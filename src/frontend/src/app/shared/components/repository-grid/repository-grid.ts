import { Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { HiddenGemCardDto, RepositoryCardDto } from '../../../core/models/repository.model';
import { RepositoryCard } from '../repository-card/repository-card';
import {
  RepositoryDetailDialogData,
  RepositoryDetailPane,
} from '../repository-detail-pane/repository-detail-pane';

// Cross-view Loading/Empty/Error/Populated state machine (dashboard-ux-brief.md §3, dashboard-
// handoff.md §6), plus the card-grid + mat-paginator layout (§4.1) - used solely by Hidden Gems now
// (Discovery Feed and the dedicated Bookmarks view both shared it too before each was removed as a
// standalone view - see the changelog entries for those removals) - "do not build a second list
// component" (Task Packet Constraints).
// The four states are mutually exclusive here (error takes precedence over loading, which takes
// precedence over empty), matching the Task Packet's Test Expectations treating them as four
// distinct render states rather than overlapping ones.
@Component({
  selector: 'app-repository-grid',
  imports: [
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
  private readonly dialog = inject(MatDialog);

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

  // Card click -> centered detail modal (originally design brief §09's right-side drawer, replaced
  // with a MatDialog per operator direction - see RepositoryDetailPane's own header comment for
  // why). `RepositoryGrid` still owns triggering it since it's the one component shared by every
  // view a card can be clicked from; the open/close state itself now lives in MatDialog/its
  // MatDialogRef instead of a local signal.
  protected openDetail(item: RepositoryCardDto | HiddenGemCardDto): void {
    this.dialog.open<RepositoryDetailPane, RepositoryDetailDialogData>(RepositoryDetailPane, {
      data: {
        item,
        scoreBreakdown: this.scoreBreakdownOf(item),
        trendGrowth: this.trendGrowthOf(item),
      },
      width: '840px',
      maxWidth: '90vw',
      maxHeight: '85vh',
      panelClass: 'repo-detail-dialog-panel',
      autoFocus: false,
    });
  }

  // "First load" (nothing to show yet) gets the full centered spinner; a refetch while results are
  // already on screen instead dims them and shows the pinned progress bar (handoff §6).
  protected readonly isFirstLoad = computed(() => this.loading() && this.items().length === 0);
  protected readonly isRefreshing = computed(() => this.loading() && this.items().length > 0);
  protected readonly isEmpty = computed(
    () => !this.loading() && !this.error() && this.items().length === 0,
  );

  protected scoreBreakdownOf(item: RepositoryCardDto | HiddenGemCardDto | null) {
    return item && this.isHiddenGems() ? (item as HiddenGemCardDto).scoreBreakdown : null;
  }

  protected trendGrowthOf(item: RepositoryCardDto | HiddenGemCardDto | null): string | null {
    return item && this.isHiddenGems() ? (item as HiddenGemCardDto).trendGrowth : null;
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
