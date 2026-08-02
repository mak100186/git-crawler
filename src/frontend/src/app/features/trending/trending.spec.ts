import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { TrendDto } from '../../core/models/trend.model';
import { TrendingApiService } from '../../core/api/trending-api.service';
import { BookmarkApiService } from '../../core/api/bookmark-api.service';
import { Trending, computeGrowthLabel } from './trending';

function makeTrend(overrides: Partial<TrendDto> = {}): TrendDto {
  return {
    id: 1,
    category: 'Rust',
    periodStart: '2026-07-01',
    periodEnd: '2026-07-07',
    repositoryCount: 3,
    averageScore: 60,
    createdAtUtc: new Date().toISOString(),
    contributingRepositories: [],
    ...overrides,
  };
}

describe('computeGrowthLabel', () => {
  it('falls back to an average-score label when only one period exists for the category', () => {
    const trend = makeTrend();
    expect(computeGrowthLabel(trend, [trend])).toBe('60 avg score');
  });

  it('computes a percentage delta against the previous period for the same category', () => {
    const previous = makeTrend({ id: 1, periodStart: '2026-06-01', averageScore: 50 });
    const current = makeTrend({ id: 2, periodStart: '2026-07-01', averageScore: 60 });
    expect(computeGrowthLabel(current, [previous, current])).toBe('▲ +20% vs. last period');
  });
});

describe('Trending', () => {
  let fixture: ComponentFixture<Trending>;
  let getTrending: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getTrending = vi.fn();

    await TestBed.configureTestingModule({
      imports: [Trending],
      providers: [
        provideNoopAnimations(),
        { provide: TrendingApiService, useValue: { getTrending } },
        {
          provide: BookmarkApiService,
          useValue: { addBookmark: vi.fn(), removeBookmark: vi.fn() },
        },
      ],
    }).compileComponents();
  });

  it('shows the loading state, then the populated trend list', () => {
    getTrending.mockReturnValue(of({ trends: [makeTrend()] }));

    fixture = TestBed.createComponent(Trending);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Rust');
    expect(fixture.nativeElement.textContent).toContain('3 repos in this trend');
  });

  it('shows the error state when the request fails', () => {
    getTrending.mockReturnValue(throwError(() => new Error('network error')));

    fixture = TestBed.createComponent(Trending);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Something went wrong');
  });

  it('shows the empty state when there are zero trends', () => {
    getTrending.mockReturnValue(of({ trends: [] }));

    fixture = TestBed.createComponent(Trending);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No trends yet');
  });

  it('renders a trend with an empty contributingRepositories list without crashing', () => {
    getTrending.mockReturnValue(of({ trends: [makeTrend({ contributingRepositories: [] })] }));

    fixture = TestBed.createComponent(Trending);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(fixture.nativeElement.textContent).toContain('No contributing repositories yet');
  });
});
