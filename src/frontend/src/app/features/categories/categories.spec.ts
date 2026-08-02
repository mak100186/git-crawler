import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RouterLink, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { CategoryDto } from '../../core/models/category.model';
import { CategoryApiService } from '../../core/api/category-api.service';
import { Categories } from './categories';

const category: CategoryDto = {
  category: 'Rust',
  repositoryCount: 12,
  averageScore: 55,
  periodStart: '2026-07-01',
  periodEnd: '2026-07-07',
};

describe('Categories', () => {
  let fixture: ComponentFixture<Categories>;
  let getCategories: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getCategories = vi.fn();

    await TestBed.configureTestingModule({
      imports: [Categories],
      providers: [provideRouter([]), { provide: CategoryApiService, useValue: { getCategories } }],
    }).compileComponents();
  });

  it('shows the loading then populated tile grid', () => {
    getCategories.mockReturnValue(of({ categories: [category] }));

    fixture = TestBed.createComponent(Categories);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Rust');
    expect(fixture.nativeElement.textContent).toContain('12 repos');
  });

  it('renders each category tile as a real mat-card component, not a custom-styled anchor', () => {
    getCategories.mockReturnValue(of({ categories: [category] }));

    fixture = TestBed.createComponent(Categories);
    fixture.detectChanges();

    const tile = (fixture.nativeElement as HTMLElement).querySelector('.category-tile');
    expect(tile).toBeTruthy();
    expect(tile!.tagName.toLowerCase()).toBe('mat-card');
    // mat-card's own component class marker, confirming it's the actual Angular Material
    // component rather than CSS mimicking one.
    expect(tile!.classList.contains('mat-mdc-card')).toBe(true);
  });

  it('URL-encodes a category name with special characters in its drill-down routerLink', () => {
    getCategories.mockReturnValue(of({ categories: [{ ...category, category: 'C++ / Systems' }] }));

    fixture = TestBed.createComponent(Categories);
    fixture.detectChanges();

    const routerLinkDebugEl = fixture.debugElement.query(By.directive(RouterLink));
    const routerLink = routerLinkDebugEl.injector.get(RouterLink);
    expect(routerLink.urlTree?.toString()).toBe('/categories/C%2B%2B%20%2F%20Systems');
  });

  it('shows the empty state when there are zero categories', () => {
    getCategories.mockReturnValue(of({ categories: [] }));

    fixture = TestBed.createComponent(Categories);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No categories yet');
  });

  it('shows the error state when the request fails', () => {
    getCategories.mockReturnValue(throwError(() => new Error('network error')));

    fixture = TestBed.createComponent(Categories);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Something went wrong');
  });
});
