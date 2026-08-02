import { BreakpointObserver } from '@angular/cdk/layout';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject } from 'rxjs';

import { FilterSortBar, FilterSortState } from './filter-sort-bar';

// White-box access to FilterSortBar's protected surface for testing purposes - the component
// intentionally keeps these `protected` (template-only) rather than public API, so tests reach them
// through this narrow, explicitly-typed view instead of a broad `any` cast.
interface FilterSortBarInternals {
  onLanguageSelectionChange(languages: string[]): void;
  onLicenseSelectionChange(licenses: string[]): void;
  setSort(sort: FilterSortState['sort']): void;
  toggleDirection(): void;
  addTopic(topic: string): void;
  removeTopic(topic: string): void;
  onBookmarkedOnlyChange(value: boolean): void;
  clearAll(): void;
  activeChips(): { key: string; label: string; removable: boolean }[];
  hasRemovableFilters(): boolean;
  filtersSheetOpen: () => boolean;
  stateChange: { subscribe: (cb: (state: FilterSortState) => void) => void };
}

describe('FilterSortBar', () => {
  let fixture: ComponentFixture<FilterSortBar>;
  let component: FilterSortBarInternals;
  let emissions: FilterSortState[];
  // Drives the mocked BreakpointObserver so tests can force wide vs. narrow (<=960px) layout -
  // jsdom has no real matchMedia, so the CDK BreakpointObserver is always mocked here, same pattern
  // app.spec.ts already uses for the primary nav's own breakpoint collapse.
  let breakpointMatches: BehaviorSubject<{ matches: boolean }>;

  function setUp(forcedCategory: string | null = null): void {
    fixture = TestBed.createComponent(FilterSortBar);
    component = fixture.componentInstance as unknown as FilterSortBarInternals;
    fixture.componentRef.setInput('defaultSort', 'Newest');
    fixture.componentRef.setInput('defaultDirection', 'Desc');
    fixture.componentRef.setInput('languageOptions', ['TypeScript', 'Rust']);
    fixture.componentRef.setInput('licenseOptions', ['MIT', 'Apache-2.0']);
    fixture.componentRef.setInput('forcedCategory', forcedCategory);
    emissions = [];
    component.stateChange.subscribe((s: FilterSortState) => emissions.push(s));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    breakpointMatches = new BehaviorSubject<{ matches: boolean }>({ matches: false });

    await TestBed.configureTestingModule({
      imports: [FilterSortBar],
      providers: [
        provideNoopAnimations(),
        {
          provide: BreakpointObserver,
          useValue: { observe: () => breakpointMatches.asObservable() },
        },
      ],
    }).compileComponents();
  });

  it('emits the outgoing filter state when a language is selected', () => {
    setUp();

    component.onLanguageSelectionChange(['TypeScript']);

    expect(emissions.at(-1)).toEqual(
      expect.objectContaining({ language: ['TypeScript'], sort: 'Newest', direction: 'Desc' }),
    );
  });

  it('emits the changed sort field when the sort toggle changes', () => {
    setUp();

    component.setSort('Score');

    expect(emissions.at(-1)?.sort).toBe('Score');
  });

  it('flips and emits the sort direction', () => {
    setUp();

    component.toggleDirection();

    expect(emissions.at(-1)?.direction).toBe('Asc');
  });

  it('adds and removes a topic', () => {
    setUp();

    component.addTopic('mcp-servers');
    expect(emissions.at(-1)?.topic).toEqual(['mcp-servers']);

    component.removeTopic('mcp-servers');
    expect(emissions.at(-1)?.topic).toEqual([]);
  });

  it('clearAll resets facets and bookmarkedOnly but leaves sort/direction untouched', () => {
    setUp();
    component.onLanguageSelectionChange(['TypeScript']);
    component.onLicenseSelectionChange(['MIT']);
    component.setSort('Stars');
    component.onBookmarkedOnlyChange(true);

    component.clearAll();

    const last = emissions.at(-1)!;
    expect(last.language).toEqual([]);
    expect(last.license).toEqual([]);
    expect(last.sort).toBe('Stars');
    expect(last.bookmarkedOnly).toBe(false);
  });

  it('seeds the forced category as a pinned, non-removable chip and keeps it through clearAll', () => {
    setUp('Rust');

    const chips = component.activeChips();
    expect(chips).toEqual([
      expect.objectContaining({ key: 'category', label: 'category: Rust', removable: false }),
    ]);
    expect(component.hasRemovableFilters()).toBe(false);

    component.clearAll();
    expect(emissions.at(-1)?.language).toEqual(['Rust']);
  });

  it('shows the inline facets/sort row on wide viewports and no "Filters · N" trigger', () => {
    setUp();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.filter-bar__row')).toBeTruthy();
    expect(el.querySelector('.filter-bar__filters-trigger')).toBeNull();
  });

  it('collapses to a "Filters · N" trigger below the 960px breakpoint, opening a mat-sidenav with the same controls, while active chips stay inline', () => {
    setUp();
    component.onLanguageSelectionChange(['TypeScript']);
    component.onLicenseSelectionChange(['MIT']);

    breakpointMatches.next({ matches: true });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;

    // Inline facets/sort row are gone; the trigger reflects the active filter count. Scoped with
    // :not(...) rather than a bare '.filter-bar__facets' selector because the sidenav's own facets
    // container deliberately reuses that base BEM class (with a --sidenav modifier) for shared flex
    // styling (filter-sort-bar.scss) - a bare selector would also match that legitimate element.
    expect(el.querySelector('.filter-bar__facets:not(.filter-bar__facets--sidenav)')).toBeNull();
    const trigger = el.querySelector('.filter-bar__filters-trigger');
    expect(trigger?.textContent).toContain('Filters · 2');

    // Active chips stay inline next to the trigger regardless of collapse.
    expect(el.querySelector('.filter-bar__active-chips')).toBeTruthy();
    expect(el.textContent).toContain('TypeScript');
    expect(el.textContent).toContain('MIT');

    // The sidenav exists (closed) and carries the same facet/sort controls.
    expect(el.querySelector('mat-sidenav-container')).toBeTruthy();
    expect(el.querySelector('.filter-bar__facets--sidenav mat-select')).toBeTruthy();

    // Opening it flips the sidenav's `opened` state.
    (trigger as HTMLButtonElement).dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(component.filtersSheetOpen()).toBe(true);
  });
});
