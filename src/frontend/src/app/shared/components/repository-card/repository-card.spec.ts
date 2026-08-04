import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { HiddenGemCardDto, RepositoryCardDto } from '../../../core/models/repository.model';
import { RepositoryCard } from './repository-card';

const baseRepository: RepositoryCardDto = {
  id: 1,
  owner: 'octocat',
  name: 'hello-world',
  url: 'https://github.com/octocat/hello-world',
  primaryLanguage: 'TypeScript',
  starCount: 42,
  forkCount: 5,
  licenseIdentifier: 'MIT',
  licenseName: 'MIT License',
  topics: ['cli', 'tools'],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'A small, well-maintained CLI tool.',
  isBookmarked: false,
};

describe('RepositoryCard', () => {
  let fixture: ComponentFixture<RepositoryCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RepositoryCard],
      providers: [
        provideNoopAnimations(),
        {
          provide: BookmarkApiService,
          useValue: {
            addBookmark: vi.fn(() => of({})),
            removeBookmark: vi.fn(() => of(undefined)),
          },
        },
        { provide: MatSnackBar, useValue: { open: vi.fn(() => ({ onAction: () => of(undefined) })) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RepositoryCard);
  });

  it('renders the base card content, with star count in the subtitle', () => {
    fixture.componentRef.setInput('repository', baseRepository);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('hello-world');
    expect(el.textContent).toContain('octocat');
    expect(el.textContent).toContain('TypeScript');
    expect(el.textContent).toContain('MIT');
    expect(el.querySelector('.repo-card__score-badge')).toBeNull();
    expect(el.querySelector('.repo-card__subtitle')?.textContent).toContain('42');
  });

  it('renders the "Summary pending" placeholder when summaryContent is null', () => {
    fixture.componentRef.setInput('repository', { ...baseRepository, summaryContent: null });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Summary pending');
    expect(el.querySelector('.repo-card__summary')).toBeNull();
  });

  it('renders the hidden-gem score badge when scoreBreakdown is set, with no "Why this score?" panel', () => {
    const hiddenGem: HiddenGemCardDto = {
      ...baseRepository,
      trendGrowth: null,
      scoreBreakdown: {
        hasLicense: true,
        licenseType: 'MIT',
        licenseWeight: 0.18,
        commitsPerWeek: 3.5,
        commitsPerWeekWeight: 0.27,
        contributorCount: 4,
        contributorCountWeight: 0.225,
        forkCount: 5,
        forkCountWeight: 0.225,
        starCount: 42,
        starCountWeight: 0.1,
        totalScore: 61.4,
      },
    };

    fixture.componentRef.setInput('repository', hiddenGem);
    fixture.componentRef.setInput('scoreBreakdown', hiddenGem.scoreBreakdown);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.repo-card__score-badge')?.textContent?.trim()).toBe('61');
    // The full breakdown now lives in RepositoryDetailPane's own always-expanded footer instead
    // (opened by the same card click) - the card itself no longer duplicates it.
    expect(el.textContent).not.toContain('Why this score?');
    expect(el.textContent).not.toContain('Commits/week');
  });

  it('emits cardClick when the card body is clicked, opening the detail pane (design brief §09)', () => {
    fixture.componentRef.setInput('repository', baseRepository);
    fixture.detectChanges();

    const emitted = vi.fn();
    fixture.componentInstance.cardClick.subscribe(emitted);

    (fixture.nativeElement as HTMLElement).querySelector('mat-card')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );

    expect(emitted).toHaveBeenCalledTimes(1);
  });

  it('does not emit cardClick when the bookmark toggle or GitHub link is clicked', () => {
    fixture.componentRef.setInput('repository', baseRepository);
    fixture.detectChanges();

    const emitted = vi.fn();
    fixture.componentInstance.cardClick.subscribe(emitted);

    const el = fixture.nativeElement as HTMLElement;
    el.querySelector('app-bookmark-toggle button')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );
    el.querySelector('.repo-card__github-link')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );

    expect(emitted).not.toHaveBeenCalled();
  });
});
