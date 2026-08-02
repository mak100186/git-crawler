// Mirrors GitCrawler.Api.Features.Trends.GetTrending's TrendDto/TrendingRepositoryDto. Note
// TrendingRepositoryDto is intentionally narrower than RepositoryCardDto (no url, forkCount,
// license, topics, firstDiscoveredAtUtc) - the backend's own comment says this matches exactly what
// the Trending view's contributing-repo row needs (dashboard-handoff.md §5 "03 Trending": name,
// language, stars, summary, bookmark toggle - no GitHub link, no discovered-date).
export interface TrendingRepositoryDto {
  id: number;
  owner: string;
  name: string;
  primaryLanguage: string | null;
  starCount: number;
  summaryContent: string | null;
  isBookmarked: boolean;
}

export interface TrendDto {
  id: number;
  category: string;
  periodStart: string;
  periodEnd: string;
  repositoryCount: number;
  averageScore: number;
  createdAtUtc: string;
  contributingRepositories: TrendingRepositoryDto[];
}
