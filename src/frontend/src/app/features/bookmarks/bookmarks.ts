import { Component, DestroyRef, Injectable, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { Observable, Subject, tap } from 'rxjs';

import { BookmarkApiService } from '../../core/api/bookmark-api.service';
import { BookmarkDto } from '../../core/models/bookmark.model';
import { RepositoryCardDto } from '../../core/models/repository.model';
import { RepositoryGrid } from '../../shared/components/repository-grid/repository-grid';

interface BookmarkChange {
  repositoryId: number;
  bookmarked: boolean;
}

// BookmarkToggle (nested two levels down inside RepositoryGrid -> RepositoryCard) is a Done component
// this feature only consumes (Task Packet Constraints) - RepositoryCard never forwards its
// `bookmarkedChange` output, so there's no @Output on the path from here down to bind to. Rather than
// touching any of BookmarkToggle/RepositoryCard/RepositoryGrid to add one, this taps the shared
// BookmarkApiService token itself via a component-scoped DI override (see Bookmarks' `providers`
// below - only this view's own subtree gets it, every other view keeps injecting the real root
// service untouched). `skipSelf` makes it delegate every call to whatever's provided one level up -
// the real HTTP-backed service in production, or a spec's mocked BookmarkApiService in tests - so
// this stays testable via the exact `{ provide: BookmarkApiService, useValue: mock }` pattern every
// other spec in this codebase already uses, while also reporting each *confirmed* add/remove over
// `changes$` for this view to react to.
@Injectable()
class BookmarkChangeApiService {
  private readonly inner = inject(BookmarkApiService, { skipSelf: true });
  private readonly changesSubject = new Subject<BookmarkChange>();
  readonly changes$ = this.changesSubject.asObservable();

  listBookmarks(): Observable<{ repositories: RepositoryCardDto[] }> {
    return this.inner.listBookmarks();
  }

  addBookmark(repositoryId: number): Observable<BookmarkDto> {
    return this.inner
      .addBookmark(repositoryId)
      .pipe(tap(() => this.changesSubject.next({ repositoryId, bookmarked: true })));
  }

  removeBookmark(repositoryId: number): Observable<void> {
    return this.inner
      .removeBookmark(repositoryId)
      .pipe(tap(() => this.changesSubject.next({ repositoryId, bookmarked: false })));
  }
}

// Dedicated Bookmarks view (F-012) - the second half of FR-007's acceptance criterion; bookmarking
// itself (toggle + persistence) shipped with F-010/F-011. Reuses RepositoryGrid's Loading/Error/
// Populated states exactly as every other view does; only the Empty state is rendered locally, since
// RepositoryGrid's own empty copy ("No repositories match these filters") is filter-bar-specific and
// this view has no filter bar to clear (TC-012-03) - RepositoryGrid itself is left untouched either
// way, per Constraints.
@Component({
  selector: 'app-bookmarks',
  imports: [MatCardModule, MatIconModule, RepositoryGrid],
  templateUrl: './bookmarks.html',
  styleUrl: './bookmarks.scss',
  providers: [
    BookmarkChangeApiService,
    { provide: BookmarkApiService, useExisting: BookmarkChangeApiService },
  ],
})
export class Bookmarks implements OnInit {
  private readonly bookmarkApi = inject(BookmarkApiService);
  private readonly changeTracking = inject(BookmarkChangeApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly allItems = signal<RepositoryCardDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  // Cards toggled off from this view but not (yet, or ever) undone - filtered out of `items` below
  // rather than spliced out of `allItems`, so Undo can restore a card at its original position
  // without a second fetch.
  private readonly removedIds = signal<ReadonlySet<number>>(new Set());

  // GET /api/bookmarks returns the full list already ordered most-recently-bookmarked-first, with no
  // pagination parameters (Task Packet Dependencies) - `items` only re-filters that same array
  // locally for toggled-off cards, it never re-sorts it.
  protected readonly items = computed(() =>
    this.allItems().filter((item) => !this.removedIds().has(item.id)),
  );
  protected readonly isEmptyState = computed(
    () => !this.loading() && !this.error() && this.items().length === 0,
  );
  // Paginator inputs sized to the full (already-filtered) list keep mat-paginator present but
  // functionally a single, un-clickable page (Task Packet Constraints, option (a)) - simplest choice
  // given the API accepts no pagination parameters to slice server-side in the first place.
  protected readonly pageSize = computed(() => Math.max(this.items().length, 1));

  ngOnInit(): void {
    this.fetch();

    this.changeTracking.changes$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((change) => {
      this.removedIds.update((ids) => {
        const next = new Set(ids);
        if (change.bookmarked) {
          next.delete(change.repositoryId);
        } else {
          next.add(change.repositoryId);
        }
        return next;
      });
    });
  }

  protected onRetry(): void {
    this.fetch();
  }

  private fetch(): void {
    this.loading.set(true);
    this.error.set(false);
    this.removedIds.set(new Set());

    this.bookmarkApi.listBookmarks().subscribe({
      next: (result) => {
        this.allItems.set(result.repositories);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }
}
