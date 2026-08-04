import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RelativeDatePipe } from '../../pipes/relative-date.pipe';
import { buildScoreRows } from '../../utils/score-breakdown.util';
import { BookmarkToggle } from '../bookmark-toggle/bookmark-toggle';

export interface RepositoryDetailDialogData {
  item: RepositoryCardDto;
  scoreBreakdown: ScoreBreakdownDto | null;
  trendGrowth: string | null;
}

// Centered modal content, opened via MatDialog by RepositoryGrid on a card click (design brief §09
// called for a right-side drawer originally; superseded by a centered dialog per operator direction
// - the drawer's backdrop was scoped to RepositoryGrid's own bounding box, not the full page, and
// couldn't be made to look like a centered modal without fighting mat-drawer's docked-edge slide
// animation). Shows the same data the compact card already carries, just untruncated (full summary,
// topics list - neither shown on the card itself) plus the score breakdown always expanded rather
// than tucked behind "Why this score?" (there's room here). `scoreBreakdown`/`trendGrowth` are
// independently optional, same as RepositoryCard - Hidden Gems (the sole current caller) always
// passes both, but the dedicated Bookmarks view passed neither before it was removed as a standalone
// view (see the changelog entry for that removal), and a future bare-card caller could do the same
// again.
@Component({
  selector: 'app-repository-detail-pane',
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    RelativeDatePipe,
    BookmarkToggle,
  ],
  templateUrl: './repository-detail-pane.html',
  styleUrl: './repository-detail-pane.scss',
})
export class RepositoryDetailPane {
  private readonly dialogRef = inject(MatDialogRef<RepositoryDetailPane>);
  private readonly data = inject<RepositoryDetailDialogData>(MAT_DIALOG_DATA);

  protected readonly item = this.data.item;
  protected readonly scoreBreakdown = this.data.scoreBreakdown;
  protected readonly trendGrowth = this.data.trendGrowth;

  protected readonly scoreRounded = this.scoreBreakdown
    ? Math.round(this.scoreBreakdown.totalScore)
    : null;

  protected readonly scoreRows = this.scoreBreakdown ? buildScoreRows(this.scoreBreakdown) : [];

  protected close(): void {
    this.dialogRef.close();
  }
}
