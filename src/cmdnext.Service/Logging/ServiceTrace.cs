using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CmdNext.Service.Logging
{
    /// <summary>
    /// Small helper for tracing service-layer operations: logs entry at Debug,
    /// completion with elapsed milliseconds, and failures with the exception.
    /// </summary>
    public static class ServiceTrace
    {
        /// <summary>
        /// Begins a traced operation. Dispose the result to record the outcome.
        /// </summary>
        public static ITracedOperation Begin(
            ILogger logger,
            string operation,
            params object?[] context)
        {
            return new TracedOperation(logger, operation, context);
        }

        private sealed class TracedOperation : ITracedOperation
        {
            private readonly ILogger _logger;
            private readonly string _operation;
            private readonly object?[] _context;
            private readonly Stopwatch _stopwatch;
            private Exception? _exception;
            private bool _completed;

            public TracedOperation(ILogger logger, string operation, object?[] context)
            {
                _logger = logger;
                _operation = operation;
                _context = context ?? Array.Empty<object?>();
                _stopwatch = Stopwatch.StartNew();

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("→ {Operation} started {@Context}", _operation, _context);
                }
            }

            public void Failed(Exception exception)
            {
                _exception = exception;
            }

            public void Dispose()
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                _stopwatch.Stop();

                if (_exception != null)
                {
                    _logger.LogError(
                        _exception,
                        "✗ {Operation} failed after {ElapsedMs} ms {@Context}",
                        _operation,
                        _stopwatch.ElapsedMilliseconds,
                        _context);
                    return;
                }

                _logger.LogInformation(
                    "✓ {Operation} completed in {ElapsedMs} ms {@Context}",
                    _operation,
                    _stopwatch.ElapsedMilliseconds,
                    _context);
            }
        }
    }

    /// <summary>
    /// A service operation being traced. Call <see cref="Failed"/> before disposing
    /// to record it as a failure.
    /// </summary>
    public interface ITracedOperation : IDisposable
    {
        void Failed(Exception exception);
    }
}
