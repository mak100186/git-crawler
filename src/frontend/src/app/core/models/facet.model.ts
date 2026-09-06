// Mirrors GitCrawler.Api.Features.Facets.GetFacetOptions' GetFacetOptionsResult. Languages are
// deliberately absent: they come from /api/categories, which already returns the catalog's distinct
// language set (Category === Repository.PrimaryLanguage server-side, F-010 D2).
export interface FacetOptionsDto {
  licenses: string[];
  topics: string[];
}
