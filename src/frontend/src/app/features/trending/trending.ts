import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { TrendDto } from '../../core/models/trend.model';
import { TrendingApiService } from '../../core/api/trending-api.service';
import { BookmarkToggle } from '../../shared/components/bookmark-toggle/bookmark-toggle';

// Trending (dashboard-ux-brief.md §4.3) - no filter/sort bar (trends aren't filterable the way
// repositories are); renders whatever order the API returns, does not re-sort client-side.
//
// Deviation (noted per Task Packet): TrendDto carries no explicit growth/period-over-period metric
// for the "▲ +18% this week"-style chip §4.3/G2 illustrates. /api/trending is *not* deduplicated per
// category the way /api/categories is (GetTrendingQueryHandler returns every TrendAggregate row,
// GetCategoriesQueryHandler collapses to "latest wins" per category) - so when a category has more
// than one period's row in the response, this view computes a genuine average-score delta between
// its two most recent periods for the growth chip; when only one period exists (no prior data to
// compare), it falls back to showing the current average score instead of fabricating a percentage.
// This only computes an auxiliary display value per card - it does not alter the server-provided
// array order the cards are rendered in.
@Component({
  selector: 'app-trending',
  imports: [
    MatCardModule,
    MatExpansionModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    BookmarkToggle,
  ],
  templateUrl: './trending.html',
  styleUrl: './trending.scss',
})
export class Trending implements OnInit {
  private readonly trendingApi = inject(TrendingApiService);

  protected readonly trends = signal<TrendDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);

  protected readonly growthLabels = computed(() => {
    const all = this.trends();
    const labels = new Map<number, string>();
    for (const trend of all) {
      labels.set(trend.id, computeGrowthLabel(trend, all));
    }
    return labels;
  });

  ngOnInit(): void {
    this.fetch();
  }

  protected onRetry(): void {
    this.fetch();
  }

  protected growthLabelFor(trend: TrendDto): string {
    return this.growthLabels().get(trend.id) ?? '';
  }

  private fetch(): void {
    this.loading.set(true);
    this.error.set(false);

    this.trendingApi.getTrending().subscribe({
      next: (result) => {
        this.trends.set(result.trends);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }
}

export function computeGrowthLabel(current: TrendDto, allTrends: TrendDto[]): string {
  const sameCategory = allTrends
    .filter((t) => t.category === current.category)
    .sort((a, b) => a.periodStart.localeCompare(b.periodStart));

  const index = sameCategory.findIndex((t) => t.id === current.id);
  const previous = index > 0 ? sameCategory[index - 1] : null;

  if (!previous || previous.averageScore === 0) {
    return `${Math.round(current.averageScore)} avg score`;
  }

  const change = ((current.averageScore - previous.averageScore) / previous.averageScore) * 100;
  const arrow = change >= 0 ? '▲' : '▼';
  const sign = change >= 0 ? '+' : '';
  return `${arrow} ${sign}${change.toFixed(0)}% vs. last period`;
}
