# Graph Report - src/backend  (2026-08-02)

## Corpus Check
- Corpus is ~29,604 words - fits in a single context window. You may not need a graph.

## Summary
- 1154 nodes · 1754 edges · 78 communities (57 shown, 21 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Repositories - GetDiscoveryFeed|Repositories - GetDiscoveryFeed]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Scoring - ComputeScores|Scoring - ComputeScores]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Repositories - GetHiddenGems|Repositories - GetHiddenGems]]
- [[_COMMUNITY_Bookmarks - ListBookmarks|Bookmarks - ListBookmarks]]
- [[_COMMUNITY_Trends - AggregateTrends|Trends - AggregateTrends]]
- [[_COMMUNITY_GitCrawler.Api|GitCrawler.Api]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Repositories|Repositories]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Trends - AggregateTrends|Trends - AggregateTrends]]
- [[_COMMUNITY_Scoring - ComputeScores|Scoring - ComputeScores]]
- [[_COMMUNITY_Trends - GetTrending|Trends - GetTrending]]
- [[_COMMUNITY_Scoring - ComputeScores|Scoring - ComputeScores]]
- [[_COMMUNITY_Bookmarks - CreateBookmark|Bookmarks - CreateBookmark]]
- [[_COMMUNITY_Categories - GetCategoryRepositories|Categories - GetCategoryRepositories]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Properties|Properties]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Summarization - GenerateSummaries|Summarization - GenerateSummaries]]
- [[_COMMUNITY_Scoring - ComputeScores|Scoring - ComputeScores]]
- [[_COMMUNITY_Categories - GetCategories|Categories - GetCategories]]
- [[_COMMUNITY_Crawling - DiscoverRepositories|Crawling - DiscoverRepositories]]
- [[_COMMUNITY_Trends - GetTrending|Trends - GetTrending]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Repositories - GetHiddenGems|Repositories - GetHiddenGems]]
- [[_COMMUNITY_Repositories - GetDiscoveryFeed|Repositories - GetDiscoveryFeed]]
- [[_COMMUNITY_Categories - GetCategoryRepositories|Categories - GetCategoryRepositories]]
- [[_COMMUNITY_Trends - AggregateTrends|Trends - AggregateTrends]]
- [[_COMMUNITY_Bookmarks - CreateBookmark|Bookmarks - CreateBookmark]]
- [[_COMMUNITY_Bookmarks - DeleteBookmark|Bookmarks - DeleteBookmark]]
- [[_COMMUNITY_Categories - GetCategories|Categories - GetCategories]]
- [[_COMMUNITY_Bookmarks - ListBookmarks|Bookmarks - ListBookmarks]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_GitCrawler.Api|GitCrawler.Api]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Scoring - ComputeScores|Scoring - ComputeScores]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Data - Migrations|Data - Migrations]]
- [[_COMMUNITY_Diagnostics - Ping|Diagnostics - Ping]]
- [[_COMMUNITY_Trends - AggregateTrends|Trends - AggregateTrends]]
- [[_COMMUNITY_GitCrawler.Api.Tests|GitCrawler.Api.Tests]]
- [[_COMMUNITY_Bookmarks - DeleteBookmark|Bookmarks - DeleteBookmark]]
- [[_COMMUNITY_Repositories - GetDiscoveryFeed|Repositories - GetDiscoveryFeed]]
- [[_COMMUNITY_Repositories - GetHiddenGems|Repositories - GetHiddenGems]]
- [[_COMMUNITY_Bookmarks - CreateBookmark|Bookmarks - CreateBookmark]]
- [[_COMMUNITY_Categories - GetCategories|Categories - GetCategories]]
- [[_COMMUNITY_Categories - GetCategoryRepositories|Categories - GetCategoryRepositories]]
- [[_COMMUNITY_Trends - GetTrending|Trends - GetTrending]]
- [[_COMMUNITY_Bookmarks - ListBookmarks|Bookmarks - ListBookmarks]]
- [[_COMMUNITY_Diagnostics - Ping|Diagnostics - Ping]]
- [[_COMMUNITY_App Startup|App Startup]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Data - Entities|Data - Entities]]
- [[_COMMUNITY_Data - Entities|Data - Entities]]
- [[_COMMUNITY_Data - Entities|Data - Entities]]
- [[_COMMUNITY_Data - Entities|Data - Entities]]
- [[_COMMUNITY_Data - Entities|Data - Entities]]
- [[_COMMUNITY_Frontend|Frontend]]
- [[_COMMUNITY_Community 77|Community 77]]

