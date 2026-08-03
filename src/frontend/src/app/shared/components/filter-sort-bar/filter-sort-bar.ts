import { BreakpointObserver } from '@angular/cdk/layout';
import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
} from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSliderModule } from '@angular/material/slider';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { map } from 'rxjs';

import { RepositorySortField, SortDirection } from '../../../core/models/repository.model';

// Below this width, the inline facet controls + sort row collapse into a single "Filters · N"
// trigger opening a full-height mat-sidenav with the same controls (dashboard-handoff.md §2) -
// same 960px breakpoint app.ts already uses for the primary nav collapse.
const NARROW_BREAKPOINT = '(max-width: 960px)';

export interface FilterSortState {
  language: string[];
  minStars?: number;
  maxStars?: number;
  topic: string[];
  license: string[];
  bookmarkedOnly: boolean;
  sort: RepositorySortField;
  direction: SortDirection;
}

interface ActiveChip {
  key: string;
  label: string;
  removable: boolean;
  remove: () => void;
}

const STAR_RANGE_MAX = 25000;
const SORT_OPTIONS: RepositorySortField[] = ['Newest', 'Score', 'Stars', 'Commits'];

// The pinned category chip (Category drill-down) has no remove action by design (Constraints:
// "non-removable"/"your call, but test whichever behavior you implement" - this feature keeps the
// category fixed for the lifetime of the drill-down view rather than clearing back to the
// ungated list). A named no-op instead of an inline `() => {}` at each call site.
function noRemoveAction(): void {
  // Intentionally does nothing - see comment above.
}

// FR-004's filter/sort bar (dashboard-ux-brief.md §5, dashboard-handoff.md §3). Task Packet
// Constraints mandate the literal component per facet: Language/License = `mat-select multiple`,
// Star range = dual-thumb `mat-slider` + synced min/max `mat-form-field` inputs, Topic =
// `mat-chip-grid` + `mat-autocomplete`, Sort = `mat-button-toggle-group` + a direction
// `mat-icon-button`, "Bookmarked only" = `mat-slide-toggle`. Shown on Discovery Feed, Hidden Gems,
// and the Category drill-down; never on Trending (§3/Constraints).
@Component({
  selector: 'app-filter-sort-bar',
  imports: [
    CommonModule,
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatSelectModule,
    MatSidenavModule,
    MatSliderModule,
    MatSlideToggleModule,
  ],
  templateUrl: './filter-sort-bar.html',
  styleUrl: './filter-sort-bar.scss',
})
export class FilterSortBar {
  private readonly breakpointObserver = inject(BreakpointObserver);

