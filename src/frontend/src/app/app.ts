import { Component, inject } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { DomSanitizer } from '@angular/platform-browser';
import { RouterOutlet } from '@angular/router';

import { registerAppIcons } from './core/icons/icon-registry.service';

// App shell (dashboard-handoff.md §2): ink-900 mat-toolbar with the brand and an inert reserved
// slot for the v2 "Search" field so the shell doesn't reflow when that lands. The primary/bottom-nav
// (originally rendering NAV_ENTRIES, down to a single "Hidden Gems" entry after Categories/
// Trending/Discovery Feed/Bookmarks were each folded away in turn - see the changelog entries for
// those removals) was removed entirely per operator direction: with exactly one page left, a tab
// list with nothing else to switch between was pure chrome. Hidden Gems is still reached via the
// router's own default redirect (app.routes.ts's `'' -> 'hidden-gems'`), just with no visible nav
// control driving it - the CDK BreakpointObserver-driven responsive collapse this used to need
// (desktop top nav vs. mobile bottom pill nav) went with it, since there's no nav left to collapse.
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, MatToolbarModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = 'GitHub Hidden Gems Discovery Platform';

  constructor() {
    const iconRegistry = inject(MatIconRegistry);
    const sanitizer = inject(DomSanitizer);
    registerAppIcons(iconRegistry, sanitizer);
  }
}
