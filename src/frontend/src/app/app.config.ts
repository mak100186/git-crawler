import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // F-010's endpoints are called via HttpClient from RepositoryApiService/CategoryApiService/
    // BookmarkApiService (core/api).
    provideHttpClient(),
    // Several approved-design components rely on Angular Material's animation module (mat-select,
    // mat-expansion-panel, mat-slide-toggle, mat-snack-bar, mat-menu overlays) - the async provider
    // lazy-loads the animations engine instead of bundling it eagerly.
    provideAnimationsAsync(),
  ],
};