## God Nodes (most connected - your core abstractions)
1. `GetDiscoveryFeedQueryHandlerTests` - 31 edges
2. `DiscoverRepositoriesCommandHandlerTests` - 28 edges
3. `Task` - 25 edges
4. `GenerateSummariesCommandHandlerTests` - 24 edges
5. `AggregateTrendsCommandHandlerTests` - 20 edges
6. `FakeStorageConnection` - 19 edges
7. `FakeStorageConnection` - 19 edges
8. `FakeStorageConnection` - 19 edges
9. `Task` - 17 edges
10. `Fact` - 17 edges

## Surprising Connections (you probably didn't know these)
- `FakeTimeProvider` --inherits--> `TimeProvider`  [EXTRACTED]
  tests/GitCrawler.Api.Tests/Features/Bookmarks/CreateBookmark/CreateBookmarkCommandHandlerTests.cs → GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommand.cs
- `IRepositorySummarizer` --implements--> `FakeRepositorySummarizer`  [EXTRACTED]
  GitCrawler.Api/Features/Summarization/GenerateSummaries/IRepositorySummarizer.cs → tests/GitCrawler.Api.Tests/Features/Summarization/GenerateSummaries/Fakes.cs
- `IScoringContinuationLink` --implements--> `FakeScoringContinuationLink`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesJob.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `IGitHubDiscoveryClient` --implements--> `FakeGitHubDiscoveryClient`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `GitHubRateLimitException` --inherits--> `Exception`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs

## Hyperedges (group relationships)
- **Angular Application Shell Composition** — index_app_root, apphtml_app_html, apphtml_mat_toolbar, apphtml_router_outlet [INFERRED 0.85]

## Communities (78 total, 21 thin omitted)

### Community 0 - "Crawling - DiscoverRepositories"
Cohesion: 0.06
Nodes (40): DeliveryOptions, FakeGitHubDiscoveryClient, FakeMessageBus, FakeRetryDelay, GitHubContributorListUnavailableException, GitHubGraphQlRateLimitExceededException, GitHubRateLimitException, GitHubRestRateLimitExceededException (+32 more)

### Community 1 - "Crawling - DiscoverRepositories"
Cohesion: 0.05
Nodes (27): ComputeScoresJobTests, FakeJobStorage, NoOpJobCancellationToken, DiscoverRepositoriesJobTests, FakeJobStorage, NoOpJobCancellationToken, FakeJobStorage, GenerateSummariesJobTests (+19 more)

### Community 2 - "Frontend"
Cohesion: 0.05
Nodes (45): build, lint, serve, test, builder, configurations, defaultConfiguration, options (+37 more)

### Community 3 - "Crawling - DiscoverRepositories"
Cohesion: 0.08
Nodes (25): FakeTimeProvider, FakeTimeProvider, DiscoverRepositoriesCommandHandler, FakeTimeProvider, DiscoverRepositoriesCommand, DiscoverRepositoriesResult, FakeTimeProvider, CancellationToken (+17 more)

### Community 4 - "Summarization - GenerateSummaries"
Cohesion: 0.07
Nodes (26): FakeHttpClientFactory, FakeHttpMessageHandler, FakeMessageBus, FakeRepositorySummarizer, HttpClient, HttpMessageHandler, IHttpClientFactory, CancellationToken (+18 more)

### Community 5 - "Crawling - DiscoverRepositories"
Cohesion: 0.05
Nodes (24): DateTime, Dictionary, FakeStorageConnection, HashSet, IDictionary, IFetchedJob, IWriteOnlyTransaction, Job (+16 more)

### Community 6 - "Repositories - GetDiscoveryFeed"
Cohesion: 0.17
Nodes (12): GetDiscoveryFeedQueryHandlerTests, GetDiscoveryFeedQueryHandler, InlineData, DateTimeOffset, Fact, GitCrawlerDbContext, List, Repository (+4 more)

