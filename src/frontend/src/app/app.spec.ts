import { TestBed } from '@angular/core/testing';
import { App } from './app';

// Placeholder spec (F-003): proves the Angular test harness is wired and can compile a
// standalone component that imports Angular Material. Real view specs are later features.
describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the title in the toolbar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('mat-toolbar')?.textContent).toContain(
      'GitHub Hidden Gems Discovery Platform',
    );
  });
});
