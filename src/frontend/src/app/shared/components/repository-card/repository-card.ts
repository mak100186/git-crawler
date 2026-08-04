import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RelativeDatePipe } from '../../pipes/relative-date.pipe';
import { BookmarkToggle } from '../bookmark-toggle/bookmark-toggle';

// Reusable repository card (dashboard-ux-brief.md §4.1/§4.2, dashboard-handoff.md §4). Renders the
// bare card when `scoreBreakdown` is omitted; passing it switches on the Hidden Gems variant (score
// badge) without a second component, per the Task Packet's original "base + hidden-gem-score
// variant via @Input". Hidden Gems (the sole current caller) always passes it, so the bare variant
// has no live caller right now - kept anyway rather than making the input required, since its two
// prior callers (Discovery Feed, then the dedicated Bookmarks view) were each removed as standalone
// views in turn, not because bare cards stopped being a real shape (see the changelog entries for
// those removals). The avatar initial this card used to render alongside the score badge was
// removed per operator feedback - the score badge alone is enough of a visual anchor in the header
// (see the changelog entry for that change). The card's own "Why this score?" expansion panel was
// removed the same way - the full score breakdown now lives in RepositoryDetailPane's
// always-expanded footer instead, opened via the same card click that used to also drive this panel
// (see the changelog entry for that removal); the score badge itself stays, as the card's own
// at-a-glance anchor. The trend-growth chip was removed from the card too, same reasoning -
// RepositoryDetailPane's own chips row already shows it, so the card doesn't need its own copy
// (star count moved into the subtitle in the same pass, replacing it as the footer's third fact).
// `trendGrowth` itself is now RepositoryGrid's concern alone (still read to build the dialog's
// data), not this component's - see RepositoryGrid.openDetail.
@Component({
  selector: 'app-repository-card',
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
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

  // Emitted on a click anywhere on the card except its own interactive controls (bookmark toggle,
  // "Open on GitHub" link - each stops propagation in the template so this only fires for a click
  // meant to open the detail pane, matching the design brief's §09 "card click -> detail pane"
  // interaction.
  readonly cardClick = output<void>();

  protected readonly scoreRounded = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? Math.round(breakdown.totalScore) : null;
  });
}
