using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using WinAPIMCP.Services;
using WinAPIMCP.Models;

namespace WinAPIMCP.MCP;

/// <summary>
/// HTTP-based transport for MCP protocol supporting multiple concurrent connections
/// </summary>
[ApiController]
[Route("mcp")]
public class HttpMcpTransportController : ControllerBase
{
    private readonly ILogger<HttpMcpTransportController> _logger;
    private readonly IWindowManager _windowManager;
    private readonly IProcessManager _processManager;
    private readonly IActivityTracker _activityTracker;
    private static readonly ConcurrentDictionary<string, DateTime> _activeSessions = new();

    public HttpMcpTransportController(
        ILogger<HttpMcpTransportController> logger,
        IWindowManager windowManager,
        IProcessManager processManager,
        IActivityTracker activityTracker)
    {
        _logger = logger;
        _windowManager = windowManager;
        _processManager = processManager;
        _activityTracker = activityTracker;
    }

    /// <summary>
    /// MCP protocol endpoint for tool calls
    /// </summary>
    [HttpPost("tools/call")]
    public async Task<IActionResult> CallTool([FromBody] McpToolCallRequest request)
    {
        var sessionId = GetOrCreateSession();
        var activityId = _activityTracker.StartActivity(
            ActivityType.SystemQuery,
            request.Name,
            JsonSerializer.Serialize(request.Arguments),
            $"HTTP-{sessionId}");

        try
        {
            _logger.LogInformation("HTTP MCP tool call: {ToolName} from session {SessionId}", request.Name, sessionId);

            var result = request.Name switch
            {
                "enumerate_windows" => await HandleEnumerateWindows(request.Arguments),
                "enumerate_child_windows" => await HandleEnumerateChildWindows(request.Arguments),
                "get_window_info" => await HandleGetWindowInfo(request.Arguments),
                "set_window_focus" => await HandleSetWindowFocus(request.Arguments),
                "show_window" => await HandleShowWindow(request.Arguments),
                "find_windows_by_title" => await HandleFindWindowsByTitle(request.Arguments),
                "find_windows_by_class" => await HandleFindWindowsByClass(request.Arguments),
                "enumerate_processes" => await HandleEnumerateProcesses(request.Arguments),
                "get_process_info" => await HandleGetProcessInfo(request.Arguments),
                "find_processes_by_name" => await HandleFindProcessesByName(request.Arguments),
                _ => throw new NotSupportedException($"Tool '{request.Name}' is not supported")
            };

            _activityTracker.CompleteActivity(activityId, JsonSerializer.Serialize(result), 0);

            return Ok(new McpToolCallResponse
            {
                Content = new[]
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", request.Name);
            _activityTracker.FailActivity(activityId, ex.Message, 0);

            return BadRequest(new McpToolCallResponse
            {
                Content = new[]
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error: {ex.Message}"
                    }
                },
                IsError = true
            });
        }
    }

    /// <summary>
    /// MCP protocol initialize endpoint
    /// </summary>
    [HttpPost("initialize")]
    public IActionResult Initialize([FromBody] object request)
    {
        return Ok(new
        {
            protocolVersion = "1.0",
            serverInfo = new
            {
                name = "Windows API MCP Server",
                version = "1.0.0"
            },
            capabilities = new
            {
                tools = new { },
                logging = new { }       
            }
        });
    }

    /// <summary>
    /// MCP protocol tools/list endpoint
    /// </summary>
    [HttpPost("tools/list")]
    public IActionResult ListTools([FromBody] object request)
    {
        var tools = ToolRegistry.GetTools();
        return Ok(new
        {
            tools = ((dynamic)tools).tools
        });
    }

    /// <summary>
    /// Get available tools with enhanced documentation
    /// </summary>
    [HttpGet("tools")]
    public IActionResult GetTools()
    {
        return Ok(ToolRegistry.GetTools());
    }

