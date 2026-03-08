using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using WinAPIMCP.Services;
using WinAPIMCP.Models;
using Serilog;
using System.Text.Json.Serialization;

namespace WinAPIMCP.MCP;

public class IntPtrConverter : JsonConverter<IntPtr>
{
    public override IntPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrEmpty(str)) return IntPtr.Zero;
        return new IntPtr(Convert.ToInt64(str.Replace("0x", ""), 16));
    }

    public override void Write(Utf8JsonWriter writer, IntPtr value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"0x{value.ToInt64():X8}");
    }
}

public class StdioMcpServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowManager _windowManager;
    private readonly IProcessManager _processManager;
    private readonly IUIInteractionManager _uiInteractionManager;
    private readonly Services.Interfaces.IMemoryHijacker _memoryHijacker;

    public StdioMcpServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _windowManager = serviceProvider.GetRequiredService<IWindowManager>();
        _processManager = serviceProvider.GetRequiredService<IProcessManager>();
        _uiInteractionManager = serviceProvider.GetRequiredService<IUIInteractionManager>();
        _memoryHijacker = serviceProvider.GetRequiredService<Services.Interfaces.IMemoryHijacker>();
    }

    public async Task RunAsync()
    {
        Log.Information("STDIO MCP transport started");
        
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (true)
        {
            string? line;
            try {
                line = await reader.ReadLineAsync();
            } catch { break; }
            
            if (line == null) break;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var request = doc.RootElement;
                
                if (!request.TryGetProperty("method", out var methodProp)) continue;
                var method = methodProp.GetString();
                
                var id = request.TryGetProperty("id", out var idProp) ? (object)idProp.Clone() : null;

                object? result = null;
                object? error = null;

                try {
                    if (method == "initialize") {
                        result = new {
                            protocolVersion = "2024-11-05",
                            capabilities = new { tools = new { } },
                            serverInfo = new { name = "Windows API MCP Server", version = "1.0.0" }
                        };
                    } else if (method == "tools/list") {
                        result = ToolRegistry.GetTools();
                    } else if (method == "tools/call") {
                        result = await HandleToolCall(request.GetProperty("params"));
                    } else {
                        error = new { code = -32601, message = $"Method '{method}' not found" };
                    }
                } catch (Exception ex) {
                    error = new { code = -32000, message = ex.Message };
                }

                var responseObj = new Dictionary<string, object?> {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id
                };

                if (error != null) responseObj["error"] = error;
                else responseObj["result"] = result;

                await writer.WriteLineAsync(JsonSerializer.Serialize(responseObj));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing JSON-RPC line");
            }
        }
    }

    private async Task<object> HandleToolCall(JsonElement @params)
    {
        string toolName = @params.GetProperty("name").GetString()!;
        JsonElement arguments = @params.TryGetProperty("arguments", out var args) ? args : default;

        object toolResult = toolName switch
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
            "click_control" => await HandleClickControl(arguments),
            "click_at_coordinates" => await HandleClickAtCoordinates(arguments),
            "send_text" => await HandleSendText(arguments),
            "send_keys" => await HandleSendKeys(arguments),
            "get_control_text" => await HandleGetControlText(arguments),
            "set_control_text" => await HandleSetControlText(arguments),
            "get_cursor_position" => await HandleGetCursorPosition(arguments),
            "move_cursor" => await HandleMoveCursor(arguments),
            "drag_from_to" => await HandleDragFromTo(arguments),
            "scroll_window" => await HandleScrollWindow(arguments),
            "find_elements_by_text" => await HandleFindElementsByText(arguments),
            "close_window" => await HandleCloseWindow(arguments),
            "send_message" => await HandleSendMessage(arguments),
            "post_message" => await HandlePostMessage(arguments),
            "inject_hook" => await HandleInjectHook(arguments),
            "inject_iat_hook" => await HandleInjectIatHook(arguments),
            "advanced_hook" => await HandleAdvancedHook(arguments),
            "read_memory" => await HandleReadMemory(arguments),
            "write_memory" => await HandleWriteMemory(arguments),
            _ => throw new Exception($"Tool '{toolName}' is not supported")
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new IntPtrConverter());

        return new
        {
            content = new[]
            {
                new { type = "text", text = JsonSerializer.Serialize(toolResult, options) }
            }
        };
    }

    private async Task<object> HandleEnumerateWindows(JsonElement arguments)
    {
        var includeMinimized = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("include_minimized", out var prop1) && prop1.GetBoolean();
        var titleFilter = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("filter_by_title", out var prop2) ? prop2.GetString() : null;
        var windows = await _windowManager.EnumerateWindowsAsync(includeMinimized, titleFilter);
        return new { windows = windows.ToArray() };
    }

    private async Task<object> HandleEnumerateChildWindows(JsonElement arguments)
    {
        var parentHandle = ParseIntPtr(arguments.GetProperty("parent_handle").GetString());
        var includeAll = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("include_all_descendants", out var prop) && prop.GetBoolean();
        var children = await _windowManager.EnumerateChildWindowsAsync(parentHandle, includeAll);
        return new { children = children.ToArray() };
    }

    private async Task<object> HandleGetWindowInfo(JsonElement arguments)
    {
        var handle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var windowInfo = await _windowManager.GetWindowInfoAsync(handle);
        return new { window = windowInfo };
    }

    private async Task<object> HandleSetWindowFocus(JsonElement arguments)
    {
        var handle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var success = await _windowManager.SetWindowFocusAsync(handle);
        return new { success };
    }

    private async Task<object> HandleShowWindow(JsonElement arguments)
    {
        var handle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        
        int showStateNum = 1; // Default to Normal
        if (arguments.TryGetProperty("show_state", out var ss) || arguments.TryGetProperty("state", out ss))
        {
            if (ss.ValueKind == JsonValueKind.Number)
            {
                showStateNum = ss.GetInt32();
            }
            else if (ss.ValueKind == JsonValueKind.String)
            {
                var stateStr = ss.GetString();
                showStateNum = stateStr switch
                {
                    "Hidden" => 0,
                    "Normalized" or "Normal" => 1,
                    "Minimized" => 2,
                    "Maximized" => 3,
                    _ => 1
                };
            }
        }
        
        var success = await _windowManager.ShowWindowAsync(handle, showStateNum);
        return new { success };
    }

    private async Task<object> HandleFindWindowsByTitle(JsonElement arguments)
    {
        var titlePattern = arguments.GetProperty("title_pattern").GetString()!;
        var exactMatch = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("exact_match", out var prop) && prop.GetBoolean();
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
        var includeSystem = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("include_system", out var prop1) && prop1.GetBoolean();
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
        var exactMatch = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("exact_match", out var prop) && prop.GetBoolean();
        var processes = await _processManager.FindProcessesByNameAsync(namePattern, exactMatch);
        return new { processes = processes.ToArray() };
    }

    private async Task<object> HandleClickControl(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var controlHandle = ParseIntPtr(arguments.GetProperty("control_handle").GetString());
        var buttonStr = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("button", out var p) ? p.GetString() : "Left";
        var button = Enum.TryParse<MouseButton>(buttonStr, true, out var b) ? b : MouseButton.Left;
        var success = await _uiInteractionManager.ClickControlAsync(windowHandle, controlHandle, button);
        return new { success };
    }

    private async Task<object> HandleClickAtCoordinates(JsonElement arguments)
    {
        var x = arguments.GetProperty("x").GetInt32();
        var y = arguments.GetProperty("y").GetInt32();
        var clickCount = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("click_count", out var cc) ? cc.GetInt32() : 1;
        var buttonStr = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("button", out var p) ? p.GetString() : "Left";
        var button = Enum.TryParse<MouseButton>(buttonStr, true, out var b) ? b : MouseButton.Left;
        var success = await _uiInteractionManager.ClickAtCoordinatesAsync(x, y, button, clickCount);
        return new { success };
    }

    private async Task<object> HandleSendText(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var text = arguments.GetProperty("text").GetString()!;
        IntPtr? controlHandle = arguments.TryGetProperty("control_handle", out var ch) ? ParseIntPtr(ch.GetString()) : null;
        var success = await _uiInteractionManager.SendTextAsync(windowHandle, text, controlHandle);
        return new { success };
    }

    private async Task<object> HandleSendKeys(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var keys = arguments.GetProperty("keys").GetString()!;
        IntPtr? controlHandle = arguments.TryGetProperty("control_handle", out var ch) ? ParseIntPtr(ch.GetString()) : null;
        var success = await _uiInteractionManager.SendKeysAsync(windowHandle, keys, controlHandle);
        return new { success };
    }

    private async Task<object> HandleGetControlText(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var controlHandle = ParseIntPtr(arguments.GetProperty("control_handle").GetString());
        var text = await _uiInteractionManager.GetControlTextAsync(windowHandle, controlHandle);
        return new { text };
    }

    private async Task<object> HandleSetControlText(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var controlHandle = ParseIntPtr(arguments.GetProperty("control_handle").GetString());
        var text = arguments.GetProperty("text").GetString()!;
        var success = await _uiInteractionManager.SetControlTextAsync(windowHandle, controlHandle, text);
        return new { success };
    }

    private async Task<object> HandleGetCursorPosition(JsonElement arguments)
    {
        var point = await _uiInteractionManager.GetCursorPositionAsync();
        return new { x = point.X, y = point.Y };
    }

    private async Task<object> HandleMoveCursor(JsonElement arguments)
    {
        var x = arguments.GetProperty("x").GetInt32();
        var y = arguments.GetProperty("y").GetInt32();
        var success = await _uiInteractionManager.MoveCursorAsync(x, y);
        return new { success };
    }

    private async Task<object> HandleDragFromTo(JsonElement arguments)
    {
        var startX = arguments.GetProperty("start_x").GetInt32();
        var startY = arguments.GetProperty("start_y").GetInt32();
        var endX = arguments.GetProperty("end_x").GetInt32();
        var endY = arguments.GetProperty("end_y").GetInt32();
        var buttonStr = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("button", out var p) ? p.GetString() : "Left";
        var button = Enum.TryParse<MouseButton>(buttonStr, true, out var b) ? b : MouseButton.Left;
        var success = await _uiInteractionManager.DragAsync(startX, startY, endX, endY, button);
        return new { success };
    }

    private async Task<object> HandleScrollWindow(JsonElement arguments)
    {
        var windowHandle = new IntPtr(Convert.ToInt64(arguments.GetProperty("window_handle").GetString()!, 16));
        var directionStr = arguments.GetProperty("direction").GetString()!;
        var direction = Enum.TryParse<ScrollDirection>(directionStr, true, out var d) ? d : ScrollDirection.Down;
        var amount = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("amount", out var a) ? a.GetInt32() : 3;
        IntPtr? controlHandle = arguments.TryGetProperty("control_handle", out var ch) ? new IntPtr(Convert.ToInt64(ch.GetString()!, 16)) : null;
        var success = await _uiInteractionManager.ScrollAsync(windowHandle, direction, amount, controlHandle);
        return new { success };
    }

    private async Task<object> HandleFindElementsByText(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var text = arguments.GetProperty("text").GetString()!;
        var exactMatch = arguments.ValueKind != JsonValueKind.Undefined && arguments.TryGetProperty("exact_match", out var e) && e.GetBoolean();
        var elements = await _uiInteractionManager.FindElementsByTextAsync(windowHandle, text, exactMatch);
        return new { elements = elements.ToArray() };
    }

    private async Task<object> HandleCloseWindow(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var success = await _windowManager.CloseWindowAsync(windowHandle);
        return new { success };
    }

    private async Task<object> HandleSendMessage(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var msg = arguments.GetProperty("msg").GetUInt32();
        var wParam = ParseIntPtr(arguments.GetProperty("w_param").GetString());
        var lParam = ParseIntPtr(arguments.GetProperty("l_param").GetString());
        
        var result = await _uiInteractionManager.SendMessageAsync(windowHandle, msg, wParam, lParam);
        return new { result };
    }

    private async Task<object> HandlePostMessage(JsonElement arguments)
    {
        var windowHandle = ParseIntPtr(arguments.GetProperty("window_handle").GetString());
        var msg = arguments.GetProperty("msg").GetUInt32();
        var wParam = ParseIntPtr(arguments.GetProperty("w_param").GetString());
        var lParam = ParseIntPtr(arguments.GetProperty("l_param").GetString());
        
        var success = await _uiInteractionManager.PostMessageAsync(windowHandle, msg, wParam, lParam);
        return new { success };
    }

    private async Task<object> HandleInjectHook(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var dllName = arguments.GetProperty("dll_name").GetString()!;
        var functionName = arguments.GetProperty("function_name").GetString()!;
        var shellcodeHex = arguments.GetProperty("shellcode_hex").GetString()!;
        
        var success = await _memoryHijacker.InjectInlineHookAsync(processId, dllName, functionName, shellcodeHex);
        return new { success };
    }

    private async Task<object> HandleInjectIatHook(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var targetDll = arguments.GetProperty("target_dll").GetString()!;
        var functionName = arguments.GetProperty("function_name").GetString()!;
        var hookFuncAddr = arguments.GetProperty("hook_function_address").GetString()!;
        
        var success = await _memoryHijacker.InjectIatHookAsync(processId, targetDll, functionName, hookFuncAddr);
        return new { success };
    }

    private async Task<object> HandleAdvancedHook(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var targetDll = arguments.GetProperty("target_dll").GetString()!;
        var functionName = arguments.GetProperty("function_name").GetString()!;
        var payloadType = arguments.GetProperty("payload_type").GetString()!;
        var payload = arguments.GetProperty("payload").GetString()!;
        
        var success = await _memoryHijacker.InjectAdvancedHookAsync(processId, targetDll, functionName, payloadType, payload);
        return new { success };
    }

    private async Task<object> HandleReadMemory(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var address = ParseIntPtr(arguments.GetProperty("address").GetString());
        var size = arguments.GetProperty("size").GetInt32();
        
        var data = await _memoryHijacker.ReadMemoryAsync(processId, address, size);
        return new { hex = BitConverter.ToString(data).Replace("-", "") };
    }

    private async Task<object> HandleWriteMemory(JsonElement arguments)
    {
        var processId = arguments.GetProperty("process_id").GetInt32();
        var address = ParseIntPtr(arguments.GetProperty("address").GetString());
        var hexData = arguments.GetProperty("hex_data").GetString()!;
        
        var data = Enumerable.Range(0, hexData.Length / 2)
                             .Select(x => Convert.ToByte(hexData.Substring(x * 2, 2), 16))
                             .ToArray();
        
        var success = await _memoryHijacker.WriteMemoryAsync(processId, address, data);
        return new { success };
    }

    private IntPtr ParseIntPtr(string? value)
    {
        if (string.IsNullOrEmpty(value)) return IntPtr.Zero;
        string clean = value.Trim();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(2);
        }
        
        try
        {
            return new IntPtr(Convert.ToInt64(clean, 16));
        }
        catch
        {
            if (long.TryParse(clean, out long result))
                return new IntPtr(result);
            throw;
        }
    }
}
