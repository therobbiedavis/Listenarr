using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Listenarr.Api.Tests
{
    /// <summary>
    /// Simple delegating handler useful in tests to return canned HttpResponseMessage objects.
    /// Used across multiple test files.
    /// </summary>
    internal class DelegatingHandlerMock : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

        public DelegatingHandlerMock(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                // Lightweight instrumentation for debugging in CI/logs
                Console.WriteLine($"DelegatingHandlerMock invoked for: {request.Method} {request.RequestUri}");
            }
            catch { }

            return _handlerFunc(request, cancellationToken);
        }
    }
}
