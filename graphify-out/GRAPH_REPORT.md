# Graph Report - src/  (2026-08-02)

## Corpus Check
- 0 files · ~14,667 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 518 nodes · 674 edges · 48 communities (35 shown, 13 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Crawler Test Fakes|Crawler Test Fakes]]
- [[_COMMUNITY_Angular Package Dependencies|Angular Package Dependencies]]
- [[_COMMUNITY_Hangfire Job Test Fakes|Hangfire Job Test Fakes]]
- [[_COMMUNITY_Angular CLI Build Config|Angular CLI Build Config]]
- [[_COMMUNITY_Crawler Handler Tests|Crawler Handler Tests]]
- [[_COMMUNITY_GitHub Discovery Client|GitHub Discovery Client]]
- [[_COMMUNITY_Backend App Settings|Backend App Settings]]
- [[_COMMUNITY_Scoring Handler Tests|Scoring Handler Tests]]
- [[_COMMUNITY_Crawler Command Handler|Crawler Command Handler]]
- [[_COMMUNITY_EF Core Migrations|EF Core Migrations]]
- [[_COMMUNITY_Crawler Job Tests|Crawler Job Tests]]
- [[_COMMUNITY_Hangfire Dashboard Auth Tests|Hangfire Dashboard Auth Tests]]
- [[_COMMUNITY_Angular CLI Schema Config|Angular CLI Schema Config]]
- [[_COMMUNITY_Backend Launch Settings|Backend Launch Settings]]
- [[_COMMUNITY_Data Store Context Tests|Data Store Context Tests]]
- [[_COMMUNITY_Scoring Weights Tests|Scoring Weights Tests]]
- [[_COMMUNITY_Scoring Command Slice|Scoring Command Slice]]
- [[_COMMUNITY_GitHub Rate-Limit Exceptions|GitHub Rate-Limit Exceptions]]
- [[_COMMUNITY_Angular App Shell & Material|Angular App Shell & Material]]
- [[_COMMUNITY_Angular Bootstrap & Routing|Angular Bootstrap & Routing]]
- [[_COMMUNITY_Crawler-to-Scoring Job Chain|Crawler-to-Scoring Job Chain]]
- [[_COMMUNITY_Retry Delay Seam|Retry Delay Seam]]
- [[_COMMUNITY_Fake Time Provider|Fake Time Provider]]
- [[_COMMUNITY_Scoring Weights Math|Scoring Weights Math]]
- [[_COMMUNITY_EF Migration Snapshot|EF Migration Snapshot]]
- [[_COMMUNITY_GitCrawler DbContext|GitCrawler DbContext]]
- [[_COMMUNITY_Hangfire Dashboard Auth Filter|Hangfire Dashboard Auth Filter]]
- [[_COMMUNITY_ESLint Config|ESLint Config]]
- [[_COMMUNITY_Dev App Settings|Dev App Settings]]
- [[_COMMUNITY_InitialCreate Migration Designer|InitialCreate Migration Designer]]
- [[_COMMUNITY_Star Count Migration Designer|Star Count Migration Designer]]
- [[_COMMUNITY_Crawler Fields Migration Designer|Crawler Fields Migration Designer]]
- [[_COMMUNITY_Ping Query Slice|Ping Query Slice]]
- [[_COMMUNITY_Compute Scores Job|Compute Scores Job]]
- [[_COMMUNITY_Backend Smoke Test|Backend Smoke Test]]
- [[_COMMUNITY_Ping Endpoint|Ping Endpoint]]
- [[_COMMUNITY_VS Code Launch Config|VS Code Launch Config]]
- [[_COMMUNITY_Program.cs Entry Point|Program.cs Entry Point]]
- [[_COMMUNITY_VS Code Tasks Config|VS Code Tasks Config]]
- [[_COMMUNITY_Bookmark Entity|Bookmark Entity]]
- [[_COMMUNITY_Repository Entity|Repository Entity]]
- [[_COMMUNITY_Score Entity|Score Entity]]
- [[_COMMUNITY_Summary Entity|Summary Entity]]
- [[_COMMUNITY_TrendAggregate Entity|TrendAggregate Entity]]
- [[_COMMUNITY_VS Code Extensions Config|VS Code Extensions Config]]

## God Nodes (most connected - your core abstractions)
1. `DiscoverRepositoriesCommandHandlerTests` - 21 edges
2. `FakeStorageConnection` - 19 edges
3. `ComputeScoresCommandHandlerTests` - 15 edges
4. `GitHubDiscoveryClient` - 14 edges
5. `GitCrawlerDbContextTests` - 13 edges
6. `ScoringWeightsTests` - 12 edges
7. `Task` - 11 edges
8. `Fact` - 11 edges
9. `Fact` - 11 edges
10. `DiscoverRepositoriesCommandHandler` - 10 edges