### Community 7 - "Frontend"
Cohesion: 0.05
Nodes (36): dependencies, @angular/cdk, @angular/common, @angular/compiler, @angular/core, @angular/forms, @angular/material, @angular/platform-browser (+28 more)

### Community 8 - "Summarization - GenerateSummaries"
Cohesion: 0.07
Nodes (21): AggregateTrendsJob, HangfireTrendsContinuationLink, ITrendsContinuationLink, FakeSummarizationContinuationLink, FakeTrendsContinuationLink, FakeSummarizationContinuationLink, FakeTrendsContinuationLink, FakeSummarizationContinuationLink (+13 more)

### Community 9 - "Summarization - GenerateSummaries"
Cohesion: 0.15
Nodes (16): FakeHttpClientFactory, FakeRepositorySummarizer, Func, GenerateSummariesCommandHandlerTests, GenerateSummariesCommandHandler, DateTimeOffset, Fact, FakeTimeProvider (+8 more)

### Community 10 - "Summarization - GenerateSummaries"
Cohesion: 0.06
Nodes (16): FakeStorageConnection, CancellationToken, DateTime, Dictionary, HashSet, IDictionary, IDisposable, IEnumerable (+8 more)

### Community 11 - "Scoring - ComputeScores"
Cohesion: 0.06
Nodes (16): FakeStorageConnection, CancellationToken, DateTime, Dictionary, HashSet, IDictionary, IDisposable, IEnumerable (+8 more)

### Community 12 - "Crawling - DiscoverRepositories"
Cohesion: 0.19
Nodes (11): DiscoverRepositoriesCommandHandlerTests, DiscoverRepositoriesCommandHandler, FakeGitHubDiscoveryClient, FakeRetryDelay, DiscoveredRepository, Fact, FakeTimeProvider, GitCrawlerDbContext (+3 more)

### Community 13 - "Repositories - GetHiddenGems"
Cohesion: 0.14
Nodes (16): ComputeScoresCommandHandler, ComputeScoresCommand, ComputeScoresResult, GetHiddenGemsQueryHandlerTests, GetHiddenGemsQueryHandler, CancellationToken, DateTimeOffset, Repository (+8 more)

### Community 14 - "Bookmarks - ListBookmarks"
Cohesion: 0.11
Nodes (16): DeleteBookmarkCommandHandlerTests, DeleteBookmarkCommandHandler, IDisposable, ListBookmarksQueryHandlerTests, ListBookmarksQueryHandler, Fact, GitCrawlerDbContext, Repository (+8 more)

### Community 15 - "Trends - AggregateTrends"
Cohesion: 0.20
Nodes (12): AggregateTrendsCommandHandlerTests, AggregateTrendsCommandHandler, Summary, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, IConfiguration (+4 more)

### Community 16 - "GitCrawler.Api"
Cohesion: 0.09
Nodes (24): AllowedHosts, ConnectionStrings, Postgres, GitHub, DiscoveryLookbackDays, DiscoveryMinimumStars, DiscoveryPageSize, Token (+16 more)

### Community 17 - "Crawling - DiscoverRepositories"
Cohesion: 0.15
Nodes (14): GitHubDiscoveryClient, CancellationToken, DateTimeOffset, DiscoveryPage, HttpResponseMessage, int, string, Task (+6 more)

### Community 18 - "Repositories"
Cohesion: 0.11
Nodes (16): int, IReadOnlyList, RankedRepository, Repository, RepositoryCardDto, SortDirection, IEnumerable, IOrderedEnumerable (+8 more)

### Community 19 - "Data - Migrations"
Cohesion: 0.09
Nodes (13): MigrationBuilder, MigrationBuilder, MigrationBuilder, Migration, MigrationBuilder, GitCrawler.Api.Data.Migrations, InitialCreate, AddCrawlerRawSignalFields (+5 more)

### Community 20 - "Trends - AggregateTrends"
Cohesion: 0.15
Nodes (15): FakeMessageBus, IMessageBus, CancellationToken, DeliveryOptions, Envelope, IAsyncEnumerable, IDestinationEndpoint, IReadOnlyList (+7 more)

