import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';

describe('App', () => {
  async function setUp() {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the brand in a mat-toolbar, with no nav (single-page app)', async () => {
    const fixture = await setUp();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('mat-toolbar')?.textContent).toContain('GitCrawler');
    expect(el.querySelector('.app-toolbar__nav')).toBeNull();
    expect(el.querySelector('.app-bottom-nav')).toBeNull();
  });

  it('renders the Search (v2) reserved placeholder', async () => {
    const fixture = await setUp();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Search (v2)');
  });
});
