# Graph Report - src  (2026-07-31)

## Corpus Check
- Corpus is ~1,559 words - fits in a single context window. You may not need a graph.

## Summary
- 168 nodes · 157 edges · 23 communities (17 shown, 6 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.9)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Angular Build Config|Angular Build Config]]
- [[_COMMUNITY_Angular Workspace Config|Angular Workspace Config]]
- [[_COMMUNITY_Launch Settings (.NET)|Launch Settings (.NET)]]
- [[_COMMUNITY_Frontend Package Manifest|Frontend Package Manifest]]
- [[_COMMUNITY_Frontend Dev Dependencies|Frontend Dev Dependencies]]
- [[_COMMUNITY_Backend App Settings|Backend App Settings]]
- [[_COMMUNITY_Frontend Runtime Dependencies|Frontend Runtime Dependencies]]
- [[_COMMUNITY_Angular App Shell (HTMLMaterial)|Angular App Shell (HTML/Material)]]
- [[_COMMUNITY_Root Component + Routing|Root Component + Routing]]
- [[_COMMUNITY_Angular Build Options|Angular Build Options]]
- [[_COMMUNITY_Backend Dev Logging Config|Backend Dev Logging Config]]
- [[_COMMUNITY_ESLint Config|ESLint Config]]
- [[_COMMUNITY_Ping Query Vertical Slice|Ping Query Vertical Slice]]
- [[_COMMUNITY_Angular Lint Config|Angular Lint Config]]
- [[_COMMUNITY_Backend Smoke Test|Backend Smoke Test]]
- [[_COMMUNITY_Ping Endpoint Vertical Slice|Ping Endpoint Vertical Slice]]
- [[_COMMUNITY_VS Code Launch Config|VS Code Launch Config]]
- [[_COMMUNITY_VS Code Tasks Config|VS Code Tasks Config]]
- [[_COMMUNITY_Backend Host Entry Point|Backend Host Entry Point]]
- [[_COMMUNITY_VS Code Extension Recommendations|VS Code Extension Recommendations]]

## God Nodes (most connected - your core abstractions)
1. `dashboard` - 7 edges
2. `scripts` - 7 edges
3. `http` - 6 edges
4. `https` - 6 edges
5. `options` - 6 edges
6. `development` - 6 edges
7. `architect` - 5 edges
8. `build` - 5 edges
9. `production` - 5 edges
10. `serve` - 4 edges

## Surprising Connections (you probably didn't know these)
- `Dashboard (Frontend README)` --conceptually_related_to--> `index.html (App Shell HTML)`  [INFERRED]
  src/frontend/README.md → src/frontend/src/index.html
- `<app-root> Element` --implements--> `app.html (Root Component Template)`  [INFERRED]
  src/frontend/src/index.html → src/frontend/src/app/app.html

## Hyperedges (group relationships)
- **Angular Application Shell Composition** — index_app_root, apphtml_app_html, apphtml_mat_toolbar, apphtml_router_outlet [INFERRED 0.85]

## Communities (23 total, 6 thin omitted)

### Community 0 - "Angular Build Config"
Cohesion: 0.11
Nodes (20): build, serve, test, builder, configurations, defaultConfiguration, development, production (+12 more)

### Community 1 - "Angular Workspace Config"
Cohesion: 0.12
Nodes (15): cli, packageManager, schematicCollections, prefix, projectType, root, schematics, sourceRoot (+7 more)

### Community 2 - "Launch Settings (.NET)"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 3 - "Frontend Package Manifest"
Cohesion: 0.14
Nodes (13): engines, node, name, packageManager, private, scripts, build, lint (+5 more)

### Community 4 - "Frontend Dev Dependencies"
Cohesion: 0.17
Nodes (12): devDependencies, @angular/build, @angular/cli, @angular/compiler-cli, angular-eslint, eslint, @eslint/js, jsdom (+4 more)

### Community 5 - "Backend App Settings"
Cohesion: 0.17
Nodes (11): AllowedHosts, ConnectionStrings, Postgres, GitHub, Token, LmStudio, BaseUrl, Logging (+3 more)

### Community 6 - "Frontend Runtime Dependencies"
Cohesion: 0.18
Nodes (11): dependencies, @angular/cdk, @angular/common, @angular/compiler, @angular/core, @angular/forms, @angular/material, @angular/platform-browser (+3 more)

### Community 7 - "Angular App Shell (HTML/Material)"
Cohesion: 0.18
Nodes (11): app.html (Root Component Template), mat-toolbar (Angular Material Toolbar), router-outlet (Angular Router), <app-root> Element, index.html (App Shell HTML), Material Icons Font, Roboto Google Font, Angular CLI (+3 more)

### Community 8 - "Root Component + Routing"
Cohesion: 0.29
Nodes (5): App, appConfig, routes, compiled, fixture

### Community 9 - "Angular Build Options"
Cohesion: 0.33
Nodes (6): options, assets, browser, inlineStyleLanguage, styles, tsConfig

### Community 10 - "Backend Dev Logging Config"
Cohesion: 0.40
Nodes (4): Logging, LogLevel, Default, Microsoft.AspNetCore

### Community 11 - "ESLint Config"
Cohesion: 0.40
Nodes (4): angular, { defineConfig }, eslint, tseslint

### Community 12 - "Ping Query Vertical Slice"
Cohesion: 0.40
Nodes (3): PingQueryHandler, PingQuery, PingResult

### Community 13 - "Angular Lint Config"
Cohesion: 0.50
Nodes (4): lint, builder, options, lintFilePatterns

## Knowledge Gaps
- **101 isolated node(s):** `Default`, `Microsoft.AspNetCore`, `Default`, `Microsoft.AspNetCore`, `AllowedHosts` (+96 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `architect` connect `Angular Build Config` to `Angular Workspace Config`, `Angular Lint Config`?**
  _High betweenness centrality (0.046) - this node is a cross-community bridge._
- **Why does `dashboard` connect `Angular Workspace Config` to `Angular Build Config`?**
  _High betweenness centrality (0.038) - this node is a cross-community bridge._
- **Why does `build` connect `Angular Build Config` to `Angular Build Options`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **What connects `Default`, `Microsoft.AspNetCore`, `Default` to the rest of the system?**
  _101 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Angular Build Config` be split into smaller, more focused modules?**
  _Cohesion score 0.11052631578947368 - nodes in this community are weakly interconnected._
- **Should `Angular Workspace Config` be split into smaller, more focused modules?**
  _Cohesion score 0.125 - nodes in this community are weakly interconnected._
- **Should `Launch Settings (.NET)` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._