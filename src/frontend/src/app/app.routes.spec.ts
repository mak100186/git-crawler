import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from './app.routes';
import { Categories } from './features/categories/categories';
import { CategoryDetail } from './features/categories/category-detail/category-detail';
import { DiscoveryFeed } from './features/discovery-feed/discovery-feed';
import { HiddenGems } from './features/hidden-gems/hidden-gems';
import { Trending } from './features/trending/trending';

// Routing (Task Packet Test Expectations): default route lands on Discovery Feed; all four nav
// entries resolve to their view; the Category drill-down route resolves with a category param that
// may contain characters needing URL encoding.
describe('app.routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimationsAsync(),
      ],
    });
  });

  it('redirects the default route ("") to Discovery Feed', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/');

    expect(component).toBeInstanceOf(DiscoveryFeed);
  });

  it('navigates each of the four nav entries to its view', async () => {
    const cases: [string, unknown][] = [
      ['/discovery-feed', DiscoveryFeed],
      ['/hidden-gems', HiddenGems],
      ['/trending', Trending],
      ['/categories', Categories],
    ];

    // RouterTestingHarness.create() throws "Only one harness should be created per test" if called
    // more than once in the same test - it's designed to be created once and reused across
    // subsequent navigateByUrl calls (its root RouterOutlet fixture is what gets reused).
    const harness = await RouterTestingHarness.create();
    for (const [path, componentType] of cases) {
      const component = await harness.navigateByUrl(path);
      expect(component).toBeInstanceOf(componentType as new (...args: unknown[]) => unknown);
    }
  });

  it('resolves the category drill-down route with a URL-encoded category segment', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/categories/C%2B%2B%20Systems');

    expect(component).toBeInstanceOf(CategoryDetail);
  });
});
