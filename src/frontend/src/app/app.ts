import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule, MatIconRegistry } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { DomSanitizer } from '@angular/platform-browser';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

import { registerAppIcons } from './core/icons/icon-registry.service';

interface NavEntry {
  label: string;
  path: string;
  icon: string;
}

// FR-009's required view. The "Categories" entry was removed - browsing by category is done via
// Hidden Gems' existing Language filter instead (see the changelog entry for this removal). The
// "Trending" entry was also removed and merged into Hidden Gems - each card there now shows its own
// category's trend growth directly, so a separate Trending view is no longer needed to see the same
// information (see the changelog entry for this removal too). The "Discovery Feed" entry was removed
// as well - it offered no meaningfully distinct browsing experience over Hidden Gems once
// Categories/Trending had already folded into it (see the changelog entry for this removal). F-012's
// "Bookmarks" entry was removed last - Hidden Gems' existing "Bookmarked only" filter already
// surfaces the same repos, so a separate view/nav entry added nothing (see the changelog entry for
// this removal). A single-entry nav array reads oddly, but keeping the same NAV_ENTRIES-driven
// rendering rather than special-casing "just show the brand" avoids two code paths for what's really
// the same shell.
const NAV_ENTRIES: NavEntry[] = [{ label: 'Hidden Gems', path: '/hidden-gems', icon: 'gem' }];

// App shell (dashboard-handoff.md §2): ink-900 mat-toolbar with the primary nav, plus an inert
// reserved slot for the v2 "Search" field so the shell doesn't reflow when that lands (F-012's own
// "Bookmarks · F-012" reserved slot is gone now that this is a live nav entry, per F-012's own
// scope). Collapses to a floating bottom pill nav below the 960px breakpoint via CDK
// BreakpointObserver.
@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly breakpointObserver = inject(BreakpointObserver);

  protected readonly title = 'GitHub Hidden Gems Discovery Platform';
  protected readonly navEntries = NAV_ENTRIES;

  protected readonly isNarrow = toSignal(
    this.breakpointObserver.observe('(max-width: 960px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  constructor() {
    const iconRegistry = inject(MatIconRegistry);
    const sanitizer = inject(DomSanitizer);
    registerAppIcons(iconRegistry, sanitizer);
  }
}
