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

// FR-009's four required views, fixed order (dashboard-ux-brief.md §3 / dashboard-handoff.md §2).
const NAV_ENTRIES: NavEntry[] = [
  { label: 'Discovery Feed', path: '/discovery-feed', icon: 'home' },
  { label: 'Hidden Gems', path: '/hidden-gems', icon: 'gem' },
  { label: 'Trending', path: '/trending', icon: 'trending-up' },
  { label: 'Categories', path: '/categories', icon: 'layers' },
];

// App shell (dashboard-handoff.md §2): ink-900 mat-toolbar with the four-entry primary nav, plus
// inert reserved slots for F-012's "Bookmarks" nav pill and the v2 "Search" field so the shell
// doesn't reflow when those land. Collapses to a floating bottom pill nav below the 960px
// breakpoint via CDK BreakpointObserver.
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
