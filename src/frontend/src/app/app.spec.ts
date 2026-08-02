import { BreakpointObserver } from '@angular/cdk/layout';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { App } from './app';

describe('App', () => {
  async function setUp(narrow: boolean) {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        {
          provide: BreakpointObserver,
          useValue: { observe: () => of({ matches: narrow, breakpoints: {} }) },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the brand and the four primary nav entries in a mat-toolbar on wide viewports', async () => {
    const fixture = await setUp(false);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('mat-toolbar')?.textContent).toContain('GitCrawler');
    const navLinks = [...el.querySelectorAll('.app-toolbar__nav-link')].map((n) =>
      n.textContent?.trim(),
    );
    expect(navLinks).toEqual(['Discovery Feed', 'Hidden Gems', 'Trending', 'Categories']);
  });

  it('renders the reserved, inert Bookmarks and Search placeholders', async () => {
    const fixture = await setUp(false);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Bookmarks · F-012');
    expect(el.textContent).toContain('Search (v2)');
  });

  it('collapses the top nav and shows a bottom pill nav below the 960px breakpoint, built from real mat-icon-button links', async () => {
    const fixture = await setUp(true);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.app-toolbar__nav')).toBeNull();
    const bottomLinks = [...el.querySelectorAll('.app-bottom-nav__link')];
    expect(bottomLinks.length).toBe(4);

    // Every link is an <a mat-icon-button> - Angular Material's own base class on the host element
    // (shared by every mat-*-button variant), not a bare anchor with hand-rolled CSS mimicking one.
    for (const link of bottomLinks) {
      expect(link.tagName.toLowerCase()).toBe('a');
      expect(link.classList.contains('mat-mdc-button-base')).toBe(true);
      expect(link.querySelector('mat-icon')).toBeTruthy();
    }
  });
});
