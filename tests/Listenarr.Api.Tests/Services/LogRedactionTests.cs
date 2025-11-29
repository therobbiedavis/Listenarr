using System;
using Listenarr.Api.Services;
using Xunit;

namespace Listenarr.Api.Tests.Services
{
    public class LogRedactionTests
    {
        [Fact]
        public void RedactText_ReplacesSensitiveEnvironmentValues()
        {
            var key = "LISTENARR_API_KEY";
            var secret = "supersecret-TEST-123";
            try
            {
                Environment.SetEnvironmentVariable(key, secret);

                var inputs = new[]
                {
                    $"This is a log line containing the secret: {secret}",
                    $"Multiple {secret} occurrences {secret}"
                };

                foreach (var input in inputs)
                {
                    var redacted = LogRedaction.RedactText(input, LogRedaction.GetSensitiveValuesFromEnvironment());
                    Assert.DoesNotContain(secret, redacted);
                    Assert.Contains("<redacted>", redacted);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }
}
