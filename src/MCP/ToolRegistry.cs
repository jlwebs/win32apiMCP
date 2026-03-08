using System.Collections.Generic;

namespace WinAPIMCP.MCP;

/// <summary>
/// Tool registry with comprehensive documentation for agent understanding
/// </summary>
public static class ToolRegistry
{
    /// <summary>
    /// Get all tools with comprehensive documentation, examples, and usage patterns
    /// </summary>
    public static object GetTools()
    {
        return new
        {
            tools = new object[]
            {
                // Discovery Tools
                new
                {
                    name = "enumerate_windows",
                    description = "Lists all desktop windows currently visible on the screen. This is the primary discovery tool for understanding what applications and windows are available for interaction.",
                    category = "Discovery",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["include_minimized"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include minimized/hidden windows in results",
                                ["default"] = false
                            },
                            ["filter_by_title"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Optional regex pattern to filter windows by title"
                            }
                        }
                    },
                    typical_next_actions = new[]
                    {
                        "get_window_info - Get detailed information about a specific window",
                        "enumerate_child_windows - Explore controls within a window",
                        "set_window_focus - Bring a window to the foreground"
                    }
                },

                new
                {
                    name = "enumerate_child_windows",
                    description = "Discovers all child windows and controls within a parent window. Essential for UI automation.",
                    category = "UI_Discovery",
                    complexity = "Intermediate",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["parent_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Handle of the parent window",
                                ["format"] = "hexadecimal"
                            },
                            ["include_all_descendants"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include all descendant windows recursively",
                                ["default"] = true
                            }
                        },
                        required = new[] { "parent_handle" }
                    }
                },

                new
                {
                    name = "get_window_info",
                    description = "Retrieves comprehensive information about a specific window.",
                    category = "Information",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Window handle from enumerate_windows",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle" }
                    }
                },

                new
                {
                    name = "find_windows_by_title",
                    description = "Searches for windows matching a title pattern.",
                    category = "Discovery",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["title_pattern"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Pattern to match window titles"
                            },
                            ["exact_match"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether to match exactly or use partial matching",
                                ["default"] = false
                            }
                        },
                        required = new[] { "title_pattern" }
                    }
                },

                new
                {
                    name = "find_windows_by_class",
                    description = "Searches for windows matching a class name.",
                    category = "Discovery",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["class_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Window class name to search for"
                            }
                        },
                        required = new[] { "class_name" }
                    }
                },

                // Process Tools
                new
                {
                    name = "enumerate_processes",
                    description = "Lists all running processes on the system.",
                    category = "Discovery",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["include_system"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Include system processes",
                                ["default"] = false
                            },
                            ["filter_by_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Optional process name filter"
                            }
                        }
                    }
                },

                new
                {
                    name = "get_process_info",
                    description = "Gets detailed information about a specific process.",
                    category = "Information",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Process ID to query"
                            }
                        },
                        required = new[] { "process_id" }
                    }
                },

                new
                {
                    name = "find_processes_by_name",
                    description = "Searches for processes by name pattern.",
                    category = "Discovery",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["name_pattern"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Process name pattern to search for"
                            },
                            ["exact_match"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether to match exactly",
                                ["default"] = false
                            }
                        },
                        required = new[] { "name_pattern" }
                    }
                },

                // Window Control
                new
                {
                    name = "set_window_focus",
                    description = "Brings a window to the foreground and gives it focus.",
                    category = "Control",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Handle of window to focus",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle" }
                    }
                },

                new
                {
                    name = "show_window",
                    description = "Changes the window's display state (minimize, maximize, restore).",
                    category = "Control",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["format"] = "hexadecimal"
                            },
                            ["state"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "Normal", "Minimized", "Maximized", "Hidden" }
                            }
                        },
                        required = new[] { "window_handle", "state" }
                    }
                },

                // UI Interaction Tools
                new
                {
                    name = "click_at_coordinates",
                    description = "Clicks at specific screen coordinates. Use this for clicking on visible UI elements.",
                    category = "UI_Interaction",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["x"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate on screen"
                            },
                            ["y"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate on screen"
                            },
                            ["button"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "Left", "Right", "Middle" },
                                ["default"] = "Left"
                            },
                            ["click_count"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Number of clicks (1=single, 2=double)",
                                ["default"] = 1
                            }
                        },
                        required = new[] { "x", "y" }
                    }
                },

                new
                {
                    name = "click_control",
                    description = "Clicks on a specific window control using its handle.",
                    category = "UI_Interaction",
                    complexity = "Intermediate",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Parent window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Control handle to click",
                                ["format"] = "hexadecimal"
                            },
                            ["button"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "Left", "Right", "Middle" },
                                ["default"] = "Left"
                            }
                        },
                        required = new[] { "window_handle", "control_handle" }
                    }
                },

                new
                {
                    name = "send_text",
                    description = "Sends text input to a window or control. The target must have focus first.",
                    category = "UI_Interaction",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["text"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Text to send"
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Optional specific control handle",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "text" }
                    }
                },

                new
                {
                    name = "send_keys",
                    description = "Sends keyboard input like key combinations and special keys.",
                    category = "UI_Interaction",
                    complexity = "Intermediate",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["keys"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Key combination (e.g., 'Ctrl+C', 'Alt+F4', 'Enter', 'Tab')"
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Optional specific control handle",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "keys" }
                    }
                },

                // Text and Control Manipulation
                new
                {
                    name = "get_control_text",
                    description = "Gets the text content from a UI control.",
                    category = "UI_Information",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Parent window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Control handle",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "control_handle" }
                    }
                },

                new
                {
                    name = "set_control_text",
                    description = "Sets/replaces text content in a UI control (clears existing text first).",
                    category = "UI_Interaction",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Parent window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Control handle",
                                ["format"] = "hexadecimal"
                            },
                            ["text"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "New text content"
                            }
                        },
                        required = new[] { "window_handle", "control_handle", "text" }
                    }
                },

                new
                {
                    name = "find_elements_by_text",
                    description = "Finds UI elements containing specific text within a window.",
                    category = "UI_Discovery",
                    complexity = "Advanced",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Parent window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["text"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Text to search for"
                            },
                            ["exact_match"] = new Dictionary<string, object>
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether to match exactly or partial",
                                ["default"] = false
                            }
                        },
                        required = new[] { "window_handle", "text" }
                    }
                },

                // Screenshots and Visual
                new
                {
                    name = "take_screenshot",
                    description = "Takes a screenshot of a window or the entire screen for visual analysis.",
                    category = "UI_Information",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Window handle (omit for full screen)",
                                ["format"] = "hexadecimal"
                            }
                        }
                    }
                },

                // Mouse Operations
                new
                {
                    name = "get_cursor_position",
                    description = "Gets the current mouse cursor position on screen.",
                    category = "UI_Information",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>()
                    }
                },

                new
                {
                    name = "move_cursor",
                    description = "Moves the mouse cursor to specific screen coordinates.",
                    category = "UI_Interaction",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["x"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate on screen"
                            },
                            ["y"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate on screen"
                            }
                        },
                        required = new[] { "x", "y" }
                    }
                },

                new
                {
                    name = "drag_from_to",
                    description = "Drags from one screen coordinate to another. Useful for moving objects, selecting text, or slider controls.",
                    category = "UI_Interaction",
                    complexity = "Intermediate",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["start_x"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Starting X coordinate"
                            },
                            ["start_y"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Starting Y coordinate"
                            },
                            ["end_x"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Ending X coordinate"
                            },
                            ["end_y"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Ending Y coordinate"
                            },
                            ["button"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "Left", "Right", "Middle" },
                                ["default"] = "Left"
                            }
                        },
                        required = new[] { "start_x", "start_y", "end_x", "end_y" }
                    }
                },

                new
                {
                    name = "scroll_window",
                    description = "Scrolls within a window or control.",
                    category = "UI_Interaction",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["direction"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "Up", "Down", "Left", "Right" }
                            },
                            ["amount"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Number of scroll steps",
                                ["default"] = 3
                            },
                            ["control_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Optional specific control handle",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "direction" }
                    }
                },

                new
                {
                    name = "close_window",
                    description = "Closes a window by sending WM_CLOSE.",
                    category = "Control",
                    complexity = "Basic",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle" }
                    }
                },

                new
                {
                    name = "send_message",
                    description = "Sends a message directly to a window using SendMessage.",
                    category = "Advanced",
                    complexity = "Advanced",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["msg"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Message ID (e.g., 273 for WM_COMMAND)"
                            },
                            ["w_param"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "wParam value in hexadecimal",
                                ["format"] = "hexadecimal"
                            },
                            ["l_param"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "lParam value in hexadecimal",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "msg", "w_param", "l_param" }
                    }
                },

                new
                {
                    name = "post_message",
                    description = "Posts a message directly to a window using PostMessage.",
                    category = "Advanced",
                    complexity = "Advanced",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["window_handle"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Target window handle",
                                ["format"] = "hexadecimal"
                            },
                            ["msg"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Message ID (e.g., 273 for WM_COMMAND)"
                            },
                            ["w_param"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "wParam value in hexadecimal",
                                ["format"] = "hexadecimal"
                            },
                            ["l_param"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "lParam value in hexadecimal",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "window_handle", "msg", "w_param", "l_param" }
                    }
                },

                new
                {
                    name = "inject_hook",
                    description = "Intercepts a Windows API in a target process and injects custom code (x64 Inline Hooking).",
                    category = "System",
                    complexity = "Expert",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Target Process ID"
                            },
                            ["dll_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "DLL containing the function (e.g., user32.dll)"
                            },
                            ["function_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the function to intercept"
                            },
                            ["shellcode_hex"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Hex string of code to execute. Must end with JMP-back logic if not intending to crash."
                            }
                        },
                        required = new[] { "process_id", "dll_name", "function_name", "shellcode_hex" }
                    }
                },

                new
                {
                    name = "inject_iat_hook",
                    description = "Intercepts external DLL function calls (WinAPI) by modifying the Import Address Table of the target process.",
                    category = "System",
                    complexity = "Expert",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Target Process ID"
                            },
                            ["target_dll"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The DLL being called (e.g., user32.dll)"
                            },
                            ["function_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "The API function to intercept"
                            },
                            ["hook_function_address"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Memory address of your custom hook function (hex)",
                                ["format"] = "hexadecimal"
                            }
                        },
                        required = new[] { "process_id", "target_dll", "function_name", "hook_function_address" }
                    }
                },

                new
                {
                    name = "read_memory",
                    description = "Reads raw memory from a target process.",
                    category = "System",
                    complexity = "Advanced",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Target Process ID"
                            },
                            ["address"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Memory address in hexadecimal",
                                ["format"] = "hexadecimal"
                            },
                            ["size"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Number of bytes to read"
                            }
                        },
                        required = new[] { "process_id", "address", "size" }
                    }
                },

                new
                {
                    name = "advanced_hook",
                    description = "Injects an advanced conditional hook supporting Python logic or ASM.",
                    category = "System",
                    complexity = "Expert",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Target Process ID"
                            },
                            ["target_dll"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "DLL to hook (e.g., user32.dll)"
                            },
                            ["function_name"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Function name (e.g., MessageBoxW)"
                            },
                            ["payload_type"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[] { "python", "asm" },
                                ["description"] = "Type of logic payload"
                            },
                            ["payload"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Python script or ASM hex. Return 0 to continue, 1 to skip."
                            }
                        },
                        required = new[] { "process_id", "target_dll", "function_name", "payload_type", "payload" }
                    }
                },

                new
                {
                    name = "write_memory",
                    description = "Writes raw memory to a target process (Auto-handles VirtualProtect).",
                    category = "System",
                    complexity = "Advanced",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["process_id"] = new Dictionary<string, object>
                            {
                                ["type"] = "integer",
                                ["description"] = "Target Process ID"
                            },
                            ["address"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Memory address in hexadecimal",
                                ["format"] = "hexadecimal"
                            },
                            ["hex_data"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["description"] = "Data to write in hexadecimal string"
                            }
                        },
                        required = new[] { "process_id", "address", "hex_data" }
                    }
                }
            },

            workflows = new
            {
                basic_window_interaction = new
                {
                    description = "Standard workflow for interacting with a window",
                    steps = new[]
                    {
                        "1. enumerate_windows or find_windows_by_title - Discover target window",
                        "2. get_window_info - Verify window state",
                        "3. set_window_focus - Ensure window has focus",
                        "4. enumerate_child_windows - Discover controls"
                    }
                },
                app_automation = new
                {
                    description = "Complete UI automation workflow",
                    steps = new[]
                    {
                        "1. enumerate_processes - Check running applications",
                        "2. find_windows_by_title - Locate target window",
                        "3. show_window - Make window visible",
                        "4. set_window_focus - Give window focus",
                        "5. enumerate_child_windows - Map UI controls"
                    }
                }
            },

            agent_tips = new
            {
                discovery_strategy = "Always start with enumerate_windows to understand desktop state",
                error_handling = "Check responses for error messages and retry with different parameters",
                focus_management = "Always call set_window_focus before sending input"
            }
        };
    }

    /// <summary>
    /// Get predefined workflows for common automation tasks
    /// </summary>
    public static object GetWorkflows()
    {
        return new
        {
            workflows = new
            {
                basic_window_interaction = new
                {
                    name = "basic_window_interaction",
                    description = "Standard workflow for interacting with a window",
                    steps = new[]
                    {
                        "enumerate_windows",
                        "get_window_info",
                        "set_window_focus",
                        "enumerate_child_windows"
                    }
                },
                app_automation = new
                {
                    name = "app_automation",
                    description = "Complete application automation workflow",
                    steps = new[]
                    {
                        "enumerate_processes",
                        "find_windows_by_title",
                        "show_window",
                        "set_window_focus",
                        "enumerate_child_windows"
                    }
                }
            }
        };
    }
}