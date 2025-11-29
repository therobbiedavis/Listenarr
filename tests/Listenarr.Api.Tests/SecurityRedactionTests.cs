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
                .Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var formatter = invocation.Arguments[4] as Func<object, Exception, string>;
                    var state = invocation.Arguments[2];
                    capturedLog = formatter?.Invoke(state, null);
                }));

            var service = new NotificationService(httpClient, mockLogger.Object, mockConfigService.Object, Mock.Of<IHttpContextAccessor>());

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
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("users/@me")), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent($"Invalid token: {secret}")
                });

            var httpClient = new HttpClient(handler.Object);
            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            string? capturedLog = null;
            var mockLogger = new Mock<ILogger<Controllers.DiscordController>>();
            mockLogger
                .Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var formatter = invocation.Arguments[4] as Func<object, Exception, string>;
                    var state = invocation.Arguments[2];
                    capturedLog = formatter?.Invoke(state, null);
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