    /// <summary>
    /// Get basic tools list (for compatibility)
    /// </summary>
    [HttpGet("tools/basic")]
    public IActionResult GetBasicTools()
    {
        var tools = new[]
        {
            new McpTool
            {
                Name = "enumerate_windows",
                Description = "Enumerate all visible desktop windows",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        include_minimized = new { type = "boolean", description = "Include minimized windows" },
                        filter_by_title = new { type = "string", description = "Optional regex pattern to filter by window title" }
                    }
                }
            },
            new McpTool
            {
                Name = "enumerate_child_windows", 
                Description = "Enumerate child windows of a parent window",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        parent_handle = new { type = "string", description = "Handle of parent window" },
                        include_all_descendants = new { type = "boolean", description = "Include all descendant windows" }
                    },
                    required = new[] { "parent_handle" }
                }
            },
            new McpTool
            {
                Name = "get_window_info",
                Description = "Get detailed information about a specific window",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        window_handle = new { type = "string", description = "Window handle" }
                    },
                    required = new[] { "window_handle" }
                }
            },
            new McpTool
            {
                Name = "set_window_focus",
                Description = "Set focus to a specific window",
                InputSchema = new
                {
                    type = "object", 
                    properties = new
                    {
                        window_handle = new { type = "string", description = "Window handle" }
                    },
                    required = new[] { "window_handle" }
                }
            },
            new McpTool
            {
                Name = "show_window",
                Description = "Show, hide, minimize, or maximize a window",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        window_handle = new { type = "string", description = "Window handle" },
                        show_state = new { type = "integer", description = "Show state (1=normal, 2=minimized, 3=maximized, 0=hidden)" }
                    },
                    required = new[] { "window_handle", "show_state" }
                }
            },
            new McpTool
            {
                Name = "find_windows_by_title",
                Description = "Find windows by title pattern",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title_pattern = new { type = "string", description = "Title pattern (regex)" },
                        exact_match = new { type = "boolean", description = "Exact match instead of regex" }
                    },
                    required = new[] { "title_pattern" }
                }
            },
            new McpTool
            {
                Name = "find_windows_by_class",
                Description = "Find windows by class name",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        class_name = new { type = "string", description = "Window class name" }
                    },
                    required = new[] { "class_name" }
                }
            },
            new McpTool
            {
                Name = "enumerate_processes",
                Description = "Enumerate running processes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        include_system = new { type = "boolean", description = "Include system processes" },
                        filter_by_name = new { type = "string", description = "Optional name pattern filter" }
                    }
                }
            },
            new McpTool
            {
                Name = "get_process_info",
                Description = "Get detailed information about a specific process",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        process_id = new { type = "integer", description = "Process ID" }
                    },
                    required = new[] { "process_id" }
                }
            },
            new McpTool
            {
                Name = "find_processes_by_name",
                Description = "Find processes by name pattern",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        name_pattern = new { type = "string", description = "Process name pattern" },
                        exact_match = new { type = "boolean", description = "Exact match instead of regex" }
                    },
                    required = new[] { "name_pattern" }
                }
            }
        };

        return Ok(new { tools });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            active_sessions = _activeSessions.Count,
            uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// Get server info
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetServerInfo()
    {
        return Ok(new
        {
            name = "Windows API MCP Server",
            version = "1.0.0",
            protocol_version = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                prompts = new { },
                resources = new { }
            },
            agent_features = new
            {
                enhanced_documentation = true,
                workflow_guidance = true,
                context_suggestions = true,
                troubleshooting_help = true,
                example_driven_api = true
            }
        });
    }

    /// <summary>
    /// Get suggested next actions based on current context
    /// </summary>
    [HttpPost("suggestions")]
    public IActionResult GetSuggestions([FromBody] SuggestionRequest request)
    {
        var suggestions = GenerateContextSuggestions(request.LastAction, request.CurrentGoal, request.AvailableData);
        return Ok(new { suggestions });
    }

    /// <summary>
    /// Get workflow guidance for common UI automation patterns
    /// </summary>
    [HttpGet("workflows")]
    public IActionResult GetWorkflows()
    {
        return Ok(new
        {
            workflows = new
            {
                window_discovery = new
                {
                    name = "Window Discovery",
                    description = "Find and identify target windows",
                    steps = new[]
                    {
                        new { step = 1, tool = "enumerate_windows", description = "Get all visible windows", parameters = "{}" },
                        new { step = 2, tool = "find_windows_by_title", description = "Filter by application name", parameters = "{\"title_pattern\": \".*YourApp.*\"}" }
                    },
                    when_to_use = "When you need to locate a specific application window",
                    expected_outcome = "Handle of target window for further interaction"
                },
                window_control = new
                {
                    name = "Window State Management",
                    description = "Control window visibility and focus",
                    steps = new[]
                    {
                        new { step = 1, tool = "get_window_info", description = "Check current window state" },
                        new { step = 2, tool = "show_window", description = "Adjust visibility (restore/maximize)" },
                        new { step = 3, tool = "set_window_focus", description = "Bring window to foreground" }
                    },
                    when_to_use = "When you need to ensure window is ready for interaction",
                    expected_outcome = "Window is visible, focused, and ready for input"
                },
                ui_exploration = new
                {
                    name = "UI Element Discovery",
                    description = "Map out controls within a window",
                    steps = new[]
                    {
                        new { step = 1, tool = "enumerate_child_windows", description = "Get all child controls" },
                        new { step = 2, tool = "get_window_info", description = "Analyze specific controls" }
                    },
                    when_to_use = "When you need to find buttons, text fields, or other UI elements",
                    expected_outcome = "Map of available UI controls for interaction"
                },
                app_automation = new
                {
                    name = "Complete Application Automation",
                    description = "End-to-end automation workflow",
                    steps = new[]
                    {
                        new { step = 1, tool = "enumerate_processes", description = "Verify app is running" },
                        new { step = 2, tool = "find_windows_by_title", description = "Locate app window" },
                        new { step = 3, tool = "show_window", description = "Ensure window is visible" },
                        new { step = 4, tool = "set_window_focus", description = "Give window focus" },
                        new { step = 5, tool = "enumerate_child_windows", description = "Map UI controls" },
                        new { step = 6, tool = "send_input", description = "Interact with controls" }
                    },
                    when_to_use = "For comprehensive application automation tasks",
                    expected_outcome = "Successfully automated interaction with target application"
                }
            },
            decision_tree = new
            {
                start_here = "If you don't know what windows are available, use 'enumerate_windows' or 'enumerate_processes'",
                if_window_known = "If you know the window title/pattern, use 'find_windows_by_title'",
                if_window_found = "Use 'get_window_info' to verify state, then 'set_window_focus' to prepare for interaction",
                if_need_controls = "Use 'enumerate_child_windows' to discover UI elements within the window",
                if_window_hidden = "Use 'show_window' with appropriate state (1=normal, 3=maximize, 9=restore)"
            }
        });
    }

    /// <summary>
    /// Get troubleshooting help for common issues
    /// </summary>
    [HttpGet("troubleshooting")]
    public IActionResult GetTroubleshooting()
    {
        return Ok(new
        {
            common_issues = new
            {
                window_not_found = new
                {
                    symptoms = new[] { "enumerate_windows returns empty or doesn't include target", "Window handle not found" },
                    diagnosis = new[]
                    {
                        "Window might be minimized - try include_minimized=true",
                        "Window might be child window - check parent applications",
                        "Application might not be running - check with enumerate_processes",
                        "Title might have changed - use broader regex patterns"
                    },
                    solutions = new[]
                    {
                        "enumerate_windows with include_minimized=true",
                        "enumerate_processes to verify app is running",
                        "Use find_windows_by_title with regex patterns like '.*PartialTitle.*'",
                        "Check if window is a dialog or popup of another application"
                    }
                },
                permission_denied = new
                {
                    symptoms = new[] { "Access denied errors", "Cannot interact with window" },
                    diagnosis = new[]
                    {
                        "Target process running as administrator",
                        "System security restrictions", 
                        "Agentic mode disabled and user denied permission"
                    },
                    solutions = new[]
                    {
                        "Check isElevated property in process info",
                        "Enable elevated access in server settings",
                        "Run MCP server as administrator",
                        "Enable agentic mode for automated permissions"
                    }
                },
                interaction_failed = new
                {
                    symptoms = new[] { "Controls not responding", "Focus not working" },
                    diagnosis = new[]
                    {
                        "Window not focused",
                        "Window in wrong state (minimized/hidden)",
                        "Timing issues with UI updates"
                    },
                    solutions = new[]
                    {
                        "Always call set_window_focus before interactions",
                        "Use show_window to ensure proper state",
                        "Add delays between rapid UI operations",
                        "Verify window state with get_window_info"
                    }
                }
            },
            debugging_workflow = new
            {
                step_1 = "enumerate_windows - Verify target window is visible",
                step_2 = "enumerate_processes - Confirm application is running",
                step_3 = "get_window_info - Check window state and properties",
                step_4 = "Check server logs at /mcp/health for detailed error information"
            }
        });
    }

    /// <summary>
    /// Get contextual help based on current situation
    /// </summary>
    [HttpPost("help")]
    public IActionResult GetContextualHelp([FromBody] HelpRequest request)
    {
        var help = GenerateContextualHelp(request.CurrentSituation, request.DesiredOutcome, request.ErrorEncountered);
        return Ok(help);
    }

    // Tool implementation methods
    private async Task<object> HandleEnumerateWindows(JsonElement arguments)
    {
        var includeMinimized = arguments.TryGetProperty("include_minimized", out var prop1) && prop1.GetBoolean();
        var titleFilter = arguments.TryGetProperty("filter_by_title", out var prop2) ? prop2.GetString() : null;
        
        var windows = await _windowManager.EnumerateWindowsAsync(includeMinimized, titleFilter);
        return new { windows = windows.ToArray() };
    }

    private async Task<object> HandleEnumerateChildWindows(JsonElement arguments)
    {
        var parentHandle = new IntPtr(Convert.ToInt64(arguments.GetProperty("parent_handle").GetString()!, 16));
        var includeAll = arguments.TryGetProperty("include_all_descendants", out var prop) && prop.GetBoolean();
        
        var children = await _windowManager.EnumerateChildWindowsAsync(parentHandle, includeAll);
        return new { children = children.ToArray() };
    }

    private async Task<object> HandleGetWindowInfo(JsonElement arguments)
    {
        var handle = new IntPtr(Convert.ToInt64(arguments.GetProperty("window_handle").GetString()!, 16));
        var windowInfo = await _windowManager.GetWindowInfoAsync(handle);
        return new { window = windowInfo };
    }

    private async Task<object> HandleSetWindowFocus(JsonElement arguments)
    {
        var handle = new IntPtr(Convert.ToInt64(arguments.GetProperty("window_handle").GetString()!, 16));
        var success = await _windowManager.SetWindowFocusAsync(handle);
        return new { success };
    }

    private async Task<object> HandleShowWindow(JsonElement arguments)
    {
        var handle = new IntPtr(Convert.ToInt64(arguments.GetProperty("window_handle").GetString()!, 16));
        var showState = arguments.GetProperty("show_state").GetInt32();
        var success = await _windowManager.ShowWindowAsync(handle, showState);
        return new { success };
    }

    private async Task<object> HandleFindWindowsByTitle(JsonElement arguments)
    {
        var titlePattern = arguments.GetProperty("title_pattern").GetString()!;
        var exactMatch = arguments.TryGetProperty("exact_match", out var prop) && prop.GetBoolean();
        
        var windows = await _windowManager.FindWindowsByTitleAsync(titlePattern, exactMatch);
        return new { windows = windows.ToArray() };
    }

    private async Task<object> HandleFindWindowsByClass(JsonElement arguments)
    {
        var className = arguments.GetProperty("class_name").GetString()!;
        var windows = await _windowManager.FindWindowsByClassAsync(className);
        return new { windows = windows.ToArray() };
    }

    private async Task<object> HandleEnumerateProcesses(JsonElement arguments)
    {
        var includeSystem = arguments.TryGetProperty("include_system", out var prop1) && prop1.GetBoolean();
        var processes = await _processManager.EnumerateProcessesAsync(includeSystem);
        return new { processes = processes.ToArray() };
    }

    private async Task<object> HandleGetProcessInfo(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var processInfo = await _processManager.GetProcessInfoAsync(processId);
        return new { process = processInfo };
    }

    private async Task<object> HandleFindProcessesByName(JsonElement arguments)
    {
        var namePattern = arguments.GetProperty("name_pattern").GetString()!;
        var exactMatch = arguments.TryGetProperty("exact_match", out var prop) && prop.GetBoolean();
        
        var processes = await _processManager.FindProcessesByNameAsync(namePattern, exactMatch);
        return new { processes = processes.ToArray() };
    }

    private string GetOrCreateSession()
    {
        var sessionId = HttpContext.Request.Headers["X-Session-ID"].FirstOrDefault() 
                       ?? HttpContext.Connection.Id 
                       ?? Guid.NewGuid().ToString("N")[..8];
        
        _activeSessions.AddOrUpdate(sessionId, DateTime.UtcNow, (key, value) => DateTime.UtcNow);
        
        // Cleanup old sessions (older than 1 hour)
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var oldSessions = _activeSessions.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var oldSession in oldSessions)
        {
            _activeSessions.TryRemove(oldSession, out _);
        }

        return sessionId;
    }

    private object[] GenerateContextSuggestions(string? lastAction, string? currentGoal, object? availableData)
    {
        var suggestions = new List<object>();

        // Base suggestions based on last action
        switch (lastAction?.ToLowerInvariant())
        {
            case "enumerate_windows":
                suggestions.Add(new
                {
                    tool = "get_window_info",
                    reason = "Get detailed information about a specific window",
                    confidence = 0.9
                });
                suggestions.Add(new
                {
                    tool = "set_window_focus",
                    reason = "Focus on a window for interaction",
                    confidence = 0.8
                });
                break;

            case "find_windows_by_title":
                suggestions.Add(new
                {
                    tool = "show_window",
                    reason = "Ensure window is visible and properly sized",
                    confidence = 0.9
                });
                suggestions.Add(new
                {
                    tool = "enumerate_child_windows",
                    reason = "Explore controls within the found window",
                    confidence = 0.8
                });
                break;

            case "set_window_focus":
                suggestions.Add(new
                {
                    tool = "enumerate_child_windows",
                    reason = "Now that window has focus, explore its controls",
                    confidence = 0.9
                });
                break;

            default:
                // No specific last action, provide general starting suggestions
                suggestions.Add(new
                {
                    tool = "enumerate_windows",
                    reason = "Start by discovering available windows",
                    confidence = 0.7
                });
                suggestions.Add(new
                {
                    tool = "enumerate_processes",
                    reason = "Check what applications are running",
                    confidence = 0.6
                });
                break;
        }

        // Goal-based suggestions
        if (currentGoal?.ToLowerInvariant().Contains("automation") == true)
        {
            suggestions.Add(new
            {
                workflow = "app_automation",
                reason = "Use the complete automation workflow for end-to-end tasks",
                confidence = 0.9
            });
        }

        return suggestions.ToArray();
    }

    private object GenerateContextualHelp(string? currentSituation, string? desiredOutcome, string? errorEncountered)
    {
        var help = new
        {
            situation_analysis = AnalyzeSituation(currentSituation),
            recommended_actions = GetRecommendedActions(currentSituation, desiredOutcome),
            error_guidance = GetErrorGuidance(errorEncountered),
            next_steps = GetNextSteps(currentSituation, desiredOutcome)
        };

        return help;
    }

    private object AnalyzeSituation(string? situation)
    {
        if (string.IsNullOrEmpty(situation))
        {
            return new { analysis = "No current situation provided", recommendation = "Start with enumerate_windows or enumerate_processes" };
        }

        if (situation.ToLowerInvariant().Contains("window not found"))
        {
            return new 
            { 
                analysis = "Target window discovery issue",
                likely_causes = new[] { "Window minimized", "Application not running", "Title changed" },
                immediate_action = "Try enumerate_windows with include_minimized=true"
            };
        }

        if (situation.ToLowerInvariant().Contains("permission denied"))
        {
            return new
            {
                analysis = "Access control issue",
                likely_causes = new[] { "Elevated process", "Security restrictions", "Agentic mode disabled" },
                immediate_action = "Check process elevation status and server settings"
            };
        }

        return new { analysis = "General situation", recommendation = "Provide more specific details for better guidance" };
    }

    private string[] GetRecommendedActions(string? situation, string? outcome)
    {
        var actions = new List<string>();

        if (string.IsNullOrEmpty(situation))
        {
            actions.Add("enumerate_windows - Start by seeing what's available");
            actions.Add("enumerate_processes - Check running applications");
        }
        else if (situation.Contains("found window"))
        {
            actions.Add("get_window_info - Verify window state");
            actions.Add("set_window_focus - Prepare for interaction");
            actions.Add("enumerate_child_windows - Discover controls");
        }

        return actions.ToArray();
    }

    private object? GetErrorGuidance(string? error)
    {
        if (string.IsNullOrEmpty(error)) return null;

        if (error.ToLowerInvariant().Contains("access denied"))
        {
            return new
            {
                error_type = "Permission Error",
                solutions = new[]
                {
                    "Run MCP server as administrator",
                    "Enable elevated process access in settings",
                    "Check if target process requires admin privileges"
                }
            };
        }

        if (error.ToLowerInvariant().Contains("not found"))
        {
            return new
            {
                error_type = "Resource Not Found",
                solutions = new[]
                {
                    "Verify the window/process still exists",
                    "Check if application has closed or restarted",
                    "Use broader search patterns"
                }
            };
        }

        return new { error_type = "General Error", suggestion = "Check server logs for detailed information" };
    }

    private string[] GetNextSteps(string? situation, string? outcome)
    {
        if (string.IsNullOrEmpty(outcome))
        {
            return new[] { "Define your specific goal for better guidance" };
        }

        if (outcome.ToLowerInvariant().Contains("automate"))
        {
            return new[]
            {
                "1. Discover target application (enumerate_windows/find_windows_by_title)",
                "2. Ensure window is ready (show_window, set_window_focus)",
                "3. Map out UI controls (enumerate_child_windows)",
                "4. Execute specific interactions with controls"
            };
        }

        return new[] { "Provide more specific outcome details for targeted guidance" };
    }
}

// MCP Protocol Data Models
public class McpToolCallRequest
{
    public string Name { get; set; } = string.Empty;
    public JsonElement Arguments { get; set; }
}

public class McpToolCallResponse
{
    public McpContent[] Content { get; set; } = Array.Empty<McpContent>();
    public bool IsError { get; set; } = false;
}

public class McpContent
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? InputSchema { get; set; }
}

public class SuggestionRequest
{
    public string? LastAction { get; set; }
    public string? CurrentGoal { get; set; }
    public object? AvailableData { get; set; }
}

public class HelpRequest
{
    public string? CurrentSituation { get; set; }
    public string? DesiredOutcome { get; set; }
    public string? ErrorEncountered { get; set; }
}
