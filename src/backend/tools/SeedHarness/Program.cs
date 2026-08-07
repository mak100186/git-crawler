using System.Diagnostics;

using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

using Npgsql;

// F-017 performance seed harness: populates a SEPARATE scratch database (gitcrawler_perf by
// default, never the operator's real dev database from POSTGRES_DB) with ~100k repositories and
// ~1M Score rows sized toward NFR-004's target scale, then runs timed page-request queries and
// EXPLAIN ANALYZE captures to prove the dashboard's filter/sort paths stay fast at scale.
//
// Never runs automatically on app startup - this is an explicit `dotnet run` invocation via
// `make seed-perf` (or directly). The scratch database is dropped and recreated on every run,
// making this idempotent/cleanly re-creatable.

// Build the scratch-database connection string from the same env vars the main app uses
// (POSTGRES_USER/PASSWORD/PORT from .env.example), with the database name pinned to
// `gitcrawler_perf` instead of POSTGRES_DB - deliberately distinct so a misconfigured run can
// never touch the operator's real data. Override via command-line argument if needed.
var envUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "gitcrawler";
var envPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "";
var envPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
var envHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
var scratchDatabase = args.Length > 0 ? args[0] : "gitcrawler_perf";
var scratchConnectionString =
    $"Host={envHost};Port={envPort};Database={scratchDatabase};Username={envUser};Password={envPassword}";

// Admin connection string (postgres database) for CREATE/DROP DATABASE operations, since those
// can't run against the target database itself (can't drop a database you're connected to).
var adminConnectionString =
    $"Host={envHost};Port={envPort};Database=postgres;Username={envUser};Password={envPassword}";

const int repositoryCount = 100_000;
const int scoresPerRepo = 10;
const int randomSeed = 42;

Console.WriteLine($"F-017 Seed Harness");
Console.WriteLine($"  Target database : {scratchDatabase} on {envHost}:{envPort}");
Console.WriteLine($"  Repositories    : {repositoryCount:N0}");
Console.WriteLine($"  Scores          : {repositoryCount * scoresPerRepo:N0}");
Console.WriteLine();

// Step 1: Drop and recreate the scratch database.
Console.Write("Recreating scratch database... ");
await using (var adminConn = new NpgsqlConnection(adminConnectionString))
{
    await adminConn.OpenAsync();
    await using var terminateCmd = new NpgsqlCommand(
        $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{scratchDatabase}' AND pid <> pg_backend_pid();",
        adminConn);
    await terminateCmd.ExecuteNonQueryAsync();
    await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratchDatabase}\";", adminConn);
    await dropCmd.ExecuteNonQueryAsync();
    await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{scratchDatabase}\";", adminConn);
    await createCmd.ExecuteNonQueryAsync();
}
Console.WriteLine("done.");

// Step 2: Apply migrations to the scratch database (same migrations the main app runs on
// startup, including the F-017 indexes being verified here).
Console.Write("Applying EF Core migrations... ");
await using (var dbContext = new GitCrawlerDbContext(
    new DbContextOptionsBuilder<GitCrawlerDbContext>().UseNpgsql(scratchConnectionString).Options))
{
    await dbContext.Database.MigrateAsync();
}
Console.WriteLine("done.");

// Step 3: Seed data.
Console.WriteLine("Seeding data...");
var seedStopwatch = Stopwatch.StartNew();
await PerfSeeder.SeedAsync(scratchConnectionString, repositoryCount, scoresPerRepo, randomSeed);
seedStopwatch.Stop();
Console.WriteLine($"Seed completed in {seedStopwatch.Elapsed.TotalSeconds:F1}s.");

// Step 4: Run EXPLAIN ANALYZE and timed page-request queries to capture evidence.
Console.WriteLine();
Console.WriteLine("=== Performance Evidence ===");
await PerfVerifier.VerifyAsync(scratchConnectionString);

Console.WriteLine();
Console.WriteLine("Seed harness complete. The scratch database is left in place for manual EXPLAIN");
Console.WriteLine("ANALYZE exploration via psql if needed. Re-run to re-seed from scratch.");