namespace TradingSystem.Application.Abstractions;

public interface ITradingLogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, Exception? ex = null, params object[] args);
    void LogDebug(string message, params object[] args);
}