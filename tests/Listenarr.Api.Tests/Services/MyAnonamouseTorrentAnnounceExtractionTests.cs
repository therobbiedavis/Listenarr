using System;
using Xunit;
using Listenarr.Api.Services;
using System.Text;

namespace Listenarr.Api.Tests.Services
{
    public class MyAnonamouseTorrentAnnounceExtractionTests
    {
        [Fact]
        public void ExtractAnnounceUrls_FindsAnnounceAndAnnounceList()
        {
            // bencode: d8:announce26:http://tracker.example.com13:announce-listll12:http://a1.comel12:http://a2.comeee
            var sb = new StringBuilder();
            sb.Append("d");
            sb.Append("8:announce26:http://tracker.example.com");
            sb.Append("13:announce-listl");
            sb.Append("l13:http://a1.come");
            sb.Append("l13:http://a2.comee");
            sb.Append("e");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            var urls = MyAnonamouseHelper.ExtractAnnounceUrls(bytes);

            Assert.Contains("http://tracker.example.com", urls);
            Assert.Contains("http://a1.com", urls);
            Assert.Contains("http://a2.com", urls);
        }

        [Fact]
        public void ExtractAnnounceUrls_FallsBackToUdpAndHttpRegex()
        {
            var ascii = "d4:spam4:eggse" + "19:http://hidden-tracker.example.com" + "10:someudpudp://1.2.3.4:6969/announce";
            var bytes = Encoding.ASCII.GetBytes(ascii);

            var urls = MyAnonamouseHelper.ExtractAnnounceUrls(bytes);

            Assert.Contains(urls, s => s.IndexOf("http://hidden-tracker.example.com", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Contains(urls, s => s.IndexOf("udp://1.2.3.4:6969/announce", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