## Surprising Connections (you probably didn't know these)
- `FakeScoringContinuationLink` --implements--> `IScoringContinuationLink`  [EXTRACTED]
  tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs → GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesJob.cs
- `FakeGitHubDiscoveryClient` --implements--> `IGitHubDiscoveryClient`  [EXTRACTED]
  tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs → GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs
- `GitHubRateLimitException` --inherits--> `Exception`  [EXTRACTED]
  GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs → tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs
- `FakeRetryDelay` --implements--> `IRetryDelay`  [EXTRACTED]
  tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs → GitCrawler.Api/Features/Crawling/DiscoverRepositories/RetryDelay.cs
- `Dashboard (Frontend README)` --conceptually_related_to--> `index.html (App Shell HTML)`  [INFERRED]
  src/frontend/README.md → src/frontend/src/index.html

## Hyperedges (group relationships)
- **Angular Application Shell Composition** — index_app_root, apphtml_app_html, apphtml_mat_toolbar, apphtml_router_outlet [INFERRED 0.85]

## Communities (48 total, 13 thin omitted)

### Community 0 - "Crawler Test Fakes"
Cohesion: 0.09
Nodes (22): DeliveryOptions, FakeGitHubDiscoveryClient, FakeMessageBus, FakeRetryDelay, FakeScoringContinuationLink, Envelope, IAsyncEnumerable, IDestinationEndpoint (+14 more)

### Community 1 - "Angular Package Dependencies"
Cohesion: 0.05
Nodes (36): dependencies, @angular/cdk, @angular/common, @angular/compiler, @angular/core, @angular/forms, @angular/material, @angular/platform-browser (+28 more)

### Community 2 - "Hangfire Job Test Fakes"
Cohesion: 0.06
Nodes (16): DateTime, Dictionary, FakeStorageConnection, HashSet, IDictionary, IEnumerable, IFetchedJob, IWriteOnlyTransaction (+8 more)

### Community 3 - "Angular CLI Build Config"
Cohesion: 0.07
Nodes (30): build, lint, serve, test, builder, configurations, defaultConfiguration, options (+22 more)

### Community 4 - "Crawler Handler Tests"
Cohesion: 0.21
Nodes (10): DiscoverRepositoriesCommandHandlerTests, DiscoverRepositoriesCommandHandler, FakeGitHubDiscoveryClient, FakeRetryDelay, DiscoveredRepository, Fact, FakeTimeProvider, GitCrawlerDbContext (+2 more)

### Community 5 - "GitHub Discovery Client"
Cohesion: 0.15
Nodes (12): GitHubDiscoveryClient, CancellationToken, DateTimeOffset, DiscoveryPage, int, Task, TimeSpan, GraphQlDiscoveryResult (+4 more)

### Community 6 - "Backend App Settings"
Cohesion: 0.12
Nodes (18): AllowedHosts, ConnectionStrings, Postgres, GitHub, DiscoveryLookbackDays, DiscoveryMinimumStars, DiscoveryPageSize, Token (+10 more)

### Community 7 - "Scoring Handler Tests"
Cohesion: 0.23
Nodes (9): ComputeScoresCommandHandlerTests, ComputeScoresCommandHandler, DateTimeOffset, Fact, FakeTimeProvider, GitCrawlerDbContext, Repository, SqliteConnection (+1 more)

### Community 8 - "Crawler Command Handler"
Cohesion: 0.20
Nodes (11): DiscoverRepositoriesCommandHandler, DiscoverRepositoriesCommand, DiscoverRepositoriesResult, CancellationToken, DateTimeOffset, DiscoveredRepository, DiscoveryPage, int (+3 more)

### Community 9 - "EF Core Migrations"
Cohesion: 0.12
Nodes (10): MigrationBuilder, MigrationBuilder, MigrationBuilder, Migration, GitCrawler.Api.Data.Migrations, InitialCreate, AddCrawlerRawSignalFields, GitCrawler.Api.Data.Migrations (+2 more)

### Community 10 - "Crawler Job Tests"
Cohesion: 0.15
Nodes (10): DiscoverRepositoriesJobTests, FakeJobStorage, NoOpJobCancellationToken, IJobCancellationToken, JobStorage, Fact, IMonitoringApi, IStorageConnection (+2 more)

### Community 11 - "Hangfire Dashboard Auth Tests"
Cohesion: 0.22
Nodes (7): FakeStorage, HangfireDashboardAuthorizationFilterTests, HangfireDashboardAuthorizationFilter, DashboardContext, Fact, IMonitoringApi, IStorageConnection

### Community 12 - "Angular CLI Schema Config"
Cohesion: 0.12
Nodes (15): cli, packageManager, schematicCollections, prefix, projectType, root, schematics, sourceRoot (+7 more)