### Community 21 - "Scoring - ComputeScores"
Cohesion: 0.16
Nodes (14): FakeMessageBus, CancellationToken, DeliveryOptions, Envelope, IAsyncEnumerable, IDestinationEndpoint, IReadOnlyList, T (+6 more)

### Community 22 - "Trends - GetTrending"
Cohesion: 0.25
Nodes (8): GetTrendingQueryHandlerTests, GetTrendingQueryHandler, DateTimeOffset, Fact, GitCrawlerDbContext, Repository, SqliteConnection, Task

### Community 23 - "Scoring - ComputeScores"
Cohesion: 0.23
Nodes (9): ComputeScoresCommandHandlerTests, ComputeScoresCommandHandler, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, Repository, SqliteConnection (+1 more)

### Community 24 - "Bookmarks - CreateBookmark"
Cohesion: 0.18
Nodes (10): CreateBookmarkCommandHandlerTests, FakeTimeProvider, CreateBookmarkCommandHandler, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, Repository (+2 more)

### Community 25 - "Categories - GetCategoryRepositories"
Cohesion: 0.25
Nodes (8): GetCategoryRepositoriesQueryHandlerTests, GetCategoryRepositoriesQueryHandler, Fact, GitCrawlerDbContext, List, Repository, SqliteConnection, Task

### Community 26 - "Summarization - GenerateSummaries"
Cohesion: 0.16
Nodes (9): ScoringWeights, double, GenerateSummariesCommandHandler, GenerateSummariesCommand, GenerateSummariesResult, DateTimeOffset, CancellationToken, int (+1 more)

### Community 27 - "Properties"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 28 - "Data"
Cohesion: 0.25
Nodes (6): GitCrawlerDbContextTests, Fact, GitCrawlerDbContext, Repository, SqliteConnection, Task

### Community 29 - "Summarization - GenerateSummaries"
Cohesion: 0.15
Nodes (10): IRepositorySummarizer, LmStudioRepositorySummarizer, CancellationToken, RepositorySummarizationContext, Task, CancellationToken, int, RepositorySummarizationContext (+2 more)

### Community 31 - "Categories - GetCategories"
Cohesion: 0.25
Nodes (7): GetCategoriesQueryHandlerTests, GetCategoriesQueryHandler, DateTimeOffset, Fact, GitCrawlerDbContext, SqliteConnection, Task

### Community 32 - "Crawling - DiscoverRepositories"
Cohesion: 0.19
Nodes (8): DiscoverRepositoriesJob, HangfireScoringContinuationLink, IScoringContinuationLink, FakeScoringContinuationLink, ComputeScoresJob, PerformContext, Task, ComputeScoresJob

### Community 33 - "Trends - GetTrending"
Cohesion: 0.17
Nodes (10): GetTrendingQueryHandler, GetTrendingQuery, GetTrendingResult, CancellationToken, IReadOnlyList, List, Repository, Task (+2 more)

### Community 34 - "Frontend"
Cohesion: 0.18
Nodes (11): app.html (Root Component Template), mat-toolbar (Angular Material Toolbar), router-outlet (Angular Router), <app-root> Element, index.html (App Shell HTML), Material Icons Font, Roboto Google Font, Angular CLI (+3 more)

### Community 35 - "Frontend"
Cohesion: 0.29
Nodes (5): App, appConfig, routes, compiled, fixture

### Community 36 - "Repositories - GetHiddenGems"
Cohesion: 0.22
Nodes (7): GetHiddenGemsQueryHandler, GetHiddenGemsQuery, CancellationToken, PagedResult, RankedRepository, Task, HiddenGemCardDto

### Community 37 - "Repositories - GetDiscoveryFeed"
Cohesion: 0.25
Nodes (6): GetDiscoveryFeedQueryHandler, GetDiscoveryFeedQuery, CancellationToken, PagedResult, RepositoryCardDto, Task

### Community 38 - "Categories - GetCategoryRepositories"
Cohesion: 0.25
Nodes (6): GetCategoryRepositoriesQueryHandler, GetCategoryRepositoriesQuery, CancellationToken, PagedResult, RepositoryCardDto, Task

