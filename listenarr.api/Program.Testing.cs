// This partial is compiled only for the test host. It applies small DI patches
// so the WebApplicationFactory used by integration tests has the same persistence
// registrations as the real app (including IDbContextFactory).
using Microsoft.AspNetCore.Builder;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Listenarr.Api.Extensions;

public partial class Program
{
    static partial void ApplyTestHostPatches(WebApplicationBuilder builder)
    {
        // Compute a test-local SQLite path (mirrors Program.cs behavior).
        var sqliteDbPath = Path.Combine(builder.Environment.ContentRootPath, "config", "database", "listenarr.db");
        var sqliteDbDir = Path.GetDirectoryName(sqliteDbPath);
        if (!string.IsNullOrEmpty(sqliteDbDir) && !Directory.Exists(sqliteDbDir))
        {
            Directory.CreateDirectory(sqliteDbDir);
        }

        // Ensure persistence registrations (DbContextFactory + compatibility DbContext)
        // are available to the test host so hosted services and other components can resolve them.
        builder.Services.AddListenarrPersistence(builder.Configuration, sqliteDbPath);

        // Disable Playwright installations during tests to avoid invoking external tools (npx/pwsh).
        // Inject a small in-memory configuration value that overrides the default.
        var inMemory = new Dictionary<string, string?>()
        {
            ["Playwright:Enabled"] = "false"
        };
        builder.Configuration.AddInMemoryCollection(inMemory);
    }
}
