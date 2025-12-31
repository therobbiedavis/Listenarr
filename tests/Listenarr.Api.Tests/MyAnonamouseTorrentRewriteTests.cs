using System.Text;
using Xunit;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class MyAnonamouseTorrentRewriteTests
    {
        [Fact]
        public void ReplaceHostInTorrent_ReplacesIpWithHost()
        {
            // Construct minimal bencoded torrent with announce containing IP
            var announce = "http://47.39.239.96/announce";
            var bencoded = $"d8:announce{announce.Length}:{announce}4:infod6:lengthi123e4:name6:testee";
            var bytes = Encoding.ASCII.GetBytes(bencoded);

            var replaced = MyAnonamouseHelper.ReplaceHostInTorrent(bytes, "47.39.239.96", "www.myanonamouse.net");
            var s = Encoding.ASCII.GetString(replaced);
            Assert.Contains("www.myanonamouse.net", s);
            Assert.DoesNotContain("47.39.239.96", s);
        }
    }
}
