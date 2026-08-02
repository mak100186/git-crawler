# Graph Report - src  (2026-08-02)

## Corpus Check
- 47 files · ~21,750 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 860 nodes · 1223 edges · 55 communities (42 shown, 13 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Trends Aggregate Trends|Trends: Aggregate Trends]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Misc|Misc]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Trends Aggregate Trends|Trends: Aggregate Trends]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Summarization Generate Summaries|Summarization: Generate Summaries]]
- [[_COMMUNITY_Properties|Properties]]
- [[_COMMUNITY_Tests Data|Tests: Data]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Crawling Discover Repositories|Crawling: Discover Repositories]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Trends Aggregate Trends|Trends: Aggregate Trends]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Scoring Compute Scores|Scoring: Compute Scores]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Misc|Misc]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Diagnostics Ping|Diagnostics: Ping]]
- [[_COMMUNITY_Trends Aggregate Trends|Trends: Aggregate Trends]]
- [[_COMMUNITY_Tests Root|Tests: Root]]
- [[_COMMUNITY_Diagnostics Ping|Diagnostics: Ping]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Misc|Misc]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Angular Dashboard Scaffold|Angular Dashboard Scaffold]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Data|Data]]
- [[_COMMUNITY_Misc|Misc]]

## God Nodes (most connected - your core abstractions)
1. `DiscoverRepositoriesCommandHandlerTests` - 24 edges
2. `GenerateSummariesCommandHandlerTests` - 24 edges
3. `AggregateTrendsCommandHandlerTests` - 20 edges
4. `FakeStorageConnection` - 19 edges
5. `FakeStorageConnection` - 19 edges
6. `FakeStorageConnection` - 19 edges
7. `GitHubDiscoveryClient` - 16 edges
8. `ComputeScoresCommandHandlerTests` - 15 edges
9. `DiscoverRepositoriesCommandHandler` - 13 edges
10. `GitCrawlerDbContextTests` - 13 edges

## Surprising Connections (you probably didn't know these)
- `IGitHubDiscoveryClient` --implements--> `FakeGitHubDiscoveryClient`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `GitHubRateLimitException` --inherits--> `Exception`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `TimeProvider` --inherits--> `FakeTimeProvider`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommand.cs → tests/GitCrawler.Api.Tests/Features/Summarization/GenerateSummaries/Fakes.cs
- `IScoringContinuationLink` --implements--> `FakeScoringContinuationLink`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesJob.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `FakeTimeProvider` --inherits--> `TimeProvider`  [EXTRACTED]
  tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs → GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommand.cs

## Hyperedges (group relationships)
- **Angular Application Shell Composition** — index_app_root, apphtml_app_html, apphtml_mat_toolbar, apphtml_router_outlet [INFERRED 0.85]

## Communities (55 total, 13 thin omitted)

### Community 0 - "Crawling: Discover Repositories"
Cohesion: 0.05
Nodes (27): ComputeScoresJobTests, FakeJobStorage, NoOpJobCancellationToken, DiscoverRepositoriesJobTests, FakeJobStorage, NoOpJobCancellationToken, FakeJobStorage, GenerateSummariesJobTests (+19 more)

### Community 1 - "Summarization: Generate Summaries"
Cohesion: 0.05
Nodes (29): AggregateTrendsJob, HangfireTrendsContinuationLink, ITrendsContinuationLink, FakeSummarizationContinuationLink, FakeTrendsContinuationLink, FakeSummarizationContinuationLink, FakeTrendsContinuationLink, FakeHttpClientFactory (+21 more)

### Community 2 - "Crawling: Discover Repositories"
Cohesion: 0.08
Nodes (30): DeliveryOptions, FakeGitHubDiscoveryClient, FakeMessageBus, FakeRetryDelay, Envelope, IAsyncEnumerable, IDestinationEndpoint, IReadOnlyList (+22 more)

### Community 3 - "Angular Dashboard Scaffold"
Cohesion: 0.05
Nodes (45): build, lint, serve, test, builder, configurations, defaultConfiguration, options (+37 more)

### Community 4 - "Crawling: Discover Repositories"
Cohesion: 0.05
Nodes (28): DateTime, Dictionary, FakeStorageConnection, HashSet, IDictionary, IEnumerable, IFetchedJob, IWriteOnlyTransaction (+20 more)

