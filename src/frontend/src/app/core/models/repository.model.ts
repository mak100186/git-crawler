// Mirrors GitCrawler.Api.Features.Repositories' shared D4/D5 shapes (RepositoryFilterCriteria,
// PagedResult<T>, RepositoryCardDto) and GetHiddenGems' HiddenGemCardDto/ScoreBreakdownDto.
// Field names match the C# records verbatim (camelCase, per ASP.NET Core's default JSON
// serialization of record properties).

export type RepositorySortField = 'Newest' | 'Score' | 'Stars' | 'Commits';
export type SortDirection = 'Asc' | 'Desc';

export interface RepositoryFilterCriteria {
  language?: string[];
  minStars?: number;
  maxStars?: number;
  topic?: string[];
  license?: string[];
  bookmarkedOnly: boolean;
  sort: RepositorySortField;
  direction: SortDirection;
  page: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RepositoryCardDto {
  id: number;
  owner: string;
  name: string;
  url: string;
  primaryLanguage: string | null;
  starCount: number;
  forkCount: number;
  licenseIdentifier: string | null;
  topics: string[];
  firstDiscoveredAtUtc: string;
  // The card itself only ever renders summaryContent (short) - detailedSummaryContent exists on
  // this shared DTO purely so the detail dialog (opened with the same card item as its data, see
  // RepositoryGrid.openDetail) has it in hand without a second fetch. Both are null exactly when no
  // Summary row exists yet ("summary pending"); never independently null otherwise, since the
  // backend generates and persists both together in one create-once operation.
  summaryContent: string | null;
  detailedSummaryContent: string | null;
  isBookmarked: boolean;
}

export interface ScoreBreakdownDto {
  hasLicense: boolean;
  licenseType: string | null;
  licenseWeight: number;
  commitsPerWeek: number;
  commitsPerWeekWeight: number;
  contributorCount: number;
  contributorCountWeight: number;
  forkCount: number;
  forkCountWeight: number;
  starCount: number;
  starCountWeight: number;
  totalScore: number;
}

// trendGrowth is this repository's OWN score trend across re-crawls, not its language/category's
// (operator: "Trend is currently calculated per language. I want it to be calculated per
// repository") - "▲ +18% vs. last period" once a second Score exists from a later re-crawl, or
// "{score} current score" when only the first Score exists yet (no prior score to diff against).
// Effectively never null for a card that reached the dashboard at all, since GetHiddenGems only
// returns repos that already have at least one Score.
export interface HiddenGemCardDto extends RepositoryCardDto {
  scoreBreakdown: ScoreBreakdownDto;
  trendGrowth: string | null;
}

// Default page size mirrors RepositoryCardQuery.DefaultPageSize (backend) - the Task Packet
// requires the paginator to use the API's own default rather than a client-chosen value.
export const DEFAULT_PAGE_SIZE = 24;
