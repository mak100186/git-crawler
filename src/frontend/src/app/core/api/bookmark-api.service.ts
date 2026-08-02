import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { BookmarkDto } from '../models/bookmark.model';

// POST/DELETE are both idempotent server-side (F-010) - safe to call repeatedly without checking
// current state first, which is what makes the optimistic toggle + Undo flow (bookmark-toggle
// component) safe to implement as "just call the target-state endpoint".
@Injectable({ providedIn: 'root' })
export class BookmarkApiService {
  private readonly http = inject(HttpClient);

  addBookmark(repositoryId: number): Observable<BookmarkDto> {
    return this.http.post<BookmarkDto>(`/api/repositories/${repositoryId}/bookmark`, null);
  }

  removeBookmark(repositoryId: number): Observable<void> {
    return this.http.delete<void>(`/api/repositories/${repositoryId}/bookmark`);
  }
}
