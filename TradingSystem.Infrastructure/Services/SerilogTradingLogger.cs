using Microsoft.Extensions.Logging;
using TradingSystem.Application.Abstractions;

namespace TradingSystem.Infrastructure.Services;

public class SerilogTradingLogger : ITradingLogger
{
    private readonly ILogger<SerilogTradingLogger> _logger;

    public SerilogTradingLogger(ILogger<SerilogTradingLogger> logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogError(string message, Exception? ex = null, params object[] args)
    {
        if (ex != null)
            _logger.LogError(ex, message, args);
        else
            _logger.LogError(message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }
}