### Community 5 - "Crawling: Discover Repositories"
Cohesion: 0.08
Nodes (23): FakeTimeProvider, FakeTimeProvider, DiscoverRepositoriesCommandHandler, FakeTimeProvider, DiscoverRepositoriesCommand, DiscoverRepositoriesResult, CancellationToken, DateTimeOffset (+15 more)

### Community 6 - "Angular Dashboard Scaffold"
Cohesion: 0.05
Nodes (36): dependencies, @angular/cdk, @angular/common, @angular/compiler, @angular/core, @angular/forms, @angular/material, @angular/platform-browser (+28 more)

### Community 7 - "Summarization: Generate Summaries"
Cohesion: 0.15
Nodes (16): FakeHttpClientFactory, FakeRepositorySummarizer, Func, GenerateSummariesCommandHandlerTests, GenerateSummariesCommandHandler, DateTimeOffset, Fact, FakeTimeProvider (+8 more)

### Community 8 - "Scoring: Compute Scores"
Cohesion: 0.06
Nodes (16): FakeStorageConnection, CancellationToken, DateTime, Dictionary, HashSet, IDictionary, IDisposable, IEnumerable (+8 more)

### Community 9 - "Summarization: Generate Summaries"
Cohesion: 0.06
Nodes (16): FakeStorageConnection, CancellationToken, DateTime, Dictionary, HashSet, IDictionary, IDisposable, IEnumerable (+8 more)

### Community 10 - "Crawling: Discover Repositories"
Cohesion: 0.19
Nodes (11): DiscoverRepositoriesCommandHandlerTests, DiscoverRepositoriesCommandHandler, FakeGitHubDiscoveryClient, FakeRetryDelay, IDisposable, DiscoveredRepository, Fact, FakeTimeProvider (+3 more)

### Community 11 - "Trends: Aggregate Trends"
Cohesion: 0.20
Nodes (12): AggregateTrendsCommandHandlerTests, AggregateTrendsCommandHandler, Summary, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, IConfiguration (+4 more)

### Community 12 - "Summarization: Generate Summaries"
Cohesion: 0.13
Nodes (17): FakeMessageBus, CancellationToken, DeliveryOptions, Envelope, HttpRequestMessage, HttpResponseMessage, IAsyncEnumerable, IDestinationEndpoint (+9 more)

### Community 13 - "Misc"
Cohesion: 0.09
Nodes (24): AllowedHosts, ConnectionStrings, Postgres, GitHub, DiscoveryLookbackDays, DiscoveryMinimumStars, DiscoveryPageSize, Token (+16 more)

### Community 14 - "Crawling: Discover Repositories"
Cohesion: 0.15
Nodes (14): GitHubDiscoveryClient, CancellationToken, DateTimeOffset, DiscoveryPage, HttpResponseMessage, int, string, Task (+6 more)

### Community 15 - "Scoring: Compute Scores"
Cohesion: 0.15
Nodes (15): FakeMessageBus, IMessageBus, CancellationToken, DeliveryOptions, Envelope, IAsyncEnumerable, IDestinationEndpoint, IReadOnlyList (+7 more)

### Community 16 - "Trends: Aggregate Trends"
Cohesion: 0.16
Nodes (14): FakeMessageBus, CancellationToken, DeliveryOptions, Envelope, IAsyncEnumerable, IDestinationEndpoint, IReadOnlyList, T (+6 more)

### Community 17 - "Scoring: Compute Scores"
Cohesion: 0.23
Nodes (9): ComputeScoresCommandHandlerTests, ComputeScoresCommandHandler, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, Repository, SqliteConnection (+1 more)

### Community 18 - "Summarization: Generate Summaries"
Cohesion: 0.11
Nodes (13): FakeRepositorySummarizer, IRepositorySummarizer, LmStudioRepositorySummarizer, CancellationToken, RepositorySummarizationContext, Task, CancellationToken, int (+5 more)

### Community 19 - "Data"
Cohesion: 0.12
Nodes (10): MigrationBuilder, MigrationBuilder, MigrationBuilder, Migration, GitCrawler.Api.Data.Migrations, InitialCreate, AddCrawlerRawSignalFields, GitCrawler.Api.Data.Migrations (+2 more)

### Community 20 - "Summarization: Generate Summaries"
Cohesion: 0.16
Nodes (9): ScoringWeights, double, GenerateSummariesCommandHandler, GenerateSummariesCommand, GenerateSummariesResult, DateTimeOffset, CancellationToken, int (+1 more)

### Community 21 - "Properties"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 22 - "Tests: Data"
Cohesion: 0.25
Nodes (6): GitCrawlerDbContextTests, Fact, GitCrawlerDbContext, Repository, SqliteConnection, Task

