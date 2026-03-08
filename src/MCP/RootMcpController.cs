using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WinAPIMCP.Services;
using WinAPIMCP.Models;

namespace WinAPIMCP.MCP;

/// <summary>
/// Root MCP protocol controller for VS Code MCP extension compatibility
/// </summary>
[ApiController]
[Route("")]
public class RootMcpController : ControllerBase
{
    private readonly ILogger<RootMcpController> _logger;
    private readonly IWindowManager _windowManager;
    private readonly IProcessManager _processManager;
    private readonly IUIInteractionManager _uiInteractionManager;
    private readonly IActivityTracker _activityTracker;

    public RootMcpController(
        ILogger<RootMcpController> logger,
        IWindowManager windowManager,
        IProcessManager processManager,
        IUIInteractionManager uiInteractionManager,
        IActivityTracker activityTracker)
    {
        _logger = logger;
        _windowManager = windowManager;
        _processManager = processManager;
        _uiInteractionManager = uiInteractionManager;
        _activityTracker = activityTracker;
    }

    /// <summary>
    /// Simple GET endpoint at root to avoid 404
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var tools = ToolRegistry.GetTools();
        return Ok(new { 
            status = "MCP Server Running", 
            version = "1.0.0",
            protocol = "MCP HTTP Transport",
            availableTools = ((dynamic)tools).tools.Length,
            endpoints = new[] { "GET /", "POST /" },
            toolsListSample = "POST / with {\"jsonrpc\":\"2.0\",\"method\":\"tools/list\",\"id\":1}"
        });
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new { 
            message = "MCP Server Test Endpoint",
            timestamp = DateTime.UtcNow,
            sampleToolsListRequest = new {
                jsonrpc = "2.0",
                method = "tools/list",
                id = 1
            },
            sampleToolCallRequest = new {
                jsonrpc = "2.0",
                method = "tools/call",
                id = 2,
                @params = new {
                    name = "enumerate_windows",
                    arguments = new {
                        include_minimized = false
                    }
                }
            }
        });
    }

    /// <summary>
    /// MCP JSON-RPC endpoint - handles all MCP protocol messages
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] JsonElement request)
    {
        // Track MCP protocol activities
        Guid? protocolActivityId = null;
        var protocolStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Log the full request for debugging
            _logger.LogInformation("Raw MCP request: {Request}", request.ToString());
            
            string method = request.GetProperty("method").GetString() ?? "";
            var id = request.TryGetProperty("id", out var idElement) ? idElement : (JsonElement?)null;
            var @params = request.TryGetProperty("params", out var paramsElement) ? paramsElement : (JsonElement?)null;

            _logger.LogInformation("MCP request - Method: {Method}, ID: {Id}, HasParams: {HasParams}", 
                method, id?.ToString() ?? "null", @params.HasValue);
            
            if (method != "tools/call") // tools/call is tracked separately
            {
                protocolActivityId = _activityTracker.StartActivity(
                    ActivityType.SystemQuery,
                    $"MCP {method}",
                    @params?.ToString() ?? "(no params)",
                    "MCP Client");
            }

            object result = method switch
            {
                "initialize" => new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    serverInfo = new
                    {
                        name = "Windows API MCP Server",
                        version = "1.0.0"
                    }
                },
                "tools/list" => ToolRegistry.GetTools(),
                "tools/call" => await HandleToolCall(@params),
                _ => throw new NotSupportedException($"Method '{method}' not supported")
            };

            // Complete protocol activity tracking
            if (protocolActivityId.HasValue)
            {
                protocolStopwatch.Stop();
                _ = Task.Run(() => 
                {
                    try
                    {
                        var resultSummary = method == "tools/list" ? $"Listed {((dynamic)result).tools?.Length ?? 0} tools" : $"MCP {method} completed";
                        _activityTracker.CompleteActivity(protocolActivityId.Value, resultSummary, protocolStopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to complete protocol activity tracking for {Method}", method);
                    }
                });
            }
            
            return Ok(new
            {
                jsonrpc = "2.0",
                id = id,
                result = result
            });
        }
        catch (Exception ex)
        {
            // Complete protocol activity with error
            if (protocolActivityId.HasValue)
            {
                protocolStopwatch.Stop();
                _ = Task.Run(() => 
                {
                    try
                    {
                        _activityTracker.FailActivity(protocolActivityId.Value, ex.Message, protocolStopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception activityEx)
                    {
                        _logger.LogWarning(activityEx, "Failed to complete error protocol activity tracking");
                    }
                });
            }
            
            _logger.LogError(ex, "Error handling MCP request");
            var errorId = request.TryGetProperty("id", out var errorIdElement) ? errorIdElement : (JsonElement?)null;
            return Ok(new
            {
                jsonrpc = "2.0",
                id = errorId,
                error = new
                {
                    code = -32000,
                    message = ex.Message
                }
            });
        }
    }

    private async Task<object> HandleToolCall(JsonElement? @params)
    {
        if (!@params.HasValue)
            throw new ArgumentException("Missing params for tools/call");

        var paramsValue = @params.Value;
        if (!paramsValue.TryGetProperty("name", out var nameElement))
            throw new ArgumentException("Missing tool name");

        string toolName = nameElement.GetString() ?? "";
        var arguments = paramsValue.TryGetProperty("arguments", out var argsElement) ? argsElement : (JsonElement?)null;
        var parametersText = arguments?.ToString() ?? "(no arguments)";

        _logger.LogInformation("Executing tool: {ToolName} with arguments: {Arguments}", toolName, parametersText);

        // Start activity tracking
        var activityId = _activityTracker.StartActivity(
            ActivityType.ToolCall,
            toolName,
            parametersText,
            "MCP Agent");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = toolName switch
            {
                "enumerate_windows" => await HandleEnumerateWindows(arguments),
                "enumerate_child_windows" => await HandleEnumerateChildWindows(arguments),
                "get_window_info" => await HandleGetWindowInfo(arguments),
                "set_window_focus" => await HandleSetWindowFocus(arguments),
                "show_window" => await HandleShowWindow(arguments),
                "find_windows_by_title" => await HandleFindWindowsByTitle(arguments),
                "find_windows_by_class" => await HandleFindWindowsByClass(arguments),
                "enumerate_processes" => await HandleEnumerateProcesses(arguments),
                "get_process_info" => await HandleGetProcessInfo(arguments),
                "find_processes_by_name" => await HandleFindProcessesByName(arguments),
                "click_at_coordinates" => await HandleClickAtCoordinates(arguments),
                "click_control" => await HandleClickControl(arguments),
                "send_text" => await HandleSendText(arguments),
                "send_keys" => await HandleSendKeys(arguments),
                "get_control_text" => await HandleGetControlText(arguments),
                "set_control_text" => await HandleSetControlText(arguments),
                "select_text" => await HandleSelectText(arguments),
                "find_elements_by_text" => await HandleFindElementsByText(arguments),
                "take_screenshot" => await HandleTakeScreenshot(arguments),
                "scroll_window" => await HandleScrollWindow(arguments),
                "get_cursor_position" => await HandleGetCursorPosition(arguments),
                "move_cursor" => await HandleMoveCursor(arguments),
                "drag_from_to" => await HandleDragFromTo(arguments),
                _ => new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Unknown tool: {toolName}"
                        }
                    },
                    isError = true
                }
            };

            stopwatch.Stop();
            
            // Complete activity on background thread to avoid UI thread issues
            _ = Task.Run(() => 
            {
                try
                {
                    var resultText = GetResultSummary(result);
                    _activityTracker.CompleteActivity(activityId, resultText, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update activity tracking for {ToolName}", toolName);
                }
            });
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            
            // Fail activity on background thread to avoid UI thread issues
            _ = Task.Run(() => 
            {
                try
                {
                    _activityTracker.FailActivity(activityId, ex.Message, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception activityEx)
                {
                    _logger.LogWarning(activityEx, "Failed to update activity tracking for failed {ToolName}", toolName);
                }
            });
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error executing tool '{toolName}': {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }

    // Window handler methods
    private async Task<object> HandleEnumerateWindows(JsonElement? arguments)
    {
        bool includeMinimized = false;
        string? titleFilter = null;

        if (arguments.HasValue)
        {
            if (arguments.Value.TryGetProperty("include_minimized", out var includeMinimizedElement))
                includeMinimized = includeMinimizedElement.GetBoolean();
            
            if (arguments.Value.TryGetProperty("filter_by_title", out var titleFilterElement))
                titleFilter = titleFilterElement.GetString();
        }

        var windows = await _windowManager.EnumerateWindowsAsync(includeMinimized, titleFilter);
        
        var windowsText = string.Join("\n", windows.Select(w => 
            $"Handle: 0x{w.Handle.ToInt64():X8}, Title: '{w.Title}', Class: '{w.ClassName}', PID: {w.ProcessId}, Visible: {w.IsVisible}, State: {w.State}"));

        var totalWindows = windows.Count();
        var resultText = totalWindows > 0 ? 
            $"Found {totalWindows} windows on the desktop:\n\n{windowsText}\n\nNext steps:\n- Use get_window_info with a handle to get detailed info\n- Use set_window_focus with a handle to bring window to front\n- Use enumerate_child_windows with a handle to explore UI controls" :
            "No windows found matching the criteria.\n\nTip: Try setting include_minimized=true to see hidden windows, or check if there are any visible windows on the desktop.";

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = resultText
                }
            }
        };
    }

    private async Task<object> HandleEnumerateChildWindows(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("parent_handle", out var handleElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing parent_handle parameter.\n\nUsage: enumerate_child_windows requires a parent window handle.\n\nExample:\n1. First call: enumerate_windows to find a parent window\n2. Copy a handle from results like '0x12345678'\n3. Then call: enumerate_child_windows with argument { parent_handle: '0x12345678' }\n\nOptional: Add { include_all_descendants: true } to get all child windows recursively."
                    }
                },
                isError = true
            };
        }

        var handleStr = handleElement.GetString();
        if (!TryParseHandle(handleStr, out IntPtr parentHandle))
            throw new ArgumentException($"Invalid parent_handle: {handleStr}");

        bool includeAllDescendants = true;
        if (arguments.Value.TryGetProperty("include_all_descendants", out var includeAllElement))
            includeAllDescendants = includeAllElement.GetBoolean();

        var childWindows = await _windowManager.EnumerateChildWindowsAsync(parentHandle, includeAllDescendants);
        
        var childWindowsText = string.Join("\n", childWindows.Select(w => 
            $"Handle: 0x{w.Handle.ToInt64():X8}, Title: '{w.Title}', Class: '{w.ClassName}', Bounds: {w.Bounds}"));

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Found {childWindows.Count()} child windows:\n{childWindowsText}"
                }
            }
        };
    }

    private async Task<object> HandleGetWindowInfo(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("window_handle", out var handleElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing window_handle parameter.\n\nUsage: get_window_info requires a window handle.\n\nExample:\n1. First call: enumerate_windows\n2. Copy a handle from results like '0x12345678'\n3. Then call: get_window_info with argument { window_handle: '0x12345678' }\n\nWindow handles are hexadecimal values starting with 0x."
                    }
                },
                isError = true
            };
        }

        var handleStr = handleElement.GetString();
        if (!TryParseHandle(handleStr, out IntPtr windowHandle))
            throw new ArgumentException($"Invalid window_handle: {handleStr}");

        var windowInfo = await _windowManager.GetWindowInfoAsync(windowHandle);
        if (windowInfo == null)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Window not found: 0x{windowHandle.ToInt64():X8}"
                    }
                }
            };
        }

        var infoText = $"Window Information for Handle 0x{windowInfo.Handle.ToInt64():X8}:\n\n" +
                      $"Title: '{windowInfo.Title}'\n" +
                      $"Class: '{windowInfo.ClassName}'\n" +
                      $"Process ID: {windowInfo.ProcessId}\n" +
                      $"Visible: {windowInfo.IsVisible}\n" +
                      $"Enabled: {windowInfo.IsEnabled}\n" +
                      $"State: {windowInfo.State}\n" +
                      $"Bounds: {windowInfo.Bounds} (X, Y, Width, Height)\n" +
                      $"Style: 0x{windowInfo.Style:X8}\n" +
                      $"Extended Style: 0x{windowInfo.ExtendedStyle:X8}\n\n" +
                      $"Available actions:\n" +
                      $"- set_window_focus: Bring this window to front\n" +
                      $"- show_window: Change window state (Normal/Minimized/Maximized/Hidden)\n" +
                      $"- enumerate_child_windows: Explore UI controls within this window";

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = infoText
                }
            }
        };
    }

    private async Task<object> HandleSetWindowFocus(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("window_handle", out var handleElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing window_handle parameter.\n\nUsage: set_window_focus requires a window handle to bring to foreground.\n\nExample:\n1. First call: enumerate_windows or find_windows_by_title\n2. Copy a handle from results like '0x12345678'\n3. Then call: set_window_focus with argument { window_handle: '0x12345678' }\n\nThis will bring the specified window to the front and give it focus."
                    }
                },
                isError = true
            };
        }

        var handleStr = handleElement.GetString();
        if (!TryParseHandle(handleStr, out IntPtr windowHandle))
            throw new ArgumentException($"Invalid window_handle: {handleStr}");

        var success = await _windowManager.SetWindowFocusAsync(windowHandle);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ? 
                        $"✓ Successfully brought window 0x{windowHandle.ToInt64():X8} to the foreground.\n\nThe window now has focus and should be visible on top of other windows.\n\nNext steps:\n- Use enumerate_child_windows to explore UI controls\n- Use get_window_info to verify the window state" :
                        $"✗ Failed to set focus to window 0x{windowHandle.ToInt64():X8}.\n\nPossible reasons:\n- Window handle is invalid or window was closed\n- Window is owned by a process with higher privileges\n- Window is disabled or hidden\n\nTry: get_window_info to check if window still exists"
                }
            }
        };
    }

    private async Task<object> HandleShowWindow(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            throw new ArgumentException("Missing arguments");
            
        if (!arguments.Value.TryGetProperty("window_handle", out var handleElement))
            throw new ArgumentException("Missing window_handle parameter");
            
        if (!arguments.Value.TryGetProperty("state", out var stateElement))
            throw new ArgumentException("Missing state parameter");

        var handleStr = handleElement.GetString();
        if (!TryParseHandle(handleStr, out IntPtr windowHandle))
            throw new ArgumentException($"Invalid window_handle: {handleStr}");

        var stateStr = stateElement.GetString();
        int showState = stateStr?.ToLower() switch
        {
            "normal" => 1, // SW_SHOWNORMAL
            "minimized" => 2, // SW_SHOWMINIMIZED
            "maximized" => 3, // SW_SHOWMAXIMIZED
            "hidden" => 0, // SW_HIDE
            _ => throw new ArgumentException($"Invalid state: {stateStr}")
        };

        var success = await _windowManager.ShowWindowAsync(windowHandle, showState);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ? 
                        $"Successfully changed window 0x{windowHandle.ToInt64():X8} to state '{stateStr}'" :
                        $"Failed to change window 0x{windowHandle.ToInt64():X8} to state '{stateStr}'"
                }
            }
        };
    }

    private async Task<object> HandleFindWindowsByTitle(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("title_pattern", out var titleElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing title_pattern parameter.\n\nUsage: find_windows_by_title searches for windows matching a title pattern.\n\nExamples:\n- { title_pattern: 'Notepad' } - finds windows with 'Notepad' in title\n- { title_pattern: '.*Calculator.*' } - regex pattern for Calculator\n- { title_pattern: 'Chrome', exact_match: true } - exact title match\n\nReturns all matching windows with their handles for further operations."
                    }
                },
                isError = true
            };
        }

        var titlePattern = titleElement.GetString() ?? "";
        bool exactMatch = false;
        
        if (arguments.Value.TryGetProperty("exact_match", out var exactMatchElement))
            exactMatch = exactMatchElement.GetBoolean();

        var windows = await _windowManager.FindWindowsByTitleAsync(titlePattern, exactMatch);
        
        var windowsText = string.Join("\n", windows.Select(w => 
            $"Handle: 0x{w.Handle.ToInt64():X8}, Title: '{w.Title}', Class: '{w.ClassName}', PID: {w.ProcessId}"));

        var matchCount = windows.Count();
        var resultText = matchCount > 0 ?
            $"Found {matchCount} windows matching '{titlePattern}':\n\n{windowsText}\n\nNext steps:\n- Use get_window_info with a handle for detailed information\n- Use set_window_focus with a handle to bring window to foreground\n- Use enumerate_child_windows with a handle to explore controls" :
            $"No windows found matching title pattern '{titlePattern}'.\n\nTips:\n- Try a partial match like 'Note' instead of 'Notepad'\n- Use enumerate_windows to see all available window titles\n- Check if the window is minimized and use include_minimized=true";

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = resultText
                }
            }
        };
    }

    private async Task<object> HandleFindWindowsByClass(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("class_name", out var classElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing class_name parameter.\n\nUsage: find_windows_by_class searches for windows by their window class.\n\nExamples:\n- { class_name: 'Notepad' } - finds all Notepad windows\n- { class_name: 'Chrome_WidgetWin_1' } - finds Chrome browser windows\n- { class_name: 'ApplicationFrameWindow' } - finds UWP app windows\n\nTip: Use enumerate_windows first to see class names of existing windows."
                    }
                },
                isError = true
            };
        }

        var className = classElement.GetString() ?? "";
        var windows = await _windowManager.FindWindowsByClassAsync(className);
        
        var windowsText = string.Join("\n", windows.Select(w => 
            $"Handle: 0x{w.Handle.ToInt64():X8}, Title: '{w.Title}', Class: '{w.ClassName}', PID: {w.ProcessId}"));

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Found {windows.Count()} windows with class '{className}':\n{windowsText}"
                }
            }
        };
    }

    // Process handler methods
    private async Task<object> HandleEnumerateProcesses(JsonElement? arguments)
    {
        bool includeSystem = false;
        string? nameFilter = null;

        if (arguments.HasValue)
        {
            if (arguments.Value.TryGetProperty("include_system", out var includeSystemElement))
                includeSystem = includeSystemElement.GetBoolean();
            
            if (arguments.Value.TryGetProperty("filter_by_name", out var nameFilterElement))
                nameFilter = nameFilterElement.GetString();
        }

        var processes = await _processManager.EnumerateProcessesAsync(includeSystem);
        
        if (!string.IsNullOrEmpty(nameFilter))
        {
            processes = processes.Where(p => p.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
        }
        
        var processesText = string.Join("\n", processes.Select(p => 
            $"PID: {p.Id}, Name: '{p.Name}', Type: {p.Type}, Architecture: {p.Architecture}, Memory: {p.MemoryUsage:N0} bytes, Windows: {p.WindowCount}"));

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Found {processes.Count()} processes:\n{processesText}"
                }
            }
        };
    }

    private async Task<object> HandleGetProcessInfo(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("process_id", out var pidElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing process_id parameter.\n\nUsage: get_process_info requires a numeric process ID.\n\nExample:\n1. First call: enumerate_processes to list all processes\n2. Find a process ID like 1234\n3. Then call: get_process_info with argument { process_id: 1234 }\n\nReturns detailed information about the specific process."
                    }
                },
                isError = true
            };
        }

        var processId = pidElement.GetInt32();
        var processInfo = await _processManager.GetProcessInfoAsync(processId);
        
        if (processInfo == null)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Process not found: PID {processId}"
                    }
                }
            };
        }

        var infoText = $"Process Information:\n" +
                      $"PID: {processInfo.Id}\n" +
                      $"Name: '{processInfo.Name}'\n" +
                      $"Type: {processInfo.Type}\n" +
                      $"Architecture: {processInfo.Architecture}\n" +
                      $"Executable Path: '{processInfo.ExecutablePath}'\n" +
                      $"Command Line: '{processInfo.CommandLine}'\n" +
                      $"Started: {processInfo.StartTime}\n" +
                      $"Is Elevated: {processInfo.IsElevated}\n" +
                      $"Memory Usage: {processInfo.MemoryUsage:N0} bytes\n" +
                      $"CPU Usage: {processInfo.CpuUsage:F2}%\n" +
                      $"Window Count: {processInfo.WindowCount}\n" +
                      $"Priority: {processInfo.PriorityClass}\n" +
                      $"Responding: {processInfo.IsResponding}";

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = infoText
                }
            }
        };
    }

    private async Task<object> HandleFindProcessesByName(JsonElement? arguments)
    {
        if (!arguments.HasValue || !arguments.Value.TryGetProperty("name_pattern", out var nameElement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Missing name_pattern parameter.\n\nUsage: find_processes_by_name searches for processes by name pattern.\n\nExamples:\n- { name_pattern: 'notepad' } - finds processes with 'notepad' in name\n- { name_pattern: 'chrome', exact_match: true } - exact name match\n- { name_pattern: '.*calc.*' } - regex pattern for calculator apps\n\nReturns all matching processes with their IDs and details."
                    }
                },
                isError = true
            };
        }

        var namePattern = nameElement.GetString() ?? "";
        bool exactMatch = false;
        
        if (arguments.Value.TryGetProperty("exact_match", out var exactMatchElement))
            exactMatch = exactMatchElement.GetBoolean();

        var processes = await _processManager.FindProcessesByNameAsync(namePattern, exactMatch);
        
        var processesText = string.Join("\n", processes.Select(p => 
            $"PID: {p.Id}, Name: '{p.Name}', Type: {p.Type}, Architecture: {p.Architecture}, Memory: {p.MemoryUsage:N0} bytes, Windows: {p.WindowCount}"));

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Found {processes.Count()} processes matching '{namePattern}':\n{processesText}"
                }
            }
        };
    }

    // Helper method to parse window handles
    private static bool TryParseHandle(string? handleStr, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (string.IsNullOrEmpty(handleStr))
            return false;

        // Remove 0x prefix if present
        if (handleStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            handleStr = handleStr.Substring(2);

        if (long.TryParse(handleStr, System.Globalization.NumberStyles.HexNumber, null, out long handleValue))
        {
            handle = new IntPtr(handleValue);
            return true;
        }

        return false;
    }

    // UI Interaction handler methods
    private async Task<object> HandleClickAtCoordinates(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for click_at_coordinates", "Provide x and y coordinates: { x: 100, y: 200 }");

        if (!arguments.Value.TryGetProperty("x", out var xElement) || !arguments.Value.TryGetProperty("y", out var yElement))
            return CreateErrorResponse("Missing x or y coordinates", "Example: { x: 100, y: 200, button: 'Left', click_count: 1 }");

        int x = xElement.GetInt32();
        int y = yElement.GetInt32();
        
        var buttonStr = arguments.Value.TryGetProperty("button", out var buttonElement) ? buttonElement.GetString() : "Left";
        var clickCount = arguments.Value.TryGetProperty("click_count", out var clickCountElement) ? clickCountElement.GetInt32() : 1;
        
        var button = buttonStr?.ToLower() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        var success = await _uiInteractionManager.ClickAtCoordinatesAsync(x, y, button, clickCount);
        var clickType = clickCount > 1 ? "double-clicked" : "clicked";
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully {clickType} at coordinates ({x}, {y}) with {buttonStr} mouse button." :
                        $"✗ Failed to click at coordinates ({x}, {y}). Coordinates may be outside screen bounds or inaccessible."
                }
            }
        };
    }

    private async Task<object> HandleClickControl(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for click_control", "Provide window_handle and control_handle: { window_handle: '0x12345', control_handle: '0x67890' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("control_handle", out var controlHandleElement))
            return CreateErrorResponse("Missing window_handle or control_handle", "Example: { window_handle: '0x12345', control_handle: '0x67890', button: 'Left' }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle) ||
            !TryParseHandle(controlHandleElement.GetString(), out IntPtr controlHandle))
            return CreateErrorResponse("Invalid handle format", "Handles must be hexadecimal like '0x12345678'");

        var buttonStr = arguments.Value.TryGetProperty("button", out var buttonElement) ? buttonElement.GetString() : "Left";
        var button = buttonStr?.ToLower() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        var success = await _uiInteractionManager.ClickControlAsync(windowHandle, controlHandle, button);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully clicked control 0x{controlHandle.ToInt64():X8} in window 0x{windowHandle.ToInt64():X8} with {buttonStr} button." :
                        $"✗ Failed to click control. Control may be disabled, hidden, or handles are invalid."
                }
            }
        };
    }

    private async Task<object> HandleSendText(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for send_text", "Provide window_handle and text: { window_handle: '0x12345', text: 'Hello World' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("text", out var textElement))
            return CreateErrorResponse("Missing window_handle or text", "Example: { window_handle: '0x12345', text: 'Hello World', control_handle: '0x67890' }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle))
            return CreateErrorResponse("Invalid window handle", "Handle must be hexadecimal like '0x12345678'");

        var text = textElement.GetString() ?? "";
        IntPtr? controlHandle = null;
        
        if (arguments.Value.TryGetProperty("control_handle", out var controlHandleElement) && 
            TryParseHandle(controlHandleElement.GetString(), out IntPtr parsedControlHandle))
        {
            controlHandle = parsedControlHandle;
        }

        var success = await _uiInteractionManager.SendTextAsync(windowHandle, text, controlHandle);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully sent text '{text}' to window 0x{windowHandle.ToInt64():X8}{(controlHandle.HasValue ? $" control 0x{controlHandle.Value.ToInt64():X8}" : "")}." :
                        $"✗ Failed to send text. Window may not have focus or text input is not supported."
                }
            }
        };
    }

    private async Task<object> HandleSendKeys(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for send_keys", "Provide window_handle and keys: { window_handle: '0x12345', keys: 'Ctrl+C' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("keys", out var keysElement))
            return CreateErrorResponse("Missing window_handle or keys", "Example: { window_handle: '0x12345', keys: 'Ctrl+C' }\n\nCommon keys: 'Enter', 'Tab', 'Escape', 'Ctrl+C', 'Alt+F4'");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle))
            return CreateErrorResponse("Invalid window handle", "Handle must be hexadecimal like '0x12345678'");

        var keys = keysElement.GetString() ?? "";
        IntPtr? controlHandle = null;
        
        if (arguments.Value.TryGetProperty("control_handle", out var controlHandleElement) && 
            TryParseHandle(controlHandleElement.GetString(), out IntPtr parsedControlHandle))
        {
            controlHandle = parsedControlHandle;
        }

        var success = await _uiInteractionManager.SendKeysAsync(windowHandle, keys, controlHandle);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully sent key combination '{keys}' to window 0x{windowHandle.ToInt64():X8}{(controlHandle.HasValue ? $" control 0x{controlHandle.Value.ToInt64():X8}" : "")}." :
                        $"✗ Failed to send keys '{keys}'. Window may not have focus or key combination is invalid."
                }
            }
        };
    }

    private async Task<object> HandleGetControlText(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for get_control_text", "Provide window_handle and control_handle: { window_handle: '0x12345', control_handle: '0x67890' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("control_handle", out var controlHandleElement))
            return CreateErrorResponse("Missing window_handle or control_handle", "Example: { window_handle: '0x12345', control_handle: '0x67890' }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle) ||
            !TryParseHandle(controlHandleElement.GetString(), out IntPtr controlHandle))
            return CreateErrorResponse("Invalid handle format", "Handles must be hexadecimal like '0x12345678'");

        var text = await _uiInteractionManager.GetControlTextAsync(windowHandle, controlHandle);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Text from control 0x{controlHandle.ToInt64():X8}:\n\n'{text}'\n\nLength: {text.Length} characters"
                }
            }
        };
    }

    private async Task<object> HandleSetControlText(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for set_control_text", "Provide window_handle, control_handle, and text: { window_handle: '0x12345', control_handle: '0x67890', text: 'New text' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("control_handle", out var controlHandleElement) ||
            !arguments.Value.TryGetProperty("text", out var textElement))
            return CreateErrorResponse("Missing required parameters", "Example: { window_handle: '0x12345', control_handle: '0x67890', text: 'New text' }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle) ||
            !TryParseHandle(controlHandleElement.GetString(), out IntPtr controlHandle))
            return CreateErrorResponse("Invalid handle format", "Handles must be hexadecimal like '0x12345678'");

        var text = textElement.GetString() ?? "";
        var success = await _uiInteractionManager.SetControlTextAsync(windowHandle, controlHandle, text);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully set text in control 0x{controlHandle.ToInt64():X8} to: '{text}'" :
                        $"✗ Failed to set text in control. Control may be read-only, disabled, or handles are invalid."
                }
            }
        };
    }

    private async Task<object> HandleSelectText(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for select_text", "Provide window_handle and control_handle: { window_handle: '0x12345', control_handle: '0x67890' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("control_handle", out var controlHandleElement))
            return CreateErrorResponse("Missing window_handle or control_handle", "Example: { window_handle: '0x12345', control_handle: '0x67890', start_index: 0, length: -1 }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle) ||
            !TryParseHandle(controlHandleElement.GetString(), out IntPtr controlHandle))
            return CreateErrorResponse("Invalid handle format", "Handles must be hexadecimal like '0x12345678'");

        var startIndex = arguments.Value.TryGetProperty("start_index", out var startElement) ? startElement.GetInt32() : 0;
        var length = arguments.Value.TryGetProperty("length", out var lengthElement) ? lengthElement.GetInt32() : -1;

        var success = await _uiInteractionManager.SelectTextAsync(windowHandle, controlHandle, startIndex, length);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully selected text in control 0x{controlHandle.ToInt64():X8} (start: {startIndex}, length: {(length == -1 ? "all" : length.ToString())})." :
                        $"✗ Failed to select text. Control may not support text selection or parameters are invalid."
                }
            }
        };
    }

    private async Task<object> HandleFindElementsByText(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for find_elements_by_text", "Provide window_handle and text: { window_handle: '0x12345', text: 'Search text' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("text", out var textElement))
            return CreateErrorResponse("Missing window_handle or text", "Example: { window_handle: '0x12345', text: 'OK', exact_match: false }");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle))
            return CreateErrorResponse("Invalid window handle", "Handle must be hexadecimal like '0x12345678'");

        var searchText = textElement.GetString() ?? "";
        var exactMatch = arguments.Value.TryGetProperty("exact_match", out var exactElement) && exactElement.GetBoolean();

        var elements = await _uiInteractionManager.FindElementsByTextAsync(windowHandle, searchText, exactMatch);
        
        if (elements.Any())
        {
            var elementsText = string.Join("\n", elements.Select(e => 
                $"Handle: 0x{e.Handle.ToInt64():X8}, Type: {e.ElementType}, Text: '{e.Text}', Bounds: {e.Bounds}, Center: ({e.CenterPoint.X}, {e.CenterPoint.Y})"));

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Found {elements.Count()} UI elements containing '{searchText}':\n\n{elementsText}\n\nNext steps:\n- Use click_control with a handle to click an element\n- Use click_at_coordinates with center coordinates\n- Use get_control_text to read element text"
                    }
                }
            };
        }
        else
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"No UI elements found containing '{searchText}' in window 0x{windowHandle.ToInt64():X8}.\n\nTips:\n- Try partial matching with exact_match: false\n- Use enumerate_child_windows to see all available controls\n- Check if the window has focus with set_window_focus"
                    }
                }
            };
        }
    }

    private async Task<object> HandleTakeScreenshot(JsonElement? arguments)
    {
        IntPtr windowHandle = IntPtr.Zero;
        
        if (arguments.HasValue && arguments.Value.TryGetProperty("window_handle", out var windowHandleElement))
        {
            if (!TryParseHandle(windowHandleElement.GetString(), out windowHandle))
                return CreateErrorResponse("Invalid window handle", "Handle must be hexadecimal like '0x12345678' or omit for full screen");
        }

        var screenshotData = await _uiInteractionManager.TakeScreenshotAsync(windowHandle);
        
        if (screenshotData != null && screenshotData.Length > 0)
        {
            var base64Data = Convert.ToBase64String(screenshotData);
            var target = windowHandle == IntPtr.Zero ? "full screen" : $"window 0x{windowHandle.ToInt64():X8}";
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"✓ Successfully captured screenshot of {target}.\n\nScreenshot size: {screenshotData.Length:N0} bytes\nFormat: PNG\nEncoding: Base64\n\nScreenshot data:\n{base64Data.Substring(0, Math.Min(100, base64Data.Length))}..."
                    }
                }
            };
        }
        else
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "✗ Failed to capture screenshot. Window may be minimized, hidden, or screenshot functionality is not available."
                    }
                }
            };
        }
    }

    private async Task<object> HandleScrollWindow(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for scroll_window", "Provide window_handle and direction: { window_handle: '0x12345', direction: 'Down' }");

        if (!arguments.Value.TryGetProperty("window_handle", out var windowHandleElement) ||
            !arguments.Value.TryGetProperty("direction", out var directionElement))
            return CreateErrorResponse("Missing window_handle or direction", "Example: { window_handle: '0x12345', direction: 'Down', amount: 3 }\n\nDirections: 'Up', 'Down', 'Left', 'Right'");

        if (!TryParseHandle(windowHandleElement.GetString(), out IntPtr windowHandle))
            return CreateErrorResponse("Invalid window handle", "Handle must be hexadecimal like '0x12345678'");

        var directionStr = directionElement.GetString() ?? "";
        var direction = directionStr.ToLower() switch
        {
            "up" => ScrollDirection.Up,
            "down" => ScrollDirection.Down,
            "left" => ScrollDirection.Left,
            "right" => ScrollDirection.Right,
            _ => ScrollDirection.Down
        };

        var amount = arguments.Value.TryGetProperty("amount", out var amountElement) ? amountElement.GetInt32() : 3;
        IntPtr? controlHandle = null;
        
        if (arguments.Value.TryGetProperty("control_handle", out var controlHandleElement) && 
            TryParseHandle(controlHandleElement.GetString(), out IntPtr parsedControlHandle))
        {
            controlHandle = parsedControlHandle;
        }

        var success = await _uiInteractionManager.ScrollAsync(windowHandle, direction, amount, controlHandle);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully scrolled {directionStr.ToLower()} in window 0x{windowHandle.ToInt64():X8} ({amount} steps){(controlHandle.HasValue ? $" control 0x{controlHandle.Value.ToInt64():X8}" : "")}." :
                        $"✗ Failed to scroll {directionStr.ToLower()}. Window may not support scrolling or is not focused."
                }
            }
        };
    }

    private async Task<object> HandleGetCursorPosition(JsonElement? arguments)
    {
        try
        {
            var position = await _uiInteractionManager.GetCursorPositionAsync();
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Current cursor position: ({position.X}, {position.Y})\n\nThis is the current mouse cursor location on screen.\n\nNext steps:\n- Use click_at_coordinates to click at this position\n- Use move_cursor to move cursor to different coordinates"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cursor position");
            return CreateErrorResponse("Failed to get cursor position", "Call without parameters: get_cursor_position");
        }
    }

    private async Task<object> HandleMoveCursor(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for move_cursor", "Provide x and y coordinates: { x: 100, y: 200 }");

        if (!arguments.Value.TryGetProperty("x", out var xElement) || !arguments.Value.TryGetProperty("y", out var yElement))
            return CreateErrorResponse("Missing x or y coordinates", "Example: { x: 100, y: 200 }");

        int x = xElement.GetInt32();
        int y = yElement.GetInt32();

        var success = await _uiInteractionManager.MoveCursorAsync(x, y);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully moved cursor to ({x}, {y}).\n\nThe mouse cursor is now positioned at the specified coordinates.\n\nNext steps:\n- Use click_at_coordinates to click at current position\n- Use get_cursor_position to confirm cursor location" :
                        $"✗ Failed to move cursor to ({x}, {y}). Coordinates may be outside screen bounds."
                }
            }
        };
    }

    private async Task<object> HandleDragFromTo(JsonElement? arguments)
    {
        if (!arguments.HasValue)
            return CreateErrorResponse("Missing arguments for drag_from_to", "Provide start and end coordinates: { start_x: 100, start_y: 100, end_x: 200, end_y: 200 }");

        if (!arguments.Value.TryGetProperty("start_x", out var startXElement) ||
            !arguments.Value.TryGetProperty("start_y", out var startYElement) ||
            !arguments.Value.TryGetProperty("end_x", out var endXElement) ||
            !arguments.Value.TryGetProperty("end_y", out var endYElement))
            return CreateErrorResponse("Missing coordinates", "Example: { start_x: 100, start_y: 100, end_x: 200, end_y: 200, button: 'Left' }");

        int startX = startXElement.GetInt32();
        int startY = startYElement.GetInt32();
        int endX = endXElement.GetInt32();
        int endY = endYElement.GetInt32();
        
        var buttonStr = arguments.Value.TryGetProperty("button", out var buttonElement) ? buttonElement.GetString() : "Left";
        var button = buttonStr?.ToLower() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        var success = await _uiInteractionManager.DragAsync(startX, startY, endX, endY, button);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = success ?
                        $"✓ Successfully dragged from ({startX}, {startY}) to ({endX}, {endY}) with {buttonStr} button.\n\nDrag operation completed. This can be used for moving objects, selecting text, or interacting with sliders and other controls." :
                        $"✗ Failed to drag from ({startX}, {startY}) to ({endX}, {endY}). Coordinates may be outside screen bounds or operation failed."
                }
            }
        };
    }

    // Helper method to extract result summary for activity tracking
    private static string GetResultSummary(object result)
    {
        try
        {
            if (result == null) return "No result";
            
            // Check if it's our standard response format
            var resultType = result.GetType();
            var contentProperty = resultType.GetProperty("content");
            
            if (contentProperty?.GetValue(result) is Array contentArray && contentArray.Length > 0)
            {
                var firstContent = contentArray.GetValue(0);
                if (firstContent != null)
                {
                    var textProperty = firstContent.GetType().GetProperty("text");
                    if (textProperty?.GetValue(firstContent) is string text)
                    {
                        // Truncate long responses for activity display
                        return text.Length > 100 ? text.Substring(0, 100) + "..." : text;
                    }
                }
            }
            
            return result.ToString() ?? "Unknown result";
        }
        catch
        {
            return "Result processing error";
        }
    }

    // Helper method to create consistent error responses
    private static object CreateErrorResponse(string error, string example)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Error: {error}\n\nUsage: {example}"
                }
            },
            isError = true
        };
    }
}
