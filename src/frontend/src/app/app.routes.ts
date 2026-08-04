import { Routes } from '@angular/router';

// FR-009's required view. The standalone Categories tab and its Category drill-down route were
// removed - browsing by category is done via Hidden Gems' existing Language filter instead
// (Category === Repository.PrimaryLanguage, so that filter already covers the same values; see the
// changelog entry for this removal). The standalone Trending tab was likewise removed and merged into
// Hidden Gems - each card there now shows its own trend growth directly
// (HiddenGemCardDto.trendGrowth, computed per repository rather than per language/category), so a
// separate Trending view is no longer needed to see the same information. The standalone Discovery Feed view was removed too - once Categories/Trending had
// already folded into Hidden Gems, Discovery Feed offered no meaningfully distinct browsing
// experience over it (see the changelog entry for this removal). F-012's dedicated Bookmarks view was
// removed last - Hidden Gems' existing "Bookmarked only" filter (FilterSortBar's
// showBookmarkedToggle) already surfaces the same repos, so a separate view added nothing (see the
// changelog entry for this removal). Hidden Gems is now the dashboard's only route besides the
// default redirect.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'hidden-gems' },
  {
    path: 'hidden-gems',
    loadComponent: () => import('./features/hidden-gems/hidden-gems').then((m) => m.HiddenGems),
  },
  { path: '**', redirectTo: 'hidden-gems' },
];
