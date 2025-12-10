using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Collections.Generic;

namespace Listenarr.Api.Tests
{
    public class SecurityRedactionTests
    {
        [Fact]
        public async Task NotificationService_LogsAreRedacted_WhenResponseContainsSensitiveValue()
        {
            // Arrange
            var secret = "MY_SUPER_SECRET_KEY";
            Environment.SetEnvironmentVariable("LISTENARR_API_KEY", secret);

            var webhookUrl = "https://discord.com/api/webhooks/test";
            var trigger = "book-added";
            var data = new { id = 1, title = "Redaction Test" };
            var enabledTriggers = new List<string> { trigger };

            string? capturedLog = null;

            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Error body includes secret: {secret}")
                });

            var httpClient = new HttpClient(mockHandler.Object);

            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService.Setup(x => x.GetStartupConfigAsync()).ReturnsAsync(new StartupConfig { UrlBase = "https://listenarr.example.com" });

            var mockLogger = new Mock<ILogger<NotificationService>>();
            mockLogger
                .Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var formatter = invocation.Arguments[4] as Func<object, Exception?, string>;
                    var state = invocation.Arguments[2];
                    if (formatter != null)
                        capturedLog = formatter.Invoke(state!, null);
                    else
                    {
                        // Try to extract the structured 'Body' value from the state object
                        try
                        {
                            if (state is System.Collections.IEnumerable enm)
                            {
                                foreach (var kv in enm)
                                {
                                    if (kv is System.Collections.Generic.KeyValuePair<string, object> pair && string.Equals(pair.Key, "Body", StringComparison.OrdinalIgnoreCase))
                                    {
                                        capturedLog = pair.Value?.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }

                        // Last-resort: fallback to ToString()
                        if (capturedLog == null) capturedLog = state?.ToString();
                    }
                }));

            var services = new ServiceCollection();
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();
            var provider = services.BuildServiceProvider();
            var payloadBuilder = provider.GetRequiredService<INotificationPayloadBuilder>();
            var service = new NotificationService(httpClient, mockLogger.Object, mockConfigService.Object, payloadBuilder, Mock.Of<IHttpContextAccessor>());

            // Act
            await service.SendNotificationAsync(trigger, data, webhookUrl, enabledTriggers);

            // Assert
            Assert.NotNull(capturedLog);
            Assert.Contains("<redacted>", capturedLog);
        }

        [Fact]
        public async Task DiscordController_LogsAreRedacted_WhenTokenValidationFails()
        {
            // Arrange
            var secret = "BOT_TOKEN_123";
            Environment.SetEnvironmentVariable("DISCORD_TOKEN", secret);

            var mockConfig = new Mock<IConfigurationService>();
            var appSettings = new ApplicationSettings
            {
                DiscordBotToken = secret,
                DiscordApplicationId = "app123",
                DiscordGuildId = null
            };
            mockConfig.Setup(x => x.GetApplicationSettingsAsync()).ReturnsAsync(appSettings);

            var handler = new Mock<HttpMessageHandler>();
            int calls = 0;
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
                {
                    // First call returns 200 OK (pre-check) - second call returns Unauthorized so
                    // the controller will proceed to the branch that logs the redacted body.
                    if (req.RequestUri!.ToString().Contains("users/@me"))
                    {
                        var current = System.Threading.Interlocked.Increment(ref calls);
                        if (current == 1)
                        {
                            return new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent("{}")
                            };
                        }

                        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            Content = new StringContent($"Invalid token: {secret}")
                        };
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                });

            var httpClient = new HttpClient(handler.Object);
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            string? capturedLog = null;
            var mockLogger = new Mock<ILogger<Controllers.DiscordController>>();
            mockLogger
                .Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var formatter = invocation.Arguments[4] as Func<object, Exception?, string>;
                    var state = invocation.Arguments[2];
                    if (formatter != null)
                        capturedLog = formatter.Invoke(state!, null);
                    else
                    {
                        try
                        {
                            if (state is System.Collections.IEnumerable enm)
                            {
                                foreach (var kv in enm)
                                {
                                    if (kv is System.Collections.Generic.KeyValuePair<string, object> pair && string.Equals(pair.Key, "Body", StringComparison.OrdinalIgnoreCase))
                                    {
                                        capturedLog = pair.Value?.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }

                        if (capturedLog == null) capturedLog = state?.ToString();
                    }
                }));

            var controller = new Controllers.DiscordController(mockConfig.Object, mockFactory.Object, mockLogger.Object, Mock.Of<IDiscordBotService>(), Mock.Of<IProcessRunner>());

            // Act
            var result = await controller.GetStatus();

            // Assert
            Assert.NotNull(capturedLog);
            Assert.Contains("<redacted>", capturedLog);
        }
    }
}
