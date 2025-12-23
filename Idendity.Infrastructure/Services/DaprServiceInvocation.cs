using Dapr.Client;
using Microsoft.Extensions.Logging;

namespace Idendity.Infrastructure.Services;

/// <summary>
/// Helper service for Dapr service-to-service invocation
/// </summary>
public interface IDaprServiceInvocation
{
    Task<TResponse?> InvokeMethodAsync<TResponse>(string appId, string methodName, CancellationToken cancellationToken = default);
    Task<TResponse?> InvokeMethodAsync<TRequest, TResponse>(string appId, string methodName, TRequest data, CancellationToken cancellationToken = default);
    Task InvokeMethodAsync<TRequest>(string appId, string methodName, TRequest data, CancellationToken cancellationToken = default);
}

public class DaprServiceInvocation : IDaprServiceInvocation
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprServiceInvocation> _logger;

    public DaprServiceInvocation(DaprClient daprClient, ILogger<DaprServiceInvocation> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    public async Task<TResponse?> InvokeMethodAsync<TResponse>(
        string appId, 
        string methodName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Invoking Dapr method: {AppId}/{MethodName}", appId, methodName);
            
            var response = await _daprClient.InvokeMethodAsync<TResponse>(
                HttpMethod.Get, 
                appId, 
                methodName, 
                cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Dapr method: {AppId}/{MethodName}", appId, methodName);
            throw;
        }
    }

    public async Task<TResponse?> InvokeMethodAsync<TRequest, TResponse>(
        string appId, 
        string methodName, 
        TRequest data, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Invoking Dapr method with data: {AppId}/{MethodName}", appId, methodName);
            
            var response = await _daprClient.InvokeMethodAsync<TRequest, TResponse>(
                HttpMethod.Post, 
                appId, 
                methodName, 
                data, 
                cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Dapr method: {AppId}/{MethodName}", appId, methodName);
            throw;
        }
    }

    public async Task InvokeMethodAsync<TRequest>(
        string appId, 
        string methodName, 
        TRequest data, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Invoking Dapr method (fire-and-forget): {AppId}/{MethodName}", appId, methodName);
            
            await _daprClient.InvokeMethodAsync(
                HttpMethod.Post, 
                appId, 
                methodName, 
                data, 
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Dapr method: {AppId}/{MethodName}", appId, methodName);
            throw;
        }
    }
}


