// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace InvoiceDesk.Ui.Host;

// blazor reports render failures only through logging
public sealed class FileLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose()
    {
    }

    sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            FileLog.WriteMessage($"{logLevel} {category}: {formatter(state, exception)}", exception);
        }
    }
}
