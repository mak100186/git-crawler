using System.Text;

using Npgsql;

// F-017: bulk data seeder for the performance scratch database. Uses PostgreSQL COPY (via Npgsql's
// BeginTextImportAsync for TEXT format) for the two large tables (Repositories, Scores) - orders
// of magnitude faster than per-row EF SaveChanges at 100k/1M scale. Smaller tables (Summaries,
// Bookmarks) use batched INSERT since they're a fraction of the row count. Deterministic (fixed
// RNG seed) with realistic skewed distributions: power-law-ish language/star-count distribution,
// random topics and licenses, ~10 Score rows per repo to reach 1M+ total, summaries only for
// top-scored repos (mirroring GenerateSummariesCommandHandler's own MinimumScore filter), and
// sparse bookmarks.

internal static class PerfSeeder
{
    // Power-law-ish language distribution: a handful of languages dominate (mirroring real GitHub),
    // with a long tail of less-common ones. Weights don't need to sum to 1.0 - the picker
    // normalizes.
    private static readonly string[] Languages =
    [
        "JavaScript", "Python", "TypeScript", "Java", "Go",
        "Rust", "C#", "C++", "Ruby", "PHP",
        "Swift", "Kotlin", "Scala", "Elixir", "Haskell",
    ];

    private static readonly double[] LanguageWeights =
    [
        20, 18, 15, 12, 8,
        6, 5, 4, 3, 3,
        2, 1.5, 1, 0.8, 0.7,
    ];

    private static readonly string[] Licenses =
        ["MIT", "Apache-2.0", "GPL-3.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "MPL-2.0", "Unlicense"];

    private static readonly string[] TopicPool =
    [
        "web", "api", "cli", "database", "testing", "devops", "ml", "ai", "security", "performance",
        "graphql", "rest", "grpc", "microservices", "docker", "kubernetes", "react", "angular", "vue", "node",
        "python", "rust", "golang", "typescript", "javascript", "css", "html", "mobile", "ios", "android",
    ];

    public static async Task SeedAsync(string connectionString, int repositoryCount, int scoresPerRepo, int seed)
    {
        var rng = new Random(seed);

        // Pre-generate all repository data in memory. 100k repos × ~200 bytes each ≈ 20MB - fine.
        var repositories = GenerateRepositories(repositoryCount, rng);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // COPY Repositories (~100k rows).
        Console.Write("  COPY Repositories... ");
        await CopyRepositoriesAsync(conn, repositories);
        Console.WriteLine("done.");
        await ResetSequenceAsync(conn, "Repositories");

        // COPY Scores (~1M rows) in batches to bound memory. Each batch is a single COPY command.
        Console.Write("  COPY Scores (in batches)... ");
        const int scoreBatchSize = 10_000;
        var scoreId = 1;
        for (var batchStart = 0; batchStart < repositoryCount; batchStart += scoreBatchSize)
        {
            var batchEnd = Math.Min(batchStart + scoreBatchSize, repositoryCount);
            scoreId = await CopyScoresBatchAsync(conn, repositories, batchStart, batchEnd, scoresPerRepo, scoreId, rng);
        }
        Console.WriteLine("done.");
        await ResetSequenceAsync(conn, "Scores");

        // Summaries: generated for top-scored repos (latest TotalScore >= 40, mirroring
        // GenerateSummariesCommandHandler's own MinimumScore threshold). ~20-30% of repos.
        Console.Write("  INSERT Summaries... ");
        var summaryRepoIds = repositories
            .Where(r => r.LatestTotalScore >= 40.0)
            .Select(r => r.Id)
            .ToList();
        await InsertSummariesAsync(conn, summaryRepoIds);
        Console.WriteLine($"{summaryRepoIds.Count:N0} rows.");

        // Bookmarks: sparse, ~5% of repos. Use the same seed-deterministic RNG.
        Console.Write("  INSERT Bookmarks... ");
        var bookmarkRepoIds = repositories
            .Where(r => rng.NextDouble() < 0.05)
            .Select(r => r.Id)
            .ToList();
        await InsertBookmarksAsync(conn, bookmarkRepoIds);
        Console.WriteLine($"{bookmarkRepoIds.Count:N0} rows.");
    }

    private static List<SeedRepository> GenerateRepositories(int count, Random rng)
    {
        var repos = new List<SeedRepository>(count);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < count; i++)
        {
            var language = PickWeighted(Languages, LanguageWeights, rng);
            var stars = (int)Math.Floor(Math.Pow(rng.NextDouble(), 3) * 50000);
            var topicCount = rng.Next(0, Math.Min(TopicPool.Length, 10) + 1);
            var topics = TopicPool.OrderBy(_ => rng.Next()).Take(topicCount).ToArray();
            var license = rng.NextDouble() < 0.7 ? Licenses[rng.Next(Licenses.Length)] : null;
            var firstDiscovered = now.AddDays(-rng.Next(1, 1000));
            var latestScore = 5.0 + rng.NextDouble() * 95.0;

            repos.Add(new SeedRepository
            {
                Id = i + 1,
                GitHubId = 100000L + i,
                Owner = $"owner-{i % 5000:D5}",
                Name = $"repo-{i:D6}",
                Url = $"https://github.com/owner-{i % 5000:D5}/repo-{i:D6}",
                PrimaryLanguage = language,
                StarCount = stars,
                ForkCount = stars / 10 + rng.Next(0, 100),
                LicenseIdentifier = license,
                LicenseName = license is not null ? $"{license} License" : null,
                Topics = topics,
                FirstDiscoveredAtUtc = firstDiscovered,
                CreatedAtUtc = firstDiscovered,
                LatestTotalScore = latestScore,
                CommitCount = rng.Next(0, 5000),
                ContributorCount = rng.Next(1, 200),
            });
        }

        return repos;
    }

