/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Services;
using Listenarr.Api.Models;
using Listenarr.Api.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of integers for better frontend compatibility
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add SignalR for real-time updates
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        // Serialize enums as strings for SignalR messages too
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add in-memory cache for metadata prefetch / reuse
builder.Services.AddMemoryCache();

// Add HTTP client for external API calls with decompression support
builder.Services.AddHttpClient<IAudibleMetadataService, AudibleMetadataService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Add default HTTP client for other services
builder.Services.AddHttpClient();

// Register our custom services
builder.Services.AddDbContext<ListenArrDbContext>(options =>
    options.UseSqlite("Data Source=listenarr.db"));
builder.Services.AddScoped<IAudiobookRepository, AudiobookRepository>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IAmazonAsinService, AmazonAsinService>();
builder.Services.AddScoped<IDownloadService, DownloadService>();
// NOTE: IAudibleMetadataService is already registered as a typed HttpClient above.
// Removing duplicate scoped registration to avoid overriding the typed client configuration.
builder.Services.AddScoped<IOpenLibraryService, OpenLibraryService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();

// Register background service for daily cache cleanup
builder.Services.AddHostedService<ImageCacheCleanupService>();

// Register background service for download monitoring and real-time updates
builder.Services.AddHostedService<DownloadMonitorService>();

// Register background service for queue monitoring (external clients) and real-time updates
builder.Services.AddHostedService<QueueMonitorService>();

// Typed HttpClients with automatic decompression for scraping services
builder.Services.AddHttpClient<IAmazonSearchService, AmazonSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

builder.Services.AddHttpClient<IAudibleSearchService, AudibleSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Register Playwright page fetcher for JS-rendered pages and bot-workarounds
// Playwright-based page fetcher removed; AudibleMetadataService will create Playwright on-demand if needed.

// Add CORS support for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "http://localhost:3000",
                    "https://localhost:3000"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for SignalR
        });

    // Development fallback (use cautiously). This can help diagnose unexpected origin mismatches.
    options.AddPolicy("DevAll", p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod());
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
    
    // Apply any pending migrations
    context.Database.Migrate();
    
    // Apply SQLite PRAGMA settings after database is created
    SqlitePragmaInitializer.ApplyPragmas(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS (use restrictive policy by default, allow override via environment var)
var corsPolicy = Environment.GetEnvironmentVariable("LISTENARR_CORS_POLICY");
if (!string.IsNullOrWhiteSpace(corsPolicy) && corsPolicy == "DevAll")
{
    app.UseCors("DevAll");
}
else
{
    app.UseCors("AllowVueApp");
}

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub for real-time download updates
app.MapHub<DownloadHub>("/hubs/downloads");

app.Run();
