namespace WinAPIMCP.Services;

/// <summary>
/// Interface for sending MCP messages
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Sends a message to the MCP client
    /// </summary>
    /// <param name="message">Message content</param>
    /// <param name="messageType">Type of message</param>
    /// <returns>True if message was sent successfully</returns>
    Task<bool> SendMessageAsync(string message, string messageType = "info");

    /// <summary>
    /// Sends an error message to the MCP client
    /// </summary>
    /// <param name="error">Error message</param>
    /// <param name="details">Additional error details</param>
    /// <returns>True if message was sent successfully</returns>
    Task<bool> SendErrorAsync(string error, string? details = null);

    /// <summary>
    /// Sends a tool response to the MCP client
    /// </summary>
    /// <param name="toolName">Name of the tool that was executed</param>
    /// <param name="result">Tool execution result</param>
    /// <returns>True if message was sent successfully</returns>
    Task<bool> SendToolResponseAsync(string toolName, object result);

    /// <summary>
    /// Checks if the message sender is connected and ready to send messages
    /// </summary>
    /// <returns>True if ready to send messages</returns>
    bool IsConnected();
}