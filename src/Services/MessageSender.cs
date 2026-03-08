using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of message sender for MCP communication
/// </summary>
public class MessageSender : IMessageSender
{
    private readonly ILogger<MessageSender> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MessageSender(ILogger<MessageSender> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<bool> SendMessageAsync(string message, string messageType = "info")
    {
        try
        {
            _logger.LogDebug("Sending {MessageType} message: {Message}", messageType, message);
            
            // In a real implementation, this would send the message over the MCP transport
            // For now, we'll just log it to console
            var messageObj = new
            {
                Type = messageType,
                Content = message,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(messageObj, _jsonOptions);
            Console.WriteLine($"[MCP] {json}");
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            return false;
        }
    }

    public async Task<bool> SendErrorAsync(string error, string? details = null)
    {
        try
        {
            _logger.LogError("Sending error message: {Error}", error);
            
            var errorObj = new
            {
                Type = "error",
                Error = error,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(errorObj, _jsonOptions);
            Console.WriteLine($"[MCP ERROR] {json}");
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error message");
            return false;
        }
    }

    public async Task<bool> SendToolResponseAsync(string toolName, object result)
    {
        try
        {
            _logger.LogDebug("Sending tool response for {ToolName}", toolName);
            
            var responseObj = new
            {
                Type = "tool_response",
                Tool = toolName,
                Result = result,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(responseObj, _jsonOptions);
            Console.WriteLine($"[MCP TOOL] {json}");
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send tool response for {ToolName}", toolName);
            return false;
        }
    }

    public bool IsConnected()
    {
        // In a real implementation, this would check the actual connection status
        // For now, we'll always return true since we're using console output
        return true;
    }
}