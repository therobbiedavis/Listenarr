using System;

namespace Listenarr.Api.Services
{
    public class NoopAppMetricsService : IAppMetricsService
    {
        public void Increment(string metricName, double value = 1)
        {
            // no-op
        }

        public void Gauge(string metricName, double value)
        {
            // no-op
        }

        public void Timing(string metricName, TimeSpan duration)
        {
            // no-op
        }
    }
}