### Community 13 - "Backend Launch Settings"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "Data Store Context Tests"
Cohesion: 0.25
Nodes (6): GitCrawlerDbContextTests, Fact, GitCrawlerDbContext, Repository, SqliteConnection, Task

### Community 16 - "Scoring Command Slice"
Cohesion: 0.20
Nodes (8): ComputeScoresCommandHandler, ComputeScoresCommand, ComputeScoresResult, CancellationToken, DateTimeOffset, Repository, Task, Score

### Community 17 - "GitHub Rate-Limit Exceptions"
Cohesion: 0.27
Nodes (8): GitHubGraphQlRateLimitExceededException, GitHubRateLimitException, GitHubRestRateLimitExceededException, GitHubSecondaryRateLimitException, IGitHubDiscoveryClient, CancellationToken, DiscoveryPage, Task

### Community 18 - "Angular App Shell & Material"
Cohesion: 0.18
Nodes (11): app.html (Root Component Template), mat-toolbar (Angular Material Toolbar), router-outlet (Angular Router), <app-root> Element, index.html (App Shell HTML), Material Icons Font, Roboto Google Font, Angular CLI (+3 more)

### Community 19 - "Angular Bootstrap & Routing"
Cohesion: 0.29
Nodes (5): App, appConfig, routes, compiled, fixture

### Community 20 - "Crawler-to-Scoring Job Chain"
Cohesion: 0.27
Nodes (6): DiscoverRepositoriesJob, HangfireScoringContinuationLink, IScoringContinuationLink, ComputeScoresJob, PerformContext, Task

### Community 21 - "Retry Delay Seam"
Cohesion: 0.39
Nodes (5): IRetryDelay, TaskDelayRetryDelay, CancellationToken, Task, TimeSpan

### Community 22 - "Fake Time Provider"
Cohesion: 0.25
Nodes (5): FakeTimeProvider, FakeTimeProvider, DateTimeOffset, DateTimeOffset, TimeProvider

### Community 23 - "Scoring Weights Math"
Cohesion: 0.33
Nodes (3): ScoringWeights, double, DateTimeOffset

### Community 24 - "EF Migration Snapshot"
Cohesion: 0.33
Nodes (4): ModelBuilder, GitCrawler.Api.Data.Migrations, GitCrawlerDbContextModelSnapshot, ModelSnapshot

### Community 25 - "GitCrawler DbContext"
Cohesion: 0.40
Nodes (3): GitCrawlerDbContext, DbContext, ModelBuilder

### Community 26 - "Hangfire Dashboard Auth Filter"
Cohesion: 0.40
Nodes (3): HangfireDashboardAuthorizationFilter, DashboardContext, IDashboardAuthorizationFilter

### Community 27 - "ESLint Config"
Cohesion: 0.40
Nodes (4): angular, { defineConfig }, eslint, tseslint

### Community 28 - "Dev App Settings"
Cohesion: 0.40
Nodes (4): Logging, LogLevel, Default, Microsoft.AspNetCore

### Community 29 - "InitialCreate Migration Designer"
Cohesion: 0.40
Nodes (3): ModelBuilder, GitCrawler.Api.Data.Migrations, InitialCreate

### Community 30 - "Star Count Migration Designer"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddScoreStarCountSignal, GitCrawler.Api.Data.Migrations

### Community 31 - "Crawler Fields Migration Designer"
Cohesion: 0.40
Nodes (3): ModelBuilder, AddCrawlerRawSignalFields, GitCrawler.Api.Data.Migrations

### Community 32 - "Ping Query Slice"
Cohesion: 0.40
Nodes (3): PingQueryHandler, PingQuery, PingResult

## Knowledge Gaps
- **195 isolated node(s):** `Default`, `Microsoft.AspNetCore`, `Default`, `Microsoft.AspNetCore`, `Postgres` (+190 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `IDisposable` connect `Hangfire Job Test Fakes` to `Crawler Handler Tests`, `Data Store Context Tests`, `Scoring Handler Tests`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Why does `FakeStorageConnection` connect `Hangfire Job Test Fakes` to `Crawler Job Tests`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Why does `DiscoverRepositoriesCommandHandlerTests` connect `Crawler Handler Tests` to `Hangfire Job Test Fakes`?**
  _High betweenness centrality (0.020) - this node is a cross-community bridge._
- **What connects `Default`, `Microsoft.AspNetCore`, `Default` to the rest of the system?**
  _195 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Crawler Test Fakes` be split into smaller, more focused modules?**
  _Cohesion score 0.08502024291497975 - nodes in this community are weakly interconnected._
- **Should `Angular Package Dependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.05405405405405406 - nodes in this community are weakly interconnected._
- **Should `Hangfire Job Test Fakes` be split into smaller, more focused modules?**
  _Cohesion score 0.06439393939393939 - nodes in this community are weakly interconnected._