  protected readonly isNarrow = toSignal(
    this.breakpointObserver.observe(NARROW_BREAKPOINT).pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly filtersSheetOpen = signal(false);

  readonly defaultSort = input.required<RepositorySortField>();
  readonly defaultDirection = input.required<SortDirection>();
  readonly showLanguageFilter = input(true);
  readonly showBookmarkedToggle = input(false);
  // When set (Category drill-down), the category is a forced, non-removable Language facet value -
  // the language mat-select is hidden and this renders as a pinned chip instead (Constraints).
  readonly forcedCategory = input<string | null>(null);
  readonly languageOptions = input<string[]>([]);
  readonly licenseOptions = input<string[]>([]);
  readonly topicOptions = input<string[]>([]);

  readonly stateChange = output<FilterSortState>();

  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly starRangeMax = STAR_RANGE_MAX;

  // mat-slider's discrete-mode thumb tooltip (bound via [displayWith] in the .html) shows this
  // instead of the raw value - "22850" doesn't fit the tooltip's compact 22px bubble at this
  // panel's scale, and reads as effectively unreadable at that size anyway. Arrow field (not a
  // method) since it's passed by reference into MatSlider's own displayWith input, which calls it
  // unbound - it doesn't touch `this` so that's safe either way, but this avoids relying on it.
  protected readonly formatStarThumbLabel = (value: number): string =>
    value >= 1000 ? `${Math.round(value / 1000)}k` : `${value}`;

  // linkedSignal seeds from the per-view default input once and is then freely mutable locally -
  // the defaults (Discovery Feed = Newest/Desc, Hidden Gems = Score/Desc, Category drill-down =
  // Newest/Desc) never change after a view is routed to, so this behaves as "initialize once".
  protected readonly sort = linkedSignal(() => this.defaultSort());
  protected readonly direction = linkedSignal(() => this.defaultDirection());
  protected readonly selectedLanguages = linkedSignal<string[]>(() =>
    this.forcedCategory() ? [this.forcedCategory()!] : [],
  );

  protected readonly selectedLicenses = signal<string[]>([]);
  protected readonly selectedTopics = signal<string[]>([]);
  protected readonly minStars = signal<number | null>(null);
  protected readonly maxStars = signal<number | null>(null);
  protected readonly bookmarkedOnly = signal(false);
  protected readonly topicInput = signal('');

  // MatAutocompleteTrigger writes the selected option's value straight into the input's native DOM
  // value synchronously on selection (MatAutocompleteTrigger.selectionChange calls
  // `_assignOptionValue` before the (optionSelected) output we listen to below even fires) - our
  // own topicInput.set('') only updates the signal, and pushing that back out through
  // NgModel -> MatAutocompleteTrigger.writeValue() is itself deferred a microtask
  // (`Promise.resolve(null).then(...)`), which loses the race and leaves the just-selected topic's
  // text stuck in the field, blocking further typing. A direct reference to the native element lets
  // resetTopicInput() clear the real DOM value synchronously, immediately after our own handler
  // runs, so it reliably wins.
  private readonly topicInputEl = viewChild<ElementRef<HTMLInputElement>>('topicInputEl');

  protected readonly filteredTopicOptions = computed(() => {
    const typed = this.topicInput().toLowerCase();
    const selected = new Set(this.selectedTopics());
    return this.topicOptions().filter(
      (topic) => !selected.has(topic) && (typed === '' || topic.toLowerCase().includes(typed)),
    );
  });

  protected readonly starRangeActive = computed(
    () => this.minStars() !== null || this.maxStars() !== null,
  );

  // Header readout inside the Stars panel itself (Dashboard Design.dc.html §8: "Star range" label
  // + bold current value, e.g. "500 – 5,000") - distinct from the "N stars" chip in activeChips
  // below, which uses a "+" suffix at the ceiling; this one always shows the plain current bounds.
  protected readonly starRangeDisplay = computed(() => {
    const min = this.minStars() ?? 0;
    const max = this.maxStars() ?? STAR_RANGE_MAX;
    return `${min.toLocaleString()} – ${max.toLocaleString()}`;
  });

  protected readonly activeChips = computed<ActiveChip[]>(() => {
    const chips: ActiveChip[] = [];
    const category = this.forcedCategory();

    if (category) {
      chips.push({
        key: 'category',
        label: `category: ${category}`,
        removable: false,
        remove: noRemoveAction,
      });
    } else {
      for (const language of this.selectedLanguages()) {
        chips.push({
          key: `lang:${language}`,
          label: language,
          removable: true,
          remove: () => this.toggleLanguage(language),
        });
      }
    }

    if (this.starRangeActive()) {
      const min = this.minStars() ?? 0;
      const max = this.maxStars() ?? STAR_RANGE_MAX;
      chips.push({
        key: 'stars',
        label: `${min}–${max === STAR_RANGE_MAX ? `${STAR_RANGE_MAX}+` : max} stars`,
        removable: true,
        remove: () => this.resetStarRange(),
      });
    }

    for (const topic of this.selectedTopics()) {
      chips.push({
        key: `topic:${topic}`,
        label: `topic: ${topic}`,
        removable: true,
        remove: () => this.removeTopic(topic),
      });
    }

    for (const license of this.selectedLicenses()) {
      chips.push({
        key: `license:${license}`,
        label: license,
        removable: true,
        remove: () => this.toggleLicense(license),
      });
    }

    return chips;
  });

  protected readonly hasRemovableFilters = computed(() =>
    this.activeChips().some((c) => c.removable),
  );

  // Count backing the collapsed "Filters · N" trigger (dashboard-handoff.md §2) - one per active
  // chip, the same set already rendered inline in the active-filter chip row.
  protected readonly activeFilterCount = computed(() => this.activeChips().length);

  protected onLanguageSelectionChange(languages: string[]): void {
    this.selectedLanguages.set(languages);
    this.emitChange();
  }

  protected toggleLanguage(language: string): void {
    this.selectedLanguages.update((current) => current.filter((l) => l !== language));
    this.emitChange();
  }

  protected onLicenseSelectionChange(licenses: string[]): void {
    this.selectedLicenses.set(licenses);
    this.emitChange();
  }

  protected toggleLicense(license: string): void {
    this.selectedLicenses.update((current) => current.filter((l) => l !== license));
    this.emitChange();
  }

  protected onStarRangeChange(min: number | null, max: number | null): void {
    this.minStars.set(min !== null && min > 0 ? min : null);
    this.maxStars.set(max !== null && max < STAR_RANGE_MAX ? max : null);
    this.emitChange();
  }

  protected resetStarRange(): void {
    this.minStars.set(null);
    this.maxStars.set(null);
    this.emitChange();
  }

  protected onTopicSelected(event: MatAutocompleteSelectedEvent): void {
    this.addTopic(event.option.value as string);
    this.resetTopicInput();
  }

  protected onTopicTokenEnd(event: MatChipInputEvent): void {
    const value = (event.value || '').trim();
    if (value) {
      this.addTopic(value);
    }
    event.chipInput.clear();
    this.resetTopicInput();
  }

  private addTopic(topic: string): void {
    if (!this.selectedTopics().includes(topic)) {
      this.selectedTopics.update((current) => [...current, topic]);
      this.emitChange();
    }
  }

  // See topicInputEl's own comment for why the signal alone isn't enough here.
  private resetTopicInput(): void {
    this.topicInput.set('');
    const inputEl = this.topicInputEl()?.nativeElement;
    if (inputEl) {
      inputEl.value = '';
    }
  }

  protected removeTopic(topic: string): void {
    this.selectedTopics.update((current) => current.filter((t) => t !== topic));
    this.emitChange();
  }

  protected onBookmarkedOnlyChange(value: boolean): void {
    this.bookmarkedOnly.set(value);
    this.emitChange();
  }

  protected setSort(sort: RepositorySortField): void {
    this.sort.set(sort);
    this.emitChange();
  }

  protected toggleDirection(): void {
    this.direction.set(this.direction() === 'Asc' ? 'Desc' : 'Asc');
    this.emitChange();
  }

  // Also used by the repository-grid's empty-state "Clear all filters" recovery button (via
  // ViewChild from the owning feature component) - resetting bookmarkedOnly here too, not just the
  // FR-004 facets, since "0 results because Bookmarked-only is on and there are no bookmarks" is
  // exactly the recovery case that button exists for (Test Expectations: "bookmarkedOnly toggled
  // with zero bookmarks - empty state, not an error").
  clearAll(): void {
    if (!this.forcedCategory()) {
      this.selectedLanguages.set([]);
    }
    this.selectedLicenses.set([]);
    this.selectedTopics.set([]);
    this.minStars.set(null);
    this.maxStars.set(null);
    this.bookmarkedOnly.set(false);
    this.emitChange();
  }

  private emitChange(): void {
    this.stateChange.emit({
      language: [...this.selectedLanguages()],
      minStars: this.minStars() ?? undefined,
      maxStars: this.maxStars() ?? undefined,
      topic: [...this.selectedTopics()],
      license: [...this.selectedLicenses()],
      bookmarkedOnly: this.bookmarkedOnly(),
      sort: this.sort(),
      direction: this.direction(),
    });
  }
}
