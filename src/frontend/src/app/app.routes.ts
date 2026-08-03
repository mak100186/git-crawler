import { Routes } from '@angular/router';

// FR-009's four required views plus the Category drill-down, and F-012's dedicated Bookmarks view.
// Discovery Feed is the default/landing route (dashboard-ux-brief.md §3). Each route lazy-loads its
// standalone component so the initial bundle only pulls in what the landing route needs.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'discovery-feed' },
  {
    path: 'discovery-feed',
    loadComponent: () =>
      import('./features/discovery-feed/discovery-feed').then((m) => m.DiscoveryFeed),
  },
  {
    path: 'hidden-gems',
    loadComponent: () => import('./features/hidden-gems/hidden-gems').then((m) => m.HiddenGems),
  },
  {
    path: 'trending',
    loadComponent: () => import('./features/trending/trending').then((m) => m.Trending),
  },
  {
    path: 'categories',
    loadComponent: () => import('./features/categories/categories').then((m) => m.Categories),
  },
  {
    path: 'categories/:category',
    loadComponent: () =>
      import('./features/categories/category-detail/category-detail').then((m) => m.CategoryDetail),
  },
  {
    path: 'bookmarks',
    loadComponent: () => import('./features/bookmarks/bookmarks').then((m) => m.Bookmarks),
  },
  { path: '**', redirectTo: 'discovery-feed' },
];
