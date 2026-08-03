import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { RepositoryCardDto, ScoreBreakdownDto } from '../../../core/models/repository.model';
import { RepositoryDetailPane } from './repository-detail-pane';

const baseRepository: RepositoryCardDto = {
  id: 1,
  owner: 'ferrous-oss',
  name: 'cargo-lens',
  url: 'https://github.com/ferrous-oss/cargo-lens',
  primaryLanguage: 'Rust',
  starCount: 1284,
  forkCount: 210,
  licenseIdentifier: 'Apache-2.0',
  licenseName: 'Apache License 2.0',
  topics: ['rust', 'cargo', 'build-tools'],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'Visualizes cargo dependency graphs and feature flags.',
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

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RepositoryDetailPane],
      providers: [
        provideNoopAnimations(),
        {
          provide: BookmarkApiService,
          useValue: { addBookmark: vi.fn(), removeBookmark: vi.fn() },
        },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RepositoryDetailPane);
  });

  it('renders nothing when there is no item', () => {
    fixture.componentRef.setInput('item', null);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.repo-detail')).toBeNull();
  });

  it('renders the full summary, topics, and chips for the given item', () => {
    fixture.componentRef.setInput('item', baseRepository);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('cargo-lens');
    expect(el.textContent).toContain('ferrous-oss');
    expect(el.textContent).toContain('Visualizes cargo dependency graphs and feature flags.');
    expect(el.textContent).toContain('Rust');
    expect(el.textContent).toContain('Apache-2.0');
    expect(el.textContent).toContain('1284');

    const topicChips = [...el.querySelectorAll('.repo-detail__topic-chip')].map((c) =>
      c.textContent?.trim(),
    );
    expect(topicChips).toEqual(['rust', 'cargo', 'build-tools']);
  });

  it('renders the "Summary pending" placeholder when summaryContent is null', () => {
    fixture.componentRef.setInput('item', { ...baseRepository, summaryContent: null });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Summary pending');
  });

  it('omits the score footer when no scoreBreakdown is provided, renders it when one is', () => {
    fixture.componentRef.setInput('item', baseRepository);
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.repo-detail__score-footer'),
    ).toBeNull();

    fixture.componentRef.setInput('scoreBreakdown', scoreBreakdown);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.repo-detail__score-value')?.textContent?.trim()).toBe('84');
    expect(el.textContent).toContain('Commits/week');
  });

  it('emits closed when the close button is clicked', () => {
    fixture.componentRef.setInput('item', baseRepository);
    fixture.detectChanges();

    const emitted = vi.fn();
    fixture.componentInstance.closed.subscribe(emitted);

    (fixture.nativeElement as HTMLElement).querySelector('.repo-detail__close')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );

    expect(emitted).toHaveBeenCalledTimes(1);
  });
});
