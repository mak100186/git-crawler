// Mirrors GitCrawler.Api.Features.Categories.GetCategories' CategoryDto. Per the backend's own
// comment, Category === Repository.PrimaryLanguage (F-010 D2), not a GitHub topic.
export interface CategoryDto {
  category: string;
  repositoryCount: number;
  averageScore: number;
  periodStart: string;
  periodEnd: string;
}
