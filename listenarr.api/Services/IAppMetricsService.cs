using System;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Minimal metrics API for Listenarr to allow unit-tests to assert telemetry points.
    /// Implementations should be lightweight and thread-safe.
    /// </summary>
    public interface IAppMetricsService
    {
        void Increment(string metricName, double value = 1);
        void Gauge(string metricName, double value);
        void Timing(string metricName, TimeSpan duration);
    }
}
