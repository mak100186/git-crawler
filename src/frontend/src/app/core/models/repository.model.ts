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
  licenseName: string | null;
  topics: string[];
  firstDiscoveredAtUtc: string;
  summaryContent: string | null;
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

// trendGrowth mirrors the old standalone Trending view's growth chip text for this card's own
// category ("▲ +18% vs. last period", or "{avg} avg score" when there's no prior period to diff
// against) - null when the category has no TrendAggregate row yet. Added when the Trending tab was
// merged into Hidden Gems (see the changelog entry for that removal).
export interface HiddenGemCardDto extends RepositoryCardDto {
  scoreBreakdown: ScoreBreakdownDto;
  trendGrowth: string | null;
}

// Default page size mirrors RepositoryCardQuery.DefaultPageSize (backend) - the Task Packet
// requires the paginator to use the API's own default rather than a client-chosen value.
export const DEFAULT_PAGE_SIZE = 24;