    private static async Task CopyRepositoriesAsync(NpgsqlConnection conn, List<SeedRepository> repositories)
    {
        const string copySql =
            "COPY \"Repositories\" (" +
            "\"Id\", \"GitHubId\", \"Owner\", \"Name\", \"Url\", \"PrimaryLanguage\", \"StarCount\", " +
            "\"ForkCount\", \"LicenseIdentifier\", \"LicenseName\", \"DefaultBranch\", \"CreatedAtUtc\", " +
            "\"PushedAtUtc\", \"LastCrawledAtUtc\", \"CommitCount\", \"ContributorCount\", " +
            "\"ContributorCountFetchedAtUtc\", \"Topics\", \"FirstDiscoveredAtUtc\") " +
            "FROM STDIN (FORMAT TEXT)";

        // Build the tab-separated content in memory. ~100k rows × ~300 bytes ≈ 30MB.
        var sb = new StringBuilder(repositories.Count * 300);
        foreach (var r in repositories)
        {
            sb.Append(r.Id).Append('\t')
                .Append(r.GitHubId).Append('\t')
                .Append(r.Owner).Append('\t')
                .Append(r.Name).Append('\t')
                .Append(r.Url).Append('\t')
                .Append(r.PrimaryLanguage ?? @"\N").Append('\t')
                .Append(r.StarCount).Append('\t')
                .Append(r.ForkCount).Append('\t')
                .Append(r.LicenseIdentifier ?? @"\N").Append('\t')
                .Append(r.LicenseName ?? @"\N").Append('\t')
                .Append("main").Append('\t')
                .Append(FormatTimestamp(r.CreatedAtUtc)).Append('\t')
                .Append(FormatTimestamp(r.CreatedAtUtc)).Append('\t')
                .Append(FormatTimestamp(r.CreatedAtUtc)).Append('\t')
                .Append(r.CommitCount).Append('\t')
                .Append(r.ContributorCount).Append('\t')
                .Append(FormatTimestamp(r.CreatedAtUtc)).Append('\t')
                .Append(FormatTopicsArray(r.Topics)).Append('\t')
                .Append(FormatTimestamp(r.FirstDiscoveredAtUtc))
                .Append('\n');
        }

        await using var writer = await conn.BeginTextImportAsync(copySql);
        await writer.WriteAsync(sb.ToString());
    }

