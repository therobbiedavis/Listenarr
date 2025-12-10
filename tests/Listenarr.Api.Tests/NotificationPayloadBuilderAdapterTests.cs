using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Moq.Protected;
using Xunit;
using Listenarr.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace Listenarr.Api.Tests
{
    public class NotificationPayloadBuilderAdapterTests
    {
        [Fact]
        public void CreateDiscordPayload_ReturnsExpectedContent()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();
            var provider = services.BuildServiceProvider();
            var adapter = provider.GetRequiredService<INotificationPayloadBuilder>();
            var data = new
            {
                title = "Adapter Title",
                authors = new[] { "Adapter Author" },
                asin = "BADAPTER"
            };
            var baseUrl = "https://listenarr.example.com";

            // Act
            var node = adapter.CreateDiscordPayload("book-added", data, baseUrl);

            // Assert
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.Equal("Adapter Title by Adapter Author has been added", obj["content"]?.ToString());
        }

        [Fact]
        public async Task CreateDiscordPayloadWithAttachmentAsync_DownloadsImageAndReturnsAttachment()
        {
            // Arrange
            var expectedBytes = new byte[] { 1, 2, 3, 4 };
            var handler = new Mock<HttpMessageHandler>();
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(expectedBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
                    }
                });

            var httpClient = new HttpClient(handler.Object);
            var services = new ServiceCollection();
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();
            var provider = services.BuildServiceProvider();
            var adapter = provider.GetRequiredService<INotificationPayloadBuilder>();

            var data = new
            {
                title = "Attachment Title",
                authors = new[] { "Attachment Author" },
                asin = "BATTACH",
                imageUrl = "https://cdn.example.com/covers/BATTACH.jpg"
            };

            // Act
            var (payload, attachment) = await adapter.CreateDiscordPayloadWithAttachmentAsync("book-added", data, "https://listenarr.example.com", httpClient, Mock.Of<IHttpContextAccessor>());

            // Assert
            Assert.NotNull(payload);
            Assert.NotNull(attachment);
            Assert.Equal(expectedBytes.Length, attachment.ImageData.Length);
            Assert.Equal("image/jpeg", attachment.ContentType);
            Assert.Contains("attachment://", payload["embeds"]!.AsArray()[0]!.AsObject()["thumbnail"]!.AsObject()["url"]!.ToString());
        }
    }
}
