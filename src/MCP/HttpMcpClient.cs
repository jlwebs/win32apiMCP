using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinAPIMCP.MCP;

/// <summary>
/// Simple HTTP client for connecting to Windows API MCP Server
/// Useful for testing and custom integrations
/// </summary>
public class HttpMcpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<HttpMcpClient>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpMcpClient(string baseUrl = "http://localhost:3000/mcp", ILogger<HttpMcpClient>? logger = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WinAPI-MCP-Client/1.0");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Test connection to the MCP server
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to MCP server at {BaseUrl}", _baseUrl);
            return false;
        }
    }

    /// <summary>
    /// Get server information
    /// </summary>
    public async Task<JsonElement> GetServerInfoAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/info");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    /// <summary>
    /// Get available tools
    /// </summary>
    public async Task<JsonElement> GetToolsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/tools");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    /// <summary>
    /// Call a tool with arguments
    /// </summary>
    public async Task<JsonElement> CallToolAsync(string toolName, object? arguments = null)
    {
        var request = new McpToolCallRequest
        {
            Name = toolName,
            Arguments = arguments != null 
                ? JsonSerializer.SerializeToElement(arguments, _jsonOptions)
                : new JsonElement()
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/tools/call", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(responseContent);
    }

    /// <summary>
    /// Enumerate all windows
    /// </summary>
    public async Task<JsonElement> EnumerateWindowsAsync(bool includeMinimized = false, string? titleFilter = null)
    {
        var args = new Dictionary<string, object>();
        if (includeMinimized) args["include_minimized"] = true;
        if (!string.IsNullOrEmpty(titleFilter)) args["filter_by_title"] = titleFilter;

        return await CallToolAsync("enumerate_windows", args.Count > 0 ? args : null);
    }

    /// <summary>
    /// Get window information
    /// </summary>
    public async Task<JsonElement> GetWindowInfoAsync(string windowHandle)
    {
        return await CallToolAsync("get_window_info", new { window_handle = windowHandle });
    }

    /// <summary>
    /// Set window focus
    /// </summary>
    public async Task<JsonElement> SetWindowFocusAsync(string windowHandle)
    {
        return await CallToolAsync("set_window_focus", new { window_handle = windowHandle });
    }

    /// <summary>
    /// Find windows by title pattern
    /// </summary>
    public async Task<JsonElement> FindWindowsByTitleAsync(string titlePattern, bool exactMatch = false)
    {
        return await CallToolAsync("find_windows_by_title", new { title_pattern = titlePattern, exact_match = exactMatch });
    }

    /// <summary>
    /// Enumerate processes
    /// </summary>
    public async Task<JsonElement> EnumerateProcessesAsync(bool includeSystem = false)
    {
        return await CallToolAsync("enumerate_processes", new { include_system = includeSystem });
    }

    /// <summary>
    /// Get health status
    /// </summary>
    public async Task<JsonElement> GetHealthAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/health");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}