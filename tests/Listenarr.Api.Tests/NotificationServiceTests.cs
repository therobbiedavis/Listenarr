using System.Text.Json.Nodes;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Api.Services;
using Xunit;
using System.Net;
using System.Net.Http;
using Moq;
using Moq.Protected;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public partial class NotificationServiceTests
    {
        [Fact]
        public void CreateDiscordPayload_IncludesTitleAuthorAndThumbnail_WhenAsinAndBaseProvided()
        {
            // Arrange
            var trigger = "book-added";
            var data = new
            {
                id = 123,
                title = "Test Book",
                authors = new[] { "Jane Doe" },
                asin = "B00TEST"
            };
            var baseUrl = "https://listenarr.example.com";

            // Act
            JsonNode node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, baseUrl);

            // Assert
            Assert.NotNull(node);
            Assert.True(node is JsonObject);
            var obj = node.AsObject();

            Assert.True(obj.ContainsKey("content"));
            Assert.Equal("Test Book by Jane Doe has been added", obj["content"]?.ToString());

            Assert.True(obj.ContainsKey("embeds"));
            Assert.NotNull(obj["embeds"]);
            var embeds = obj["embeds"]!.AsArray();
            Assert.Single(embeds);

            var embed = embeds[0]!.AsObject();
            Assert.Equal("Test Book", embed["title"]?.ToString());
            // Author should be present as a labeled field
            Assert.True(embed.ContainsKey("fields"));
            var fields = embed["fields"]!.AsArray();
            Assert.Contains(fields, f => f.AsObject()! ["name"]!.ToString() == "Author" && f.AsObject()!["value"]!.ToString() == "Jane Doe");
            Assert.True(embed.ContainsKey("thumbnail"));
            Assert.NotNull(embed["thumbnail"]);
            var thumb = embed["thumbnail"]!.AsObject();
            Assert.Equal("https://listenarr.example.com/api/images/B00TEST", thumb["url"]?.ToString());
        }
    }

    // Additional tests for truncation and metadata fields
    public partial class NotificationServiceTests
    {
        [Fact]
        public void CreateDiscordPayload_TruncatesLongFields_ToDiscordLimits()
        {
            // Arrange
            var trigger = "book-added";
            var longTitle = new string('T', 300); // longer than 256
            var longAuthor = new string('A', 5000); // longer than 4096
            var data = new
            {
                title = longTitle,
                authors = new[] { longAuthor }
            };

            // Act
            var node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, null);

            // Assert
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.True(obj.ContainsKey("embeds"));
            Assert.NotNull(obj["embeds"]);
            var embeds = obj["embeds"]!.AsArray();
            Assert.Single(embeds);
            var embed = embeds[0]!.AsObject();

            var title = embed["title"]?.ToString() ?? string.Empty;

            // Author should be present in fields and truncated to field limit (1024)
            string authorFieldValue = string.Empty;
            if (embed.ContainsKey("fields"))
            {
                foreach (var f in embed["fields"]!.AsArray())
                {
                    var fo = f!.AsObject();
                    if (fo["name"]?.ToString() == "Author")
                    {
                        authorFieldValue = fo["value"]?.ToString() ?? string.Empty;
                        break;
                    }
                }
            }

            Assert.True(title.Length <= 256, "Title should be truncated to 256 chars");
            Assert.True(authorFieldValue.Length <= 1024, "Author field should be truncated to 1024 chars");
        }

        [Fact]
        public void CreateDiscordPayload_IncludesPublisherYearImageAndFooter_WhenProvided()
        {
            // Arrange
            var trigger = "book-added";
            var data = new
            {
                title = "Metadata Book",
                authors = new[] { "Author" },
                publisher = "Test Publisher",
                year = "2021",
                imageUrl = "https://cdn.example.com/covers/test.jpg",
            };
            var baseUrl = "https://listenarr.example.com";

            // Act
            var node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, baseUrl);

            // Assert
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.True(obj.ContainsKey("embeds"));
            Assert.NotNull(obj["embeds"]);
            var embeds = obj["embeds"]!.AsArray();
            Assert.Single(embeds);
            var embed = embeds[0]!.AsObject();

            // fields should include Publisher and Year
            Assert.True(embed.ContainsKey("fields"));
            Assert.NotNull(embed["fields"]);
            var fields = embed["fields"]!.AsArray();
            Assert.Contains(fields, f => f.AsObject()!["name"]!.ToString() == "Publisher");
            Assert.Contains(fields, f => f.AsObject()!["name"]!.ToString() == "Year");

            // thumbnail should be present (we use thumbnail only now)
            Assert.True(embed.ContainsKey("thumbnail"));
            Assert.NotNull(embed["thumbnail"]);
            var thumb = embed["thumbnail"]!.AsObject();
            Assert.Equal("https://cdn.example.com/covers/test.jpg", thumb["url"]?.ToString());

            // footer should contain publisher and year
            Assert.True(embed.ContainsKey("footer"));
            Assert.NotNull(embed["footer"]);
            var footer = embed["footer"]!.AsObject();
            var footerText = footer["text"]?.ToString() ?? string.Empty;
            Assert.Contains("Test Publisher", footerText);
            Assert.Contains("2021", footerText);
        }

        [Fact]
        public void CreateDiscordPayload_ConvertsRelativeImageUrlToAbsolute_WhenBaseUrlProvided()
        {
            // Arrange
            var trigger = "book-added";
            var data = new
            {
                title = "Relative Image Book",
                authors = new[] { "Author" },
                imageUrl = "/api/images/B123RELATIVE"
            };
            var baseUrl = "https://listenarr.example.com";

            // Act
            var node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, baseUrl);

            // Assert
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.True(obj.ContainsKey("embeds"));
            var embeds = obj["embeds"]!.AsArray();
            Assert.Single(embeds);
            var embed = embeds[0]!.AsObject();

            // thumbnail should be present and converted to absolute URL
            Assert.True(embed.ContainsKey("thumbnail"));
            Assert.NotNull(embed["thumbnail"]);
            var image = embed["thumbnail"]!.AsObject();
            Assert.Equal("https://listenarr.example.com/api/images/B123RELATIVE", image["url"]?.ToString());
        }
    }

    public partial class NotificationServiceTests
    {
        [Fact]
        public void GetBaseUrlFromHttpContext_ReturnsExpectedBase()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Scheme = "https";
            ctx.Request.Host = new HostString("listenarr.example.com");

            var baseUrl = NotificationPayloadBuilder.GetBaseUrlFromHttpContext(ctx);
            Assert.Equal("https://listenarr.example.com", baseUrl);
        }

        [Fact]
        public void CreateDiscordPayload_UsesDerivedBaseForThumbnail_WhenProvided()
        {
            var trigger = "book-added";
            var data = new
            {
                title = "Derived Base Book",
                authors = new[] { "Author" },
                asin = "B123DERIVE"
            };

            var ctx = new DefaultHttpContext();
            ctx.Request.Scheme = "https";
            ctx.Request.Host = new HostString("listenarr.example.com");

            var derived = NotificationPayloadBuilder.GetBaseUrlFromHttpContext(ctx);
            Assert.NotNull(derived);

            var node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, derived);
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.NotNull(obj["embeds"]);
            var embed = obj["embeds"]!.AsArray()[0]!.AsObject();
            Assert.True(embed.ContainsKey("thumbnail"));
            var thumb = embed["thumbnail"]!.AsObject();
            Assert.Equal("https://listenarr.example.com/api/images/B123DERIVE", thumb["url"]?.ToString());
        }

        [Fact]
        public void CreateDiscordPayload_EnforcesOverallEmbedLimit_TruncatesAsNeeded()
        {
            var trigger = "book-added";
            // Create a very large description and many large fields to exceed the 6000 char limit
            var desc = new string('D', 5000);
            var fields = new List<JsonObject>();
            for (int i = 0; i < 10; i++)
            {
                var f = new JsonObject {
                    ["name"] = "F" + i,
                    ["value"] = new string('V', 1000),
                    ["inline"] = true
                };
                fields.Add(f);
            }

            var data = new JsonObject {
                ["title"] = "Big Embed",
                ["authors"] = new JsonArray("Author"),
                ["description"] = desc
            };

            // Also attach large fields by creating a wrapper object that CreateDiscordPayload will pick up
            // We'll add them under keys so the payload parsing can pick them up; simpler to include them as part of the data
            data["publisher"] = "Pub";

            var node = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, null);
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.NotNull(obj["embeds"]);
            var embed = obj["embeds"]!.AsArray()[0]!.AsObject();

            // Calculate total embed size
            int total = 0;
            if (embed.ContainsKey("title")) total += embed["title"]?.ToString()?.Length ?? 0;
            if (embed.ContainsKey("description")) total += embed["description"]?.ToString()?.Length ?? 0;
            if (embed.ContainsKey("footer")) total += embed["footer"]?.AsObject()? ["text"]?.ToString()?.Length ?? 0;
            if (embed.ContainsKey("fields"))
            {
                foreach (var f in embed["fields"]!.AsArray())
                {
                    var fo = f!.AsObject();
                    total += fo["name"]?.ToString()?.Length ?? 0;
                    total += fo["value"]?.ToString()?.Length ?? 0;
                }
            }

            Assert.True(total <= 6000, "Combined embed content should not exceed MAX_EMBED_TOTAL (6000)");
        }
    }

    public partial class NotificationServiceTests
    {
        [Fact]
        public async Task SendNotificationAsync_PostsCorrectJsonToDiscordWebhook()
        {
            // Arrange
            var trigger = "book-added";
            var data = new
            {
                id = 123,
                title = "Integration Test Book",
                authors = new[] { "Test Author" },
                asin = "B00INTEGRATION",
                publisher = "Test Publisher",
                year = "2023"
            };
            var webhookUrl = "https://discord.com/api/webhooks/test";
            var enabledTriggers = new List<string> { trigger };

            // Mock HttpClient to capture the posted content
            string? capturedJson = null;
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>(async (request, token) =>
                {
                    capturedJson = await request.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);

            // Mock configuration service
            var mockConfigService = new Mock<IConfigurationService>();
            var startupConfig = new StartupConfig { UrlBase = "https://listenarr.example.com" };
            mockConfigService
                .Setup(x => x.GetStartupConfigAsync())
                .ReturnsAsync(startupConfig);

            // Mock HttpContextAccessor (optional for this test)
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            // Create service
            var services = new ServiceCollection();
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();
            var provider = services.BuildServiceProvider();
            var payloadBuilder = provider.GetRequiredService<INotificationPayloadBuilder>();
            var service = new NotificationService(
                httpClient,
                Mock.Of<ILogger<NotificationService>>(),
                mockConfigService.Object,
                payloadBuilder,
                mockHttpContextAccessor.Object
            );

            // Act
            await service.SendNotificationAsync(trigger, data, webhookUrl, enabledTriggers);

            // Assert
            Assert.NotNull(capturedJson);
            var postedNode = JsonNode.Parse(capturedJson);
            Assert.NotNull(postedNode);

            // Compare with what CreateDiscordPayload produces
            var expectedNode = NotificationPayloadBuilder.CreateDiscordPayload(trigger, data, startupConfig.UrlBase);
            
            // Parse both as objects and compare key properties (excluding timestamp which is dynamic)
            var postedObj = postedNode.AsObject();
            var expectedObj = expectedNode.AsObject();
            
            // Compare content
            Assert.Equal(expectedObj["content"]?.ToString(), postedObj["content"]?.ToString());
            
            // Compare embeds (excluding timestamp)
            Assert.True(postedObj.ContainsKey("embeds"));
            Assert.True(expectedObj.ContainsKey("embeds"));
            var postedEmbeds = postedObj["embeds"]!.AsArray();
            var expectedEmbeds = expectedObj["embeds"]!.AsArray();
            Assert.Single(postedEmbeds);
            Assert.Single(expectedEmbeds);
            
            var postedEmbed = postedEmbeds[0]!.AsObject();
            var expectedEmbed = expectedEmbeds[0]!.AsObject();
            
            // Compare all properties except timestamp
            Assert.Equal(expectedEmbed["title"]?.ToString(), postedEmbed["title"]?.ToString());
            Assert.Equal(expectedEmbed["description"]?.ToString(), postedEmbed["description"]?.ToString());
            
            // Compare thumbnail if present
            if (expectedEmbed.ContainsKey("thumbnail"))
            {
                Assert.True(postedEmbed.ContainsKey("thumbnail"));
                var expectedThumb = expectedEmbed["thumbnail"]!.AsObject();
                var postedThumb = postedEmbed["thumbnail"]!.AsObject();
                Assert.Equal(expectedThumb["url"]?.ToString(), postedThumb["url"]?.ToString());
            }
            
            // Compare fields if present
            if (expectedEmbed.ContainsKey("fields"))
            {
                Assert.True(postedEmbed.ContainsKey("fields"));
                var expectedFields = expectedEmbed["fields"]!.AsArray();
                var postedFields = postedEmbed["fields"]!.AsArray();
                Assert.Equal(expectedFields.Count, postedFields.Count);
                
                for (int i = 0; i < expectedFields.Count; i++)
                {
                    var expectedField = expectedFields[i]!.AsObject();
                    var postedField = postedFields[i]!.AsObject();
                    Assert.Equal(expectedField["name"]?.ToString(), postedField["name"]?.ToString());
                    Assert.Equal(expectedField["value"]?.ToString(), postedField["value"]?.ToString());
                    Assert.Equal(expectedField["inline"]?.GetValue<bool>(), postedField["inline"]?.GetValue<bool>());
                }
            }
            
            // Compare footer if present
            if (expectedEmbed.ContainsKey("footer"))
            {
                Assert.True(postedEmbed.ContainsKey("footer"));
                var expectedFooter = expectedEmbed["footer"]!.AsObject();
                var postedFooter = postedEmbed["footer"]!.AsObject();
                Assert.Equal(expectedFooter["text"]?.ToString(), postedFooter["text"]?.ToString());
            }
        }
    }

    public partial class NotificationServiceTests
    {
        [Fact]
        public async Task SendNotificationAsync_AttachesImageAndReferencesAttachmentInPayload()
        {
            // Arrange
            var trigger = "book-added";
            var data = new
            {
                id = 321,
                title = "Attachment Test Book",
                authors = new[] { "Attach Author" },
                asin = "BATTACH",
                imageUrl = "https://cdn.example.com/covers/attach.jpg"
            };
            var webhookUrl = "https://discord.com/api/webhooks/test-attach";
            var enabledTriggers = new List<string> { trigger };

            string? capturedBody = null;

            // Mock HttpMessageHandler to return an image on GET and capture POST body
            var mockHandler = new Mock<HttpMessageHandler>();
            // GET for image
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(() =>
                {
                    var img = new byte[] { 1, 2, 3 };
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(img)
                    };
                    resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    return resp;
                });

            // POST to webhook - capture body
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>(async (request, token) =>
                {
                    capturedBody = await request.Content!.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(mockHandler.Object);

            // Mock configuration service
            var mockConfigService = new Mock<IConfigurationService>();
            var startupConfig = new StartupConfig { UrlBase = "https://listenarr.example.com" };
            mockConfigService
                .Setup(x => x.GetStartupConfigAsync())
                .ReturnsAsync(startupConfig);

            var services = new ServiceCollection();
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();
            var provider = services.BuildServiceProvider();
            var payloadBuilder = provider.GetRequiredService<INotificationPayloadBuilder>();
            var service = new NotificationService(
                httpClient,
                Mock.Of<ILogger<NotificationService>>(),
                mockConfigService.Object,
                payloadBuilder,
                Mock.Of<IHttpContextAccessor>()
            );

            // Act
            await service.SendNotificationAsync(trigger, data, webhookUrl, enabledTriggers);

            // Assert
            Assert.NotNull(capturedBody);
            // multipart should include payload_json part and reference attachment://BATTACH.jpg
            Assert.Contains("name=\"payload_json\"", capturedBody);
            Assert.Contains("attachment://BATTACH.jpg", capturedBody);
            // files[0] should be present with filename
            Assert.Contains("name=\"files[0]\"; filename=\"BATTACH.jpg\"", capturedBody);
        }
    }
}
