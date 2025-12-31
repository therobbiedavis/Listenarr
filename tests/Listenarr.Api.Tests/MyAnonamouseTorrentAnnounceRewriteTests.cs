using System.Text;
using Xunit;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class MyAnonamouseTorrentAnnounceRewriteTests
    {
        [Fact]
        public void ReplaceHostInTorrent_ReplacesTrackerHostWithIndexHost()
        {
            // announce contains tracker host with passkey in path
            var announce = "https://t.myanonamouse.net/tracker.php/mGDjyetAEBGCaneLZNS9OHawTo1upcwU/announce";
            var bencoded = $"d8:announce{announce.Length}:{announce}4:infod6:lengthi123e4:name6:testee";
            var bytes = Encoding.ASCII.GetBytes(bencoded);

            var replaced = MyAnonamouseHelper.ReplaceHostInTorrent(bytes, "t.myanonamouse.net", "www.myanonamouse.net");
            var s = Encoding.ASCII.GetString(replaced);
            Assert.Contains("www.myanonamouse.net", s);
            Assert.Contains("/tracker.php/mGDjyetAEBGCaneLZNS9OHawTo1upcwU/announce", s); // passkey/path preserved
            Assert.DoesNotContain("t.myanonamouse.net", s);
        }
    }
}
