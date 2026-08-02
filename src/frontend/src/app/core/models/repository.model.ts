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

export interface HiddenGemCardDto extends RepositoryCardDto {
  scoreBreakdown: ScoreBreakdownDto;
}

// Default page size mirrors RepositoryCardQuery.DefaultPageSize (backend) - the Task Packet
// requires the paginator to use the API's own default rather than a client-chosen value.
export const DEFAULT_PAGE_SIZE = 24;