### Community 24 - "Crawling: Discover Repositories"
Cohesion: 0.19
Nodes (8): DiscoverRepositoriesJob, HangfireScoringContinuationLink, IScoringContinuationLink, FakeScoringContinuationLink, ComputeScoresJob, PerformContext, Task, ComputeScoresJob

### Community 25 - "Crawling: Discover Repositories"
Cohesion: 0.23
Nodes (10): GitHubContributorListUnavailableException, GitHubGraphQlRateLimitExceededException, GitHubRateLimitException, GitHubRestRateLimitExceededException, GitHubSecondaryRateLimitException, IGitHubDiscoveryClient, Exception, CancellationToken (+2 more)

### Community 26 - "Scoring: Compute Scores"
Cohesion: 0.20
Nodes (8): ComputeScoresCommandHandler, ComputeScoresCommand, ComputeScoresResult, CancellationToken, DateTimeOffset, Repository, Task, Score

### Community 27 - "Angular Dashboard Scaffold"
Cohesion: 0.18
Nodes (11): app.html (Root Component Template), mat-toolbar (Angular Material Toolbar), router-outlet (Angular Router), <app-root> Element, index.html (App Shell HTML), Material Icons Font, Roboto Google Font, Angular CLI (+3 more)

### Community 28 - "Angular Dashboard Scaffold"
Cohesion: 0.29
Nodes (5): App, appConfig, routes, compiled, fixture

### Community 29 - "Trends: Aggregate Trends"
Cohesion: 0.25
Nodes (6): AggregateTrendsCommandHandler, AggregateTrendsCommand, AggregateTrendsResult, CancellationToken, int, Task

### Community 30 - "Data"
Cohesion: 0.33
Nodes (4): ModelBuilder, GitCrawler.Api.Data.Migrations, GitCrawlerDbContextModelSnapshot, ModelSnapshot

### Community 31 - "Scoring: Compute Scores"
Cohesion: 0.40
Nodes (3): ComputeScoresJob, PerformContext, Task

### Community 32 - "Angular Dashboard Scaffold"
Cohesion: 0.40
Nodes (4): angular, { defineConfig }, eslint, tseslint

### Community 33 - "Misc"
Cohesion: 0.40
Nodes (4): Logging, LogLevel, Default, Microsoft.AspNetCore

### Community 34 - "Data"
Cohesion: 0.40
Nodes (3): GitCrawlerDbContext, DbContext, ModelBuilder

### Community 35 - "Data"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddScoreStarCountSignal, GitCrawler.Api.Data.Migrations

### Community 36 - "Data"
Cohesion: 0.40
Nodes (3): ModelBuilder, GitCrawler.Api.Data.Migrations, InitialCreate

### Community 37 - "Data"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddCrawlerRawSignalFields, GitCrawler.Api.Data.Migrations

### Community 38 - "Diagnostics: Ping"
Cohesion: 0.40
Nodes (3): PingQueryHandler, PingQuery, PingResult

### Community 39 - "Trends: Aggregate Trends"
Cohesion: 0.40
Nodes (3): AggregateTrendsJobTests, Fact, Task

## Knowledge Gaps
- **324 isolated node(s):** `Default`, `Microsoft.AspNetCore`, `Default`, `Microsoft.AspNetCore`, `Postgres` (+319 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `FakeStorageConnection` connect `Crawling: Discover Repositories` to `Crawling: Discover Repositories`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `IDisposable` connect `Crawling: Discover Repositories` to `Scoring: Compute Scores`, `Crawling: Discover Repositories`, `Tests: Data`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **Why does `DiscoverRepositoriesCommandHandlerTests` connect `Crawling: Discover Repositories` to `Crawling: Discover Repositories`?**
  _High betweenness centrality (0.052) - this node is a cross-community bridge._
- **What connects `Default`, `Microsoft.AspNetCore`, `Default` to the rest of the system?**
  _324 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Crawling: Discover Repositories` be split into smaller, more focused modules?**
  _Cohesion score 0.05442176870748299 - nodes in this community are weakly interconnected._
- **Should `Summarization: Generate Summaries` be split into smaller, more focused modules?**
  _Cohesion score 0.05053191489361702 - nodes in this community are weakly interconnected._
- **Should `Crawling: Discover Repositories` be split into smaller, more focused modules?**
  _Cohesion score 0.07729468599033816 - nodes in this community are weakly interconnected._