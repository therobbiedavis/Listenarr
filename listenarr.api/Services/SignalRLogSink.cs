/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Custom Serilog sink to broadcast log messages via SignalR in real-time
    /// </summary>
    public class SignalRLogSink : ILogEventSink
    {
        private IServiceProvider? _serviceProvider;

        public SignalRLogSink()
        {
        }

        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            // Don't try to broadcast if service provider not initialized yet
            if (_serviceProvider == null)
            {
                return;
            }

            // Fire and forget - broadcast in background
            _ = Task.Run(async () =>
            {
                try
                {
                    // Create a scope to resolve the hub context
                    using var scope = _serviceProvider.CreateScope();
                    var hubContext = scope.ServiceProvider.GetService<IHubContext<LogHub>>();

                    if (hubContext == null)
                    {
                        return;
                    }

                    // Map Serilog log level to our log level string
                    var level = logEvent.Level switch
                    {
                        LogEventLevel.Verbose => "Debug",
                        LogEventLevel.Debug => "Debug",
                        LogEventLevel.Information => "Info",
                        LogEventLevel.Warning => "Warning",
                        LogEventLevel.Error => "Error",
                        LogEventLevel.Fatal => "Error",
                        _ => "Info"
                    };

                    // Extract source from log event properties
                    string? source = null;
                    if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue))
                    {
                        source = sourceContextValue.ToString().Trim('"');
                        // Simplify namespace (e.g., "Listenarr.Api.Controllers.SystemController" -> "SystemController")
                        if (source.Contains('.'))
                        {
                            var parts = source.Split('.');
                            source = parts[^1]; // Last part
                        }
                    }

                    // Create log entry
                    var logEntry = new LogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = logEvent.Timestamp.UtcDateTime,
                        Level = level,
                        Message = logEvent.RenderMessage(),
                        Exception = logEvent.Exception?.ToString(),
                        Source = source ?? "Application"
                    };

                    // Broadcast to all connected clients
                    await hubContext.Clients.All.SendAsync("ReceiveLog", logEntry);
                }
                catch (Exception ex)
                {
                    // If the host is shutting down the service provider may be disposed.
                    // Swallow disposal-related exceptions (including when wrapped in AggregateException
                    // or InnerExceptions) to avoid noisy CI logs during shutdown.
                    static bool IsOrContainsObjectDisposed(Exception? e)
                    {
                        while (e != null)
                        {
                            if (e is ObjectDisposedException)
                                return true;

                            if (e is AggregateException agg)
                            {
                                foreach (var ie in agg.InnerExceptions)
                                {
                                    if (IsOrContainsObjectDisposed(ie))
                                        return true;
                                }

                                return false;
                            }

                            e = e.InnerException;
                        }

                        return false;
                    }

                    if (IsOrContainsObjectDisposed(ex))
                    {
                        return;
                    }

                    // Don't log errors from the log sink to avoid infinite loops.
                    // Use Trace so output is still available for diagnostics without feeding back into Serilog.
                    Trace.TraceError($"[SignalRLogSink] Error broadcasting log: {ex.Message}");
                }
            });
        }
    }
}