    private static async Task<int> CopyScoresBatchAsync(
        NpgsqlConnection conn, List<SeedRepository> repos, int batchStart, int batchEnd,
        int scoresPerRepo, int nextScoreId, Random rng)
    {
        const string copySql =
            "COPY \"Scores\" (" +
            "\"Id\", \"RepositoryId\", \"HasLicense\", \"LicenseType\", \"CommitsPerWeek\", " +
            "\"ContributorCount\", \"ForkCount\", \"StarCount\", \"TotalScore\", \"ComputedAtUtc\") " +
            "FROM STDIN (FORMAT TEXT)";

        var sb = new StringBuilder((batchEnd - batchStart) * scoresPerRepo * 100);
        var scoreId = nextScoreId;

        for (var i = batchStart; i < batchEnd; i++)
        {
            var repo = repos[i];
            for (var s = 0; s < scoresPerRepo; s++)
            {
                var computedAt = repo.FirstDiscoveredAtUtc.AddDays(s * 7);
                var totalScore = repo.LatestTotalScore * (0.7 + rng.NextDouble() * 0.6);
                var hasLicense = repo.LicenseIdentifier is not null;
                var commitsPerWeek = 0.5 + rng.NextDouble() * 20.0;
                var contributors = rng.Next(1, 100);

                sb.Append(scoreId++).Append('\t')
                    .Append(repo.Id).Append('\t')
                    .Append(hasLicense ? "true" : "false").Append('\t')
                    .Append(repo.LicenseIdentifier ?? @"\N").Append('\t')
                    .Append(commitsPerWeek.ToString("F2")).Append('\t')
                    .Append(contributors).Append('\t')
                    .Append(repo.ForkCount).Append('\t')
                    .Append(repo.StarCount).Append('\t')
                    .Append(totalScore.ToString("F2")).Append('\t')
                    .Append(FormatTimestamp(computedAt))
                    .Append('\n');
            }
        }

        await using var writer = await conn.BeginTextImportAsync(copySql);
        await writer.WriteAsync(sb.ToString());
        return scoreId;
    }

    private static async Task InsertSummariesAsync(NpgsqlConnection conn, List<int> repoIds)
    {
        if (repoIds.Count == 0)
        {
            return;
        }

        const int batchSize = 1000;
        var now = FormatTimestamp(DateTimeOffset.UtcNow);
        for (var i = 0; i < repoIds.Count; i += batchSize)
        {
            var batch = repoIds.Skip(i).Take(batchSize).ToList();
            var sb = new StringBuilder(
                "INSERT INTO \"Summaries\" (\"RepositoryId\", \"ShortContent\", \"DetailedContent\", \"GeneratedAtUtc\") VALUES ");
            for (var j = 0; j < batch.Count; j++)
            {
                if (j > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"({batch[j]}, 'Short summary for repo {batch[j]}', 'Detailed summary content for repo {batch[j]}.', '{now}')");
            }

            await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertBookmarksAsync(NpgsqlConnection conn, List<int> repoIds)
    {
        if (repoIds.Count == 0)
        {
            return;
        }

        const int batchSize = 1000;
        var now = FormatTimestamp(DateTimeOffset.UtcNow);
        for (var i = 0; i < repoIds.Count; i += batchSize)
        {
            var batch = repoIds.Skip(i).Take(batchSize).ToList();
            var sb = new StringBuilder(
                "INSERT INTO \"Bookmarks\" (\"RepositoryId\", \"CreatedAtUtc\") VALUES ");
            for (var j = 0; j < batch.Count; j++)
            {
                if (j > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"({batch[j]}, '{now}')");
            }

            await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ResetSequenceAsync(NpgsqlConnection conn, string table)
    {
        await using var cmd = new NpgsqlCommand(
            $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), (SELECT MAX(\"Id\") FROM \"{table}\"));",
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string FormatTimestamp(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss+00");

    private static string FormatTopicsArray(string[] topics) =>
        topics.Length == 0 ? "{}" : "{" + string.Join(",", topics) + "}";

    private static string PickWeighted(string[] items, double[] weights, Random rng)
    {
        var total = weights.Sum();
        var pick = rng.NextDouble() * total;
        var cumulative = 0.0;
        for (var i = 0; i < items.Length; i++)
        {
            cumulative += weights[i];
            if (pick <= cumulative)
            {
                return items[i];
            }
        }

        return items[^1];
    }

    private sealed class SeedRepository
    {
        public int Id { get; init; }

        public long GitHubId { get; init; }

        public string Owner { get; init; } = "";

        public string Name { get; init; } = "";

        public string Url { get; init; } = "";

        public string? PrimaryLanguage { get; init; }

        public int StarCount { get; init; }

        public int ForkCount { get; init; }

        public string? LicenseIdentifier { get; init; }

        public string? LicenseName { get; init; }

        public string[] Topics { get; init; } = [];

        public DateTimeOffset FirstDiscoveredAtUtc { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public double LatestTotalScore { get; init; }

        public int CommitCount { get; init; }

        public int ContributorCount { get; init; }
    }
}