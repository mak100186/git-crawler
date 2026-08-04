import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RepositoryDetailDialogData, RepositoryDetailPane } from './repository-detail-pane';

const baseRepository: RepositoryCardDto = {
  id: 1,
  owner: 'ferrous-oss',
  name: 'cargo-lens',
  url: 'https://github.com/ferrous-oss/cargo-lens',
  primaryLanguage: 'Rust',
  starCount: 1284,
  forkCount: 210,
  licenseIdentifier: 'Apache-2.0',
  topics: ['rust', 'cargo', 'build-tools'],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'Visualizes cargo dependency graphs and feature flags.',
  detailedSummaryContent:
    'A thorough walkthrough of cargo-lens: it renders interactive dependency graphs for any Rust ' +
    'crate, including feature-flag combinations, and highlights version conflicts before they hit CI.',
  isBookmarked: false,
};

const scoreBreakdown: ScoreBreakdownDto = {
  hasLicense: true,
  licenseType: 'Apache-2.0',
  licenseWeight: 0.18,
  commitsPerWeek: 14,
  commitsPerWeekWeight: 0.27,
  contributorCount: 22,
  contributorCountWeight: 0.225,
  forkCount: 210,
  forkCountWeight: 0.225,
  starCount: 1284,
  starCountWeight: 0.1,
  totalScore: 84,
};

describe('RepositoryDetailPane', () => {
  let fixture: ComponentFixture<RepositoryDetailPane>;
  let dialogRefClose: ReturnType<typeof vi.fn>;

  // Data is fixed for the dialog's whole lifetime (injected via MAT_DIALOG_DATA, not a template
  // input), so each test builds its own TestBed/fixture rather than mutating inputs on a shared one.
  async function createFixture(data: RepositoryDetailDialogData): Promise<void> {
    dialogRefClose = vi.fn();

    await TestBed.configureTestingModule({
      imports: [RepositoryDetailPane],
      providers: [
        provideNoopAnimations(),
        {
          provide: BookmarkApiService,
          useValue: { addBookmark: vi.fn(), removeBookmark: vi.fn() },
        },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: dialogRefClose } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RepositoryDetailPane);
  }

  it('renders the detailed summary (not the short card summary), topics, and chips for the given item', async () => {
    await createFixture({ item: baseRepository, scoreBreakdown: null, trendGrowth: null });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('cargo-lens');
    expect(el.textContent).toContain('ferrous-oss');
    expect(el.textContent).toContain(baseRepository.detailedSummaryContent);
    expect(el.querySelector('.repo-detail__summary')?.textContent).not.toContain(
      'Visualizes cargo dependency graphs and feature flags.',
    );
    expect(el.textContent).toContain('Rust');
    expect(el.textContent).toContain('Apache-2.0');
    expect(el.textContent).toContain('1284');

    const topicChips = [...el.querySelectorAll('.repo-detail__topic-chip')].map((c) =>
      c.textContent?.trim(),
    );
    expect(topicChips).toEqual(['rust', 'cargo', 'build-tools']);
  });

  it('renders the "Summary pending" placeholder when detailedSummaryContent is null', async () => {
    await createFixture({
      item: { ...baseRepository, detailedSummaryContent: null },
      scoreBreakdown: null,
      trendGrowth: null,
    });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Summary pending');
  });

  it('omits the score footer when no scoreBreakdown is provided', async () => {
    await createFixture({ item: baseRepository, scoreBreakdown: null, trendGrowth: null });
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.repo-detail__score-footer'),
    ).toBeNull();
  });

  it('renders the score footer when scoreBreakdown is provided', async () => {
    await createFixture({ item: baseRepository, scoreBreakdown, trendGrowth: null });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.repo-detail__score-value')?.textContent?.trim()).toBe('84');
    expect(el.textContent).toContain('Commits/week');
  });

  it('closes the dialog when the close button is clicked', async () => {
    await createFixture({ item: baseRepository, scoreBreakdown: null, trendGrowth: null });
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement)
      .querySelector('.repo-detail__close')
      ?.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(dialogRefClose).toHaveBeenCalledTimes(1);
  });
});
