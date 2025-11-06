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
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using Listenarr.Api.Models;

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
                    // Don't log errors from the log sink to avoid infinite loops
                    // Just write to console as a fallback
                    Console.WriteLine($"[SignalRLogSink] Error broadcasting log: {ex.Message}");
                }
            });
        }
    }
}