### Community 39 - "Trends - AggregateTrends"
Cohesion: 0.25
Nodes (6): AggregateTrendsCommandHandler, AggregateTrendsCommand, AggregateTrendsResult, CancellationToken, int, Task

### Community 40 - "Bookmarks - CreateBookmark"
Cohesion: 0.29
Nodes (5): BookmarkDto, CreateBookmarkCommandHandler, CreateBookmarkCommand, CancellationToken, Task

### Community 41 - "Bookmarks - DeleteBookmark"
Cohesion: 0.29
Nodes (5): DeleteBookmarkCommandHandler, DeleteBookmarkCommand, DeleteBookmarkResult, CancellationToken, Task

### Community 42 - "Categories - GetCategories"
Cohesion: 0.29
Nodes (5): GetCategoriesQueryHandler, GetCategoriesQuery, GetCategoriesResult, CancellationToken, Task

### Community 43 - "Bookmarks - ListBookmarks"
Cohesion: 0.29
Nodes (5): CancellationToken, Task, ListBookmarksQueryHandler, ListBookmarksQuery, ListBookmarksResult

### Community 44 - "Data - Migrations"
Cohesion: 0.33
Nodes (4): ModelBuilder, GitCrawler.Api.Data.Migrations, GitCrawlerDbContextModelSnapshot, ModelSnapshot

### Community 45 - "Data"
Cohesion: 0.40
Nodes (3): GitCrawlerDbContext, DbContext, ModelBuilder

### Community 46 - "Frontend"
Cohesion: 0.40
Nodes (4): angular, { defineConfig }, eslint, tseslint

### Community 47 - "GitCrawler.Api"
Cohesion: 0.40
Nodes (4): Logging, LogLevel, Default, Microsoft.AspNetCore

### Community 48 - "Data - Migrations"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddScoreStarCountSignal, GitCrawler.Api.Data.Migrations

### Community 49 - "Scoring - ComputeScores"
Cohesion: 0.40
Nodes (3): ComputeScoresJob, PerformContext, Task

### Community 50 - "Data - Migrations"
Cohesion: 0.40
Nodes (3): ModelBuilder, GitCrawler.Api.Data.Migrations, InitialCreate

### Community 51 - "Data - Migrations"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddCrawlerRawSignalFields, GitCrawler.Api.Data.Migrations

### Community 52 - "Data - Migrations"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddRepositoryTopicsAndFirstDiscoveredAt, GitCrawler.Api.Data.Migrations

### Community 53 - "Diagnostics - Ping"
Cohesion: 0.40
Nodes (3): PingQueryHandler, PingQuery, PingResult

### Community 54 - "Trends - AggregateTrends"
Cohesion: 0.40
Nodes (3): AggregateTrendsJobTests, Fact, Task

## Knowledge Gaps
- **417 isolated node(s):** `Default`, `Microsoft.AspNetCore`, `Default`, `Microsoft.AspNetCore`, `Postgres` (+412 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **21 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FakeStorageConnection` connect `Crawling - DiscoverRepositories` to `Crawling - DiscoverRepositories`, `Repositories`?**
  _High betweenness centrality (0.144) - this node is a cross-community bridge._
- **Why does `DiscoverRepositoriesCommandHandlerTests` connect `Crawling - DiscoverRepositories` to `Crawling - DiscoverRepositories`, `Bookmarks - ListBookmarks`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Why does `IDisposable` connect `Crawling - DiscoverRepositories` to `Data`, `Crawling - DiscoverRepositories`, `Scoring - ComputeScores`?**
  _High betweenness centrality (0.118) - this node is a cross-community bridge._
- **What connects `Default`, `Microsoft.AspNetCore`, `Default` to the rest of the system?**
  _417 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Crawling - DiscoverRepositories` be split into smaller, more focused modules?**
  _Cohesion score 0.058445353594389245 - nodes in this community are weakly interconnected._
- **Should `Crawling - DiscoverRepositories` be split into smaller, more focused modules?**
  _Cohesion score 0.05442176870748299 - nodes in this community are weakly interconnected._
- **Should `Frontend` be split into smaller, more focused modules?**
  _Cohesion score 0.04541062801932367 - nodes in this community are weakly interconnected._