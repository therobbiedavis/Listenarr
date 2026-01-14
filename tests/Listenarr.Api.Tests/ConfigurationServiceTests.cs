using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Tests
{
    public class ConfigurationServiceTests
    {
        [Fact]
        public async Task SaveApplicationSettings_PersistsChanges()
        {
            // Arrange - build service provider with in-memory DB
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            var provider = services.BuildServiceProvider(validateScopes: true);

            // Resolve scoped DB context from a scope (AddDbContext registers it as scoped)
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ConfigurationService>>();

            var mockUser = new Mock<IUserService>();
            var mockStartup = new Mock<IStartupConfigService>();

            var svc = new ConfigurationService(db, logger, mockUser.Object, mockStartup.Object);

            // Act - save a modified settings object
            var settings = await svc.GetApplicationSettingsAsync();
            settings.OutputPath = "C:\\test-output";
            settings.ShowCompletedExternalDownloads = true;
            settings.EnabledNotificationTriggers = new System.Collections.Generic.List<string> { "book-added", "book-completed" };
            settings.Webhooks = new System.Collections.Generic.List<WebhookConfiguration>
            {
                new WebhookConfiguration { Name = "UnitWebhook", Url = "https://example.test/webhook", Type = "Zapier" }
            };

            await svc.SaveApplicationSettingsAsync(settings);

            // Read back
            var saved = await svc.GetApplicationSettingsAsync();

            // Assert
            Assert.Equal("C:\\test-output", saved.OutputPath);
            Assert.True(saved.ShowCompletedExternalDownloads);
            Assert.NotNull(saved.EnabledNotificationTriggers);
            Assert.Contains("book-completed", saved.EnabledNotificationTriggers);
            Assert.NotNull(saved.Webhooks);
            Assert.Single(saved.Webhooks!);
            Assert.Equal("UnitWebhook", saved.Webhooks![0].Name);

            // Now simulate a partial update where collections are omitted from the payload
            var partial = new ApplicationSettings { Id = 1, OutputPath = "C:\\partial-update" };
            await svc.SaveApplicationSettingsAsync(partial);

            var afterPartial = await svc.GetApplicationSettingsAsync();
            // Ensure previously saved collections were not cleared by the partial update
            Assert.Equal("C:\\partial-update", afterPartial.OutputPath);
            Assert.NotNull(afterPartial.EnabledNotificationTriggers);
            Assert.Contains("book-completed", afterPartial.EnabledNotificationTriggers);
            Assert.NotNull(afterPartial.Webhooks);
            Assert.Single(afterPartial.Webhooks!);
            Assert.Equal("UnitWebhook", afterPartial.Webhooks![0].Name);
        }

        [Fact]
        public async Task InMemoryDb_Persists_Webhooks_Directly()
        {
            // Arrange - build service provider with in-memory DB
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            var provider = services.BuildServiceProvider(validateScopes: true);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

            // Act - ensure settings exist, set webhooks directly, save, read back
            var settings = await db.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == 1);
            if (settings == null)
            {
                settings = new ApplicationSettings();
                db.ApplicationSettings.Add(settings);
                await db.SaveChangesAsync();
            }

            settings.Webhooks = new System.Collections.Generic.List<WebhookConfiguration>
            {
                new WebhookConfiguration { Name = "DirectWebhook", Url = "https://example.test/direct", Type = "Zapier" }
            };

            await db.SaveChangesAsync();

            var reloaded = await db.ApplicationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);

            // Assert
            Assert.NotNull(reloaded);
            Assert.NotNull(reloaded!.Webhooks);
            Assert.Single(reloaded.Webhooks!);
            Assert.Equal("DirectWebhook", reloaded.Webhooks![0].Name);
        }

    }
}