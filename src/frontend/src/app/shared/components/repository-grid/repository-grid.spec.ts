import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar } from '@angular/material/snack-bar';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { RepositoryCardDto } from '../../../core/models/repository.model';
import { RepositoryGrid } from './repository-grid';

const repo: RepositoryCardDto = {
  id: 1,
  owner: 'octocat',
  name: 'hello-world',
  url: 'https://github.com/octocat/hello-world',
  primaryLanguage: 'TypeScript',
  starCount: 42,
  forkCount: 5,
  licenseIdentifier: 'MIT',
  licenseName: 'MIT License',
  topics: [],
  firstDiscoveredAtUtc: new Date().toISOString(),
  summaryContent: 'A tool.',
  isBookmarked: false,
};

describe('RepositoryGrid', () => {
  let fixture: ComponentFixture<RepositoryGrid>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RepositoryGrid],
      providers: [
        provideNoopAnimations(),
        {
          provide: BookmarkApiService,
          useValue: { addBookmark: vi.fn(), removeBookmark: vi.fn() },
        },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RepositoryGrid);
  });

  it('shows the loading spinner on first load (no items yet)', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading repositories');
  });

  it('shows the empty state when there are zero results and no error', () => {
    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No repositories match these filters');
  });

  it('shows the error state and emits retry on button click', () => {
    fixture.componentRef.setInput('error', true);
    fixture.componentRef.setInput('items', []);
    let retried = false;
    fixture.componentInstance.retry.subscribe(() => (retried = true));
    fixture.detectChanges();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    button.dispatchEvent(new Event('click'));

    expect(retried).toBe(true);
  });

  it('renders a populated grid with a paginator and emits pageChange', () => {
    fixture.componentRef.setInput('items', [repo]);
    fixture.componentRef.setInput('totalCount', 50);
    fixture.componentRef.setInput('page', 1);
    fixture.componentRef.setInput('pageSize', 24);
    let emitted: { page: number; pageSize: number } | undefined;
    fixture.componentInstance.pageChange.subscribe((event) => (emitted = event));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('hello-world');

    (fixture.componentInstance as unknown as { onPage: (event: unknown) => void }).onPage({
      pageIndex: 1,
      pageSize: 24,
      length: 50,
    });
    expect(emitted).toEqual({ page: 2, pageSize: 24 });
  });

  it("opens the detail drawer with the clicked card's data (design brief §09)", () => {
    fixture.componentRef.setInput('items', [repo]);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    el.querySelector('app-repository-card mat-card')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('app-repository-detail-pane')
        ?.textContent,
    ).toContain('hello-world');
  });

  it('closes the drawer when the detail pane emits closed', () => {
    fixture.componentRef.setInput('items', [repo]);
    fixture.detectChanges();

    let el = fixture.nativeElement as HTMLElement;
    el.querySelector('app-repository-card mat-card')?.dispatchEvent(
      new MouseEvent('click', { bubbles: true }),
    );
    fixture.detectChanges();

    el = fixture.nativeElement as HTMLElement;
    el
      .querySelector('.repo-detail__close')
      ?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();

    el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-repository-detail-pane .repo-detail')).toBeNull();
  });
});
