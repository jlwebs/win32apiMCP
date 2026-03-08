# Win32 API MCP Server

基于 C# 编写的 Model Context Protocol (MCP) 服务器，提供 Windows 环境下的桌面自动化底层支持，通过 `StdioMcpServer` 直接调用 `user32.dll` 等系统原生 API。

## 客户端配置

将以下配置添加至 MCP 客户端（如 Claude Desktop 或 Cursor），配置文件中。
注意：根据实际项目路径修改 `--project` 参数。

```json
{
  "mcpServers": {
    "winapi-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\GitHub\\127.0.0.1-84\\WinapiMCP-main\\src\\WinAPIMCP.csproj",
        "--",
        "--stdio"
      ]
    }
  }
}
```

*配置中必须包含 `--stdio` 参数。*

## 功能列表 (Tools)
共计 29 种系统级自动化 API 工具。
> [!TIP]
> **[查看工具详细说明文档与用法用例](./docs/TOOLS_GUIDE.md)**

### 窗口管理 (Window Control)
1. `enumerate_windows`: 枚举所有桌面可见窗口的句柄、类名和位置。
2. `enumerate_child_windows`: 获取指定窗口内部的所有子窗口和控件。
3. `get_window_info`: 获取指定窗口或控件的详细状态信息。
4. `find_windows_by_title`: 根据标题（支持正则表达式）查找特定窗口。
5. `find_windows_by_class`: 根据类名 (Class Name) 查找特定窗口。
6. `set_window_focus`: 强制将目标窗口置顶并赋予焦点。
7. `show_window`: 改变窗口状态（最小化、最大化、常规、隐藏）。
8. `close_window`: 优雅强制向受支持句柄发送 `WM_CLOSE` (16) 退出指令。

### 内存劫持与进程管理 (Memory Hijacking & Process)
9. `read_memory`: 读取目标进程的原始内存数据。
10. `write_memory`: 向目标进程写入原始内存数据（自动处理 VirtualProtect 权限）。
11. `inject_iat_hook`: 通过修改 IAT（导入地址表）劫持目标进程的 WinAPI 调用。
12. `advanced_hook`: 高级劫持工具，支持通过命名管道（Named Pipe）回调 Python 脚本或 ASM 逻辑，实现有条件拦截。
13. `inject_hook`: 标准 x64 Inline Hook（指令级劫持），用于更复杂的底层操作。
14. `enumerate_processes`: 枚举系统内正在运行的所有进程。
15. `get_process_info`: 获取指定 PID 的内存及路径信息。
16. `find_processes_by_name`: 根据进程名查找目标进程。

### 鼠标与屏幕控制 (Mouse & Cursor)
17. `move_cursor`: 瞬间移动鼠标光标至指定物理坐标。
18. `drag_from_to`: 按住指定按键执行坐标间的拖拽操作。
19. `scroll_window`: 在指定窗口内模拟鼠标滚轮滚动。
20. `get_cursor_position`: 获取当前屏幕的鼠标坐标。
21. `click_at_coordinates`: 前台直接在指定坐标执行点击。

### 控件交互与模拟键鼠 (UI Interaction)
22. `click_control`: (后台) 解析并向目标控件发送鼠标点击事件。
23. `send_text`: (后台) 向指定控件逐字发送键盘字符输入。
24. `send_keys`: (前台) 模拟物理键盘组合键（如 `Ctrl+C`, `Enter`）。
25. `get_control_text`: 提取指定 UI 控件内的纯文本（WM_GETTEXT）。
26. `set_control_text`: 覆盖、替换目标控件内部文本。
27. `find_elements_by_text`: 检索子树中包含指定文本的特定控件。

### 底层消息操控 (Win32 Messages)
28. `send_message`: 同步阻塞执行底层的 `SendMessage`，支持 Msg/wParam/lParam 深度定制。
29. `post_message`: 异步执行底层的 `PostMessage`，不论对方窗口是否响应，强行压入目标消息队列。
