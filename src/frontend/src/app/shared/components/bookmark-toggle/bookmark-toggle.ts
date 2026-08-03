import { Component, inject, input, linkedSignal, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';

import { BookmarkApiService } from '../../../core/api/bookmark-api.service';

// FR-007's create/delete collapsed into one idempotent toggle (dashboard-ux-brief.md §6.1). A
// standalone component keyed only on repositoryId/isBookmarked, used by the repository card (Hidden
// Gems - the dedicated Bookmarks view used it too before being removed as a standalone view, see the
// changelog entry for that removal) rather than living inline inside it.
@Component({
  selector: 'app-bookmark-toggle',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  templateUrl: './bookmark-toggle.html',
  styleUrl: './bookmark-toggle.scss',
})
export class BookmarkToggle {
  private readonly bookmarkApi = inject(BookmarkApiService);
  private readonly snackBar = inject(MatSnackBar);

  readonly repositoryId = input.required<number>();
  readonly initialBookmarked = input.required<boolean>();
  readonly bookmarkedChange = output<boolean>();

  // linkedSignal resets to initialBookmarked() whenever a new repository/page swaps this input in,
  // while still allowing purely-local optimistic mutation via .set() in between those resets.
  protected readonly bookmarked = linkedSignal(() => this.initialBookmarked());
  protected readonly pending = signal(false);

  protected toggle(): void {
    this.apply(!this.bookmarked(), true);
  }

  // announce=false is used by the Undo action itself, which reverses a prior toggle silently
  // (it already showed its own confirmation the first time round) rather than chaining snack-bars.
  private apply(next: boolean, announce: boolean): void {
    const previous = this.bookmarked();
    this.bookmarked.set(next);
    this.bookmarkedChange.emit(next);
    this.pending.set(true);

    // Typed as Observable<unknown> - addBookmark/removeBookmark return different generic Observable
    // types (BookmarkDto vs void), and this handler only cares about success/failure, not the
    // payload, so unifying the type here avoids TS being unable to resolve subscribe()'s overloads
    // across a union of two distinct Observable<T> types.
    const request$: Observable<unknown> = next
      ? this.bookmarkApi.addBookmark(this.repositoryId())
      : this.bookmarkApi.removeBookmark(this.repositoryId());

    request$.subscribe({
      next: () => {
        this.pending.set(false);
        if (announce) {
          this.showConfirmation(next, previous);
        }
      },
      error: () => {
        this.pending.set(false);
        this.bookmarked.set(previous);
        this.bookmarkedChange.emit(previous);
        this.showError(next);
      },
    });
  }

  private showConfirmation(next: boolean, previous: boolean): void {
    const message = next ? 'Added to bookmarks' : 'Removed from bookmarks';
    const ref = this.snackBar.open(message, 'Undo', { duration: 4000, panelClass: 'app-snackbar' });
    ref.onAction().subscribe(() => this.apply(previous, false));
  }

  private showError(attempted: boolean): void {
    const ref = this.snackBar.open("Couldn't save bookmark — try again", 'Retry', {
      duration: 6000,
      panelClass: 'app-snackbar-error',
    });
    ref.onAction().subscribe(() => this.apply(attempted, true));
  }
}
