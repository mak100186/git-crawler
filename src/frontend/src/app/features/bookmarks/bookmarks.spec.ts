import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatSnackBar, MatSnackBarRef } from '@angular/material/snack-bar';
import { Subject, of, throwError } from 'rxjs';

import { BookmarkApiService } from '../../core/api/bookmark-api.service';
import { RepositoryCardDto } from '../../core/models/repository.model';
import { Bookmarks } from './bookmarks';

function repo(id: number, name: string): RepositoryCardDto {
  return {
    id,
    owner: 'octocat',
    name,
    url: `https://github.com/octocat/${name}`,
    primaryLanguage: 'TypeScript',
    starCount: 42,
    forkCount: 5,
    licenseIdentifier: 'MIT',
    licenseName: 'MIT License',
    topics: [],
    firstDiscoveredAtUtc: new Date().toISOString(),
    summaryContent: 'A tool.',
    isBookmarked: true,
  };
}

const repoA = repo(1, 'repo-a');
const repoB = repo(2, 'repo-b');

describe('Bookmarks', () => {
  let fixture: ComponentFixture<Bookmarks>;
  let bookmarkApi: {
    listBookmarks: ReturnType<typeof vi.fn>;
    addBookmark: ReturnType<typeof vi.fn>;
    removeBookmark: ReturnType<typeof vi.fn>;
  };
  let snackBarOpen: ReturnType<typeof vi.fn>;
  let undoAction: Subject<void>;

  beforeEach(async () => {
    bookmarkApi = { listBookmarks: vi.fn(), addBookmark: vi.fn(), removeBookmark: vi.fn() };
    undoAction = new Subject<void>();
    // Matches bookmark-toggle.spec.ts's own mock shape - onAction() only emits once this test
    // explicitly pushes through `undoAction`, mirroring an actual snack-bar Undo click.
    snackBarOpen = vi.fn().mockReturnValue({
      onAction: () => undoAction.asObservable(),
    } as unknown as MatSnackBarRef<unknown>);

    await TestBed.configureTestingModule({
      imports: [Bookmarks],
      providers: [
        provideNoopAnimations(),
        { provide: BookmarkApiService, useValue: bookmarkApi },
        { provide: MatSnackBar, useValue: { open: snackBarOpen } },
      ],
    }).compileComponents();
  });

  it('fetches and renders bookmarked repos on init (TC-012-01)', () => {
    bookmarkApi.listBookmarks.mockReturnValue(of({ repositories: [repoA, repoB] }));

    fixture = TestBed.createComponent(Bookmarks);
    fixture.detectChanges();

    expect(bookmarkApi.listBookmarks).toHaveBeenCalled();
    const content = (fixture.nativeElement as HTMLElement).textContent;
    expect(content).toContain('repo-a');
    expect(content).toContain('repo-b');
  });

  it('renders bookmarks-specific empty-state copy when there are zero bookmarks (TC-012-03)', () => {
    bookmarkApi.listBookmarks.mockReturnValue(of({ repositories: [] }));

    fixture = TestBed.createComponent(Bookmarks);
    fixture.detectChanges();

    const content = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(content).toContain("You haven't bookmarked any repositories yet");
    expect(content).not.toContain('No repositories match these filters');
  });

  it('removes a card once its toggle reaches the off state, and Undo restores it (TC-012-04)', () => {
    bookmarkApi.listBookmarks.mockReturnValue(of({ repositories: [repoA, repoB] }));
    bookmarkApi.removeBookmark.mockReturnValue(of(undefined));
    bookmarkApi.addBookmark.mockReturnValue(
      of({ id: 9, repositoryId: repoA.id, createdAtUtc: '2026-01-01' }),
    );

    fixture = TestBed.createComponent(Bookmarks);
    fixture.detectChanges();

    const toggleButtons = () =>
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        'button.bookmark-toggle',
      );
    expect(toggleButtons().length).toBe(2);

    toggleButtons()[0].dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(bookmarkApi.removeBookmark).toHaveBeenCalledWith(repoA.id);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('repo-a');
    expect(toggleButtons().length).toBe(1);

    undoAction.next();
    fixture.detectChanges();

    expect(bookmarkApi.addBookmark).toHaveBeenCalledWith(repoA.id);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('repo-a');
    expect(toggleButtons().length).toBe(2);
  });

  it('shows the error state with a working Retry button on fetch failure (TC-012-06)', () => {
    bookmarkApi.listBookmarks.mockReturnValueOnce(throwError(() => new Error('network error')));

    fixture = TestBed.createComponent(Bookmarks);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Something went wrong');

    bookmarkApi.listBookmarks.mockReturnValueOnce(of({ repositories: [repoA] }));
    const retryButton = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    retryButton.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(bookmarkApi.listBookmarks).toHaveBeenCalledTimes(2);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('repo-a');
  });
});
