import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSnackBar, MatSnackBarRef } from '@angular/material/snack-bar';
import { EMPTY, of, throwError } from 'rxjs';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';
import { TestIconRegistry } from '../../../core/icons/test-icon-registry';
import { BookmarkToggle } from './bookmark-toggle';

describe('BookmarkToggle', () => {
  let fixture: ComponentFixture<BookmarkToggle>;
  let bookmarkApi: {
    addBookmark: ReturnType<typeof vi.fn>;
    removeBookmark: ReturnType<typeof vi.fn>;
  };
  let snackBarOpen: ReturnType<typeof vi.fn>;

  function setUp(initialBookmarked: boolean): void {
    fixture = TestBed.createComponent(BookmarkToggle);
    fixture.componentRef.setInput('repositoryId', 42);
    fixture.componentRef.setInput('initialBookmarked', initialBookmarked);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    bookmarkApi = { addBookmark: vi.fn(), removeBookmark: vi.fn() };
    // onAction() must NOT emit synchronously here: in real usage it only fires once the user
    // actually clicks the snack-bar's Undo/Retry action, not the instant the snack-bar opens. Using
    // `of(undefined)` made every `.subscribe()` on it fire immediately, silently chaining an
    // undo/retry call into the same synchronous flow as the initial toggle - which crashed with
    // "Cannot read properties of undefined (reading 'subscribe')" in tests that never mocked the
    // opposite addBookmark/removeBookmark call that chained call requires. EMPTY completes without
    // emitting, matching "no action taken yet".
    snackBarOpen = vi.fn().mockReturnValue({
      onAction: () => EMPTY,
    } as unknown as MatSnackBarRef<unknown>);

    await TestBed.configureTestingModule({
      imports: [BookmarkToggle],
      providers: [
        { provide: BookmarkApiService, useValue: bookmarkApi },
        { provide: MatSnackBar, useValue: { open: snackBarOpen } },
        TestIconRegistry,
      ],
    }).compileComponents();
  });

  it('flips optimistically on click and confirms with an Undo snack-bar on success', () => {
    bookmarkApi.addBookmark.mockReturnValue(
      of({ id: 1, repositoryId: 42, createdAtUtc: '2026-01-01' }),
    );
    setUp(false);

    (fixture.nativeElement as HTMLElement)
      .querySelector('button')!
      .dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(bookmarkApi.addBookmark).toHaveBeenCalledWith(42);
    expect(snackBarOpen).toHaveBeenCalledWith('Added to bookmarks', 'Undo', expect.any(Object));
  });

  it('reverts the icon state and shows an error snack-bar with Retry on a failed write', () => {
    bookmarkApi.addBookmark.mockReturnValue(throwError(() => new Error('network error')));
    setUp(false);

    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    button.dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(button.getAttribute('aria-pressed')).toBe('false');
    expect(snackBarOpen).toHaveBeenCalledWith(
      "Couldn't save bookmark — try again",
      'Retry',
      expect.any(Object),
    );
  });

  it('calls removeBookmark when toggled from an already-bookmarked state', () => {
    bookmarkApi.removeBookmark.mockReturnValue(of(undefined));
    setUp(true);

    (fixture.nativeElement as HTMLElement)
      .querySelector('button')!
      .dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(bookmarkApi.removeBookmark).toHaveBeenCalledWith(42);
  });
});
