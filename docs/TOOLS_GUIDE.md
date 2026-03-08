# WinapiMCP 工具详细使用指南 (Detailed Tools Guide)

本文档提供 WinapiMCP 所有工具的详细说明与实际调用示例，帮助您快速上手系统级自动化与内存劫持。

---

## 1. 窗口管理 (Window Control)

### `enumerate_windows`
- **描述**: 枚举所有桌面可见窗口的句柄、类名和位置。
- **示例**:
  ```json
  { "name": "enumerate_windows", "arguments": {} }
  ```

### `find_windows_by_title`
- **描述**: 根据标题（支持正则表达式）查找特定窗口。
- **示例 (查找记事本)**:
  ```json
  { "name": "find_windows_by_title", "arguments": { "title_pattern": ".*记事本.*" } }
  ```

---

## 2. 内存劫持与进程管理 (Memory & Hooking)

### `read_memory` / `write_memory`
- **描述**: 读取或写入目标进程内存。`write_memory` 会自动处理 `VirtualProtect` 权限切换。
- **示例 (写入 0x90 填充)**:
  ```json
  {
    "name": "write_memory",
    "arguments": {
      "process_id": 1234,
      "address": "0x7FF712345678",
      "data_hex": "90909090"
    }
  }
  ```

### `inject_iat_hook`
- **描述**: 修改 IAT（导入地址表）劫持 WinAPI 调用。
- **劫持逻辑**: 指定一个已存在的代码地址，Hook 返回 `0` 放行，返回 `1` 拦截。

### `advanced_hook` (核心推荐)
- **描述**: 高级劫持工具，支持 Python 逻辑回调或直接注入 ASM。
- **原理**: 
  - **Python 模式**: 注入通用中转 Shellcode，并建立命名管道回调 MCP 服务端运行 Python 脚本。
  - **ASM 模式**: 直接在目标地址注入您提供的十六进制机器码（Shellcode）。
- **Demo 1: 拦截所有 `MessageBoxW` 弹窗 (Python)**:
  ```json
  {
    "name": "advanced_hook",
    "arguments": {
      "process_id": 1564,
      "target_dll": "user32.dll",
      "function_name": "MessageBoxW",
      "payload_type": "python",
      "payload": "print(1)"
    }
  }
  ```
- **Demo 2: 基于逻辑判断是否拦截 (Python)**:
  *未来版本支持实时参数获取，当前版本可通过修改 Python 逻辑实现固定行为控制。*
  ```json
  {
    "name": "advanced_hook",
    "arguments": {
      "process_id": 1564,
      "target_dll": "user32.dll",
      "function_name": "CreateWindowExW",
      "payload_type": "python",
      "payload": "import datetime; print(1 if datetime.datetime.now().hour > 22 else 0)"
    }
  }
  ```
- **Demo 3: 强制拦截 (ASM 源码)**:
  直接输入汇编指令，MCP 将自动为您编译并注入。实现最纯粹的硬件级拦截。
  ```json
  {
    "name": "advanced_hook",
    "arguments": {
      "process_id": 1564,
      "target_dll": "user32.dll",
      "function_name": "MessageBoxW",
      "payload_type": "asm",
      "payload": "mov rax, 1; ret"
    }
  }
  ```

---

## 3. 鼠标与控制器工具 (Input & Interaction)

### `send_keys`
- **描述**: 模拟物理键盘输入（如组合键）。
- **示例 (保存文件)**:
  ```json
  {
    "name": "send_keys",
    "arguments": {
      "window_handle": "0x00010203",
      "keys": "Ctrl+S"
    }
  }
  ```

### `click_control`
- **描述**: 向特定控件句柄发送后台点击指令（不移动鼠标）。
- **示例**:
  ```json
  {
    "name": "click_control",
    "arguments": {
      "window_handle": "0x00010203",
      "control_handle": "0x00040506",
      "button": "Left"
    }
  }
  ```

---

## 4. 底层消息操控 (Win32 Messages)

### `send_message` / `post_message`
- **用法**: 直接调用底层的 `SendMessage` 或 `PostMessage`。
- **示例 (关闭窗口)**:
  ```json
  {
    "name": "post_message",
    "arguments": {
      "window_handle": "0x00010203",
      "msg": 16,
      "w_param": "0",
      "l_param": "0"
    }
  }
  ```
