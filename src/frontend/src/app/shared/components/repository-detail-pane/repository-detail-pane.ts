import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RelativeDatePipe } from '../../pipes/relative-date.pipe';
import { buildScoreRows } from '../../utils/score-breakdown.util';
import { BookmarkToggle } from '../bookmark-toggle/bookmark-toggle';

// Right-side detail drawer content, opened by RepositoryGrid on a card click (design brief §09,
// "card click -> right-side mat-drawer over the current view - list keeps its scroll position").
// Shows the same data the compact card already carries, just untruncated (full summary, topics list
// - neither shown on the card itself) plus the score breakdown always expanded rather than tucked
// behind "Why this score?" (there's room here). `scoreBreakdown`/`trendGrowth` are independently
// optional, same as RepositoryCard - Hidden Gems (the sole current caller) always passes both, but
// the dedicated Bookmarks view passed neither before it was removed as a standalone view (see the
// changelog entry for that removal), and a future bare-card caller could do the same again.
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
  readonly item = input<RepositoryCardDto | null>(null);
  readonly scoreBreakdown = input<ScoreBreakdownDto | null>(null);
  readonly trendGrowth = input<string | null>(null);

  readonly closed = output<void>();

  protected readonly scoreRounded = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? Math.round(breakdown.totalScore) : null;
  });

  protected readonly scoreRows = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? buildScoreRows(breakdown) : [];
  });
}
