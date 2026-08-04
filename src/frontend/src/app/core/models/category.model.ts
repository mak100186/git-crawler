// Mirrors GitCrawler.Api.Features.Categories.GetCategories' CategoryDto. Per the backend's own
// comment, Category === Repository.PrimaryLanguage (F-010 D2), not a GitHub topic. Simplified to
// just this one field 2026-08-04 - FacetOptionsService (the sole consumer) only ever read
// `.category`; the backend used to also send repositoryCount/averageScore/periodStart/periodEnd
// (sourced from TrendAggregate) that nothing here ever used.
export interface CategoryDto {
  category: string;
}
