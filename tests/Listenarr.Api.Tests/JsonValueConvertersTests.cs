// csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;
using Listenarr.Infrastructure.Persistence.Converters;

namespace Listenarr.Api.Tests
{
    public class JsonValueConvertersTests
    {
        [Fact]
        public void JsonValueConverter_SerializesNullToEmptyAndDeserializesToNewInstance()
        {
            var conv = new JsonValueConverter<List<string>>();
            var toProvider = conv.ConvertToProviderExpression.Compile();
            var fromProvider = conv.ConvertFromProviderExpression.Compile();

            string serialized = toProvider(null);
            Assert.Equal(string.Empty, serialized);

            var deserialized = fromProvider(serialized);
            Assert.NotNull(deserialized);
            Assert.Empty(deserialized);
        }

        [Fact]
        public void JsonValueConverter_RoundTripsDictionary()
        {
            var conv = new JsonValueConverter<Dictionary<string, int>>();
            var toProvider = conv.ConvertToProviderExpression.Compile();
            var fromProvider = conv.ConvertFromProviderExpression.Compile();

            var original = new Dictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2
            };

            var serialized = toProvider(original);
            Assert.False(string.IsNullOrWhiteSpace(serialized));
            Assert.Contains("\"one\"", serialized);
            Assert.Contains("\"two\"", serialized);

            var deserialized = fromProvider(serialized);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Count);
            Assert.Equal(1, deserialized["one"]);
            Assert.Equal(2, deserialized["two"]);
        }
    }
}
