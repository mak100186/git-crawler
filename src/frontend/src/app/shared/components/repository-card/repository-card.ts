import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RelativeDatePipe } from '../../pipes/relative-date.pipe';
import { BookmarkToggle } from '../bookmark-toggle/bookmark-toggle';

interface ScoreRow {
  label: string;
  value: string;
  weight: number;
  ratio: number; // 0..1, drives the progress bar width
}

// The five caps ScoringWeights.cs log-normalizes each raw signal against (backend, ComputeScores) -
// mirrored here purely to draw an accurate "how much of this signal's ceiling did this repo hit"
// progress bar per signal row (FR-005's "independently-weighted, identifiable inputs"). This does
// not duplicate any scoring *decision* - the actual score always comes from the API's totalScore -
// it only reproduces the same display-normalization math the design's per-signal bars need.
const COMMITS_PER_WEEK_CAP = 10.0;
const CONTRIBUTOR_COUNT_CAP = 25.0;
const FORK_COUNT_CAP = 200.0;
const STAR_COUNT_CAP = 50.0;

function normalizeLog(rawValue: number, cap: number): number {
  if (rawValue <= 0) {
    return 0;
  }
  return Math.min(Math.log(rawValue + 1) / Math.log(cap + 1), 1);
}

// Reusable repository card (dashboard-ux-brief.md §4.1/§4.2, dashboard-handoff.md §4). Renders the
// base Discovery Feed / Category-drill-down card by default; passing `scoreBreakdown` switches on
// the Hidden Gems variant (score badge + "Why this score?" expansion panel) without a second
// component, per the Task Packet's "base + hidden-gem-score variant via @Input".
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

  protected readonly avatarInitial = computed(() => {
    const repo = this.repository();
    return (repo.owner || repo.name || '?').charAt(0).toUpperCase();
  });

  // "rotating terracotta/olive/neutral fills" (dashboard-handoff.md §4) - a stable, deterministic
  // rotation keyed on repository id rather than array index, so a card's avatar color doesn't shift
  // as the surrounding list re-sorts/re-pages.
  private static readonly AVATAR_PALETTE = ['terracotta', 'olive', 'neutral'] as const;
  protected readonly avatarPalette = computed(
    () =>
      RepositoryCard.AVATAR_PALETTE[this.repository().id % RepositoryCard.AVATAR_PALETTE.length],
  );

  protected readonly scoreRounded = computed(() => {
    const breakdown = this.scoreBreakdown();
    return breakdown ? Math.round(breakdown.totalScore) : null;
  });

  protected readonly scoreRows = computed<ScoreRow[]>(() => {
    const b = this.scoreBreakdown();
    if (!b) {
      return [];
    }

    return [
      {
        label: 'License',
        value: b.hasLicense ? (b.licenseType ?? 'Licensed') : 'None',
        weight: b.licenseWeight,
        ratio: b.hasLicense ? 1 : 0,
      },
      {
        label: 'Commits/week',
        value: b.commitsPerWeek.toFixed(1),
        weight: b.commitsPerWeekWeight,
        ratio: normalizeLog(b.commitsPerWeek, COMMITS_PER_WEEK_CAP),
      },
      {
        label: 'Contributors',
        value: String(b.contributorCount),
        weight: b.contributorCountWeight,
        ratio: normalizeLog(b.contributorCount, CONTRIBUTOR_COUNT_CAP),
      },
      {
        label: 'Forks',
        value: String(b.forkCount),
        weight: b.forkCountWeight,
        ratio: normalizeLog(b.forkCount, FORK_COUNT_CAP),
      },
      {
        label: 'Stars',
        value: String(b.starCount),
        weight: b.starCountWeight,
        ratio: normalizeLog(b.starCount, STAR_COUNT_CAP),
      },
    ];
  });
}
