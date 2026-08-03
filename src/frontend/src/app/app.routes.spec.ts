import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from './app.routes';
import { HiddenGems } from './features/hidden-gems/hidden-gems';

// Routing (Task Packet Test Expectations, later narrowed when Discovery Feed was removed as a
// standalone view - see the changelog entry for that removal): default route lands on Hidden Gems.
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

  it('redirects the default route ("") to Hidden Gems', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/');

    expect(component).toBeInstanceOf(HiddenGems);
  });

  it('navigates /hidden-gems to its view', async () => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/hidden-gems');

    expect(component).toBeInstanceOf(HiddenGems);
  });
});
