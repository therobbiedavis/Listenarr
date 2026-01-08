using System;
using System.Reflection;
using Listenarr.Api.Services;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class AudimetaServiceTests
    {
        private static bool InvokeSearchResultIndicatesPodcast(AudimetaSearchResult r)
        {
            var method = typeof(AudimetaService).GetMethod("SearchResultIndicatesPodcast", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) throw new InvalidOperationException("Could not find SearchResultIndicatesPodcast method");
            return (bool)method.Invoke(null, new object[] { r });
        }

        [Fact]
        public void ContentDeliveryBook_PreventsPodcastDetection()
        {
            var r = new AudimetaSearchResult
            {
                ContentType = "podcast",
                ContentDeliveryType = "SinglePartBook"
            };

            var isPodcast = InvokeSearchResultIndicatesPodcast(r);
            Assert.False(isPodcast);
        }

        [Fact]
        public void ContentTypePodcast_DetectedWhenNoBookDelivery()
        {
            var r = new AudimetaSearchResult
            {
                ContentType = "podcast",
                ContentDeliveryType = null
            };

            var isPodcast = InvokeSearchResultIndicatesPodcast(r);
            Assert.True(isPodcast);
        }
    }
}
