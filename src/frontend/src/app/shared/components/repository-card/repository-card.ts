import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RelativeDatePipe } from '../../pipes/relative-date.pipe';
import { buildScoreRows } from '../../utils/score-breakdown.util';
import { BookmarkToggle } from '../bookmark-toggle/bookmark-toggle';

// Reusable repository card (dashboard-ux-brief.md §4.1/§4.2, dashboard-handoff.md §4). Renders the
// bare card when `scoreBreakdown` is omitted; passing it switches on the Hidden Gems variant (score
// badge + "Why this score?" expansion panel) without a second component, per the Task Packet's
// original "base + hidden-gem-score variant via @Input". Hidden Gems (the sole current caller) always
// passes it, so the bare variant has no live caller right now - kept anyway rather than making the
// input required, since its two prior callers (Discovery Feed, then the dedicated Bookmarks view)
// were each removed as standalone views in turn, not because bare cards stopped being a real shape
// (see the changelog entries for those removals). `trendGrowth` is a separate, independently-optional
// input in the same vein -
// Hidden Gems passes both, but a future caller could pass either alone (added when the standalone
// Trending tab was merged into Hidden Gems; see the changelog entry for that removal). The avatar
// initial this card used to render alongside the score badge was removed per operator feedback -
// the score badge alone is enough of a visual anchor in the header (see the changelog entry for
// that change).
@Component({
  selector: 'app-repository-card',
  imports: [
    CommonModule,
    MatCardModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    RelativeDatePipe,
    BookmarkToggle,
  ],
  templateUrl: './repository-card.html',
  styleUrl: './repository-card.scss',
})
export class RepositoryCard {
  readonly repository = input.required<RepositoryCardDto>();
  readonly scoreBreakdown = input<ScoreBreakdownDto | null>(null);
  readonly trendGrowth = input<string | null>(null);

  // Emitted on a click anywhere on the card except its own interactive controls (bookmark toggle,
  // "Why this score?" panel, "Open on GitHub" link - each stops propagation in the template so this
  // only fires for a click meant to open the detail pane, matching the design brief's §09 "card
  // click -> right-side detail drawer" interaction.
  readonly cardClick = output<void>();

  protected readonly scoreRounded = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? Math.round(breakdown.totalScore) : null;
  });

  protected readonly scoreRows = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? buildScoreRows(breakdown) : [];
  });
}
