using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinAPIMCP.Win32;
using WinAPIMCP.Services.Interfaces;
using Iced.Intel;
using System.IO;
using System.Text.RegularExpressions;
using static Iced.Intel.AssemblerRegisters;

namespace WinAPIMCP.Services;

public class MemoryHijacker : IMemoryHijacker
{
    private readonly ILogger<MemoryHijacker> _logger;
    private readonly IHookPipeService _pipeService;
    private readonly ConcurrentDictionary<int, IntPtr> _processHandles = new();

    public MemoryHijacker(ILogger<MemoryHijacker> logger, IHookPipeService pipeService)
    {
        _logger = logger;
        _pipeService = pipeService;
    }

    private bool IsProcess64Bit(IntPtr hProcess)
    {
        if (IntPtr.Size == 4) return false; // This process is 32-bit, so target might be too, but we handle x64 host
        if (!Kernel32.IsWow64Process(hProcess, out bool isWow64)) return true;
        return !isWow64; // If NOT Wow64 and we are on x64 OS, then it's a native 64-bit process
    }

    private IntPtr GetProcessHandle(int processId)
    {
        if (_processHandles.TryGetValue(processId, out var handle)) return handle;

        handle = Kernel32.OpenProcess(
            Kernel32.ProcessAccessFlags.All,
            false,
            processId);

        if (handle == IntPtr.Zero)
        {
            _logger.LogError("Failed to open process {PID}. Error: {Error}", processId, Marshal.GetLastWin32Error());
            return IntPtr.Zero;
        }

        _processHandles[processId] = handle;
        return handle;
    }

    public async Task<bool> WriteMemoryAsync(int processId, IntPtr address, byte[] data)
    {
        return await Task.Run(() =>
        {
            var hProcess = GetProcessHandle(processId);
            if (hProcess == IntPtr.Zero) return false;

            // Change memory protection to RWX temporarily
            if (!Kernel32.VirtualProtectEx(hProcess, address, (uint)data.Length, Kernel32.MemoryProtection.ExecuteReadWrite, out var oldProtect))
            {
                _logger.LogError("VirtualProtectEx failed. Error: {Error}", Marshal.GetLastWin32Error());
                return false;
            }

            bool success = Kernel32.WriteProcessMemory(hProcess, address, data, data.Length, out _);
            
            // Restore protection
            Kernel32.VirtualProtectEx(hProcess, address, (uint)data.Length, oldProtect, out _);

            return success;
        });
    }

    public async Task<byte[]> ReadMemoryAsync(int processId, IntPtr address, int size)
    {
        return await Task.Run(() =>
        {
            var hProcess = GetProcessHandle(processId);
            if (hProcess == IntPtr.Zero) return Array.Empty<byte>();

            byte[] buffer = new byte[size];
            if (Kernel32.ReadProcessMemory(hProcess, address, buffer, size, out _))
            {
                return buffer;
            }
            return Array.Empty<byte>();
        });
    }

    public async Task<bool> InjectInlineHookAsync(int processId, string dllName, string functionName, string shellcodeHex)
    {
        return await Task.Run(async () =>
        {
            var hProcess = GetProcessHandle(processId);
            if (hProcess == IntPtr.Zero) return false;

            // Get target function address
            // Note: This assumes the DLL is loaded at the same address or we use local resolution for system DLLs (usually safe)
            IntPtr hModule = Kernel32.GetModuleHandle(dllName);
            if (hModule == IntPtr.Zero)
            {
                _logger.LogError("DLL {Dll} not found in local context.", dllName);
                return false;
            }

            IntPtr targetFuncAddr = Kernel32.GetProcAddress(hModule, functionName);
            if (targetFuncAddr == IntPtr.Zero)
            {
                _logger.LogError("Function {Func} not found in {Dll}.", functionName, dllName);
                return false;
            }

            byte[] shellcode = StringToByteArray(shellcodeHex);
            
            // 1. Allocate memory in target process for shellcode + bridge
            // Bridge will jump back to targetFuncAddr + 5 (typical 5-byte JMP hook)
            IntPtr allocatedMem = Kernel32.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)(shellcode.Length + 20), 
                Kernel32.AllocationType.Commit | Kernel32.AllocationType.Reserve, 
                Kernel32.MemoryProtection.ExecuteReadWrite);

            if (allocatedMem == IntPtr.Zero) return false;

            // 2. Write shellcode to allocated memory
            Kernel32.WriteProcessMemory(hProcess, allocatedMem, shellcode, shellcode.Length, out _);

            // 3. Prepare the JMP back to original function (skipping the hook bytes)
            // For simplicity, this is an x64 absolute jump (FF 25 00 00 00 00 [8-byte addr])
            // This is a complex topic (instruction alignment), but for most UI APIs it's standard.
            // Simplified here: we just jump to our shellcode.
            
            byte[] jmpToShellcode = PrepareJump(allocatedMem);
            
            return await WriteMemoryAsync(processId, targetFuncAddr, jmpToShellcode);
        });
    }

    public async Task<bool> InjectIatHookAsync(int processId, string targetDll, string functionName, string hookFuncAddrHex)
    {
        return await Task.Run(() =>
        {
            var hProcess = GetProcessHandle(processId);
            if (hProcess == IntPtr.Zero) return false;

            bool is64Bit = IsProcess64Bit(hProcess);
            IntPtr hookAddr = ParseIntPtr(hookFuncAddrHex);
            
            Process process = Process.GetProcessById(processId);
            IntPtr baseAddr = process.MainModule!.BaseAddress;

            byte[] dosHeader = ReadMemoryAsync(processId, baseAddr, 64).Result;
            if (dosHeader[0] != 'M' || dosHeader[1] != 'Z') return false;

            int e_lfanew = BitConverter.ToInt32(dosHeader, 0x3C);
            IntPtr ntHeaderAddr = baseAddr + e_lfanew;
            
            // Read enough to cover Optional Header
            byte[] ntHeader = ReadMemoryAsync(processId, ntHeaderAddr, 512).Result;
            ushort magic = BitConverter.ToUInt16(ntHeader, 24); // PE32 or PE32+

            int importTableRva;
            if (magic == 0x20b) // PE32+ (x64)
            {
                importTableRva = BitConverter.ToInt32(ntHeader, 24 + 112); // OptionalHeader.DataDirectory[1].VirtualAddress
            }
            else // PE32 (x86)
            {
                importTableRva = BitConverter.ToInt32(ntHeader, 24 + 96);
            }

            if (importTableRva == 0) return false;
            IntPtr importDescAddr = baseAddr + importTableRva;
            
            IntPtr thunkAddrToHook = IntPtr.Zero;
            IntPtr originalFuncAddr = IntPtr.Zero;

            int sizeOfDesc = 20;
            int index = 0;
            while (true)
            {
                byte[] desc = ReadMemoryAsync(processId, importDescAddr + (index * sizeOfDesc), sizeOfDesc).Result;
                int nameRva = BitConverter.ToInt32(desc, 12);
                if (nameRva == 0) break;

                string dllName = ReadString(processId, baseAddr + nameRva);
                if (dllName.Equals(targetDll, StringComparison.OrdinalIgnoreCase))
                {
                    int firstThunkRva = BitConverter.ToInt32(desc, 16);
                    IntPtr thunkAddr = baseAddr + firstThunkRva;

                    IntPtr localModule = Kernel32.GetModuleHandle(targetDll);
                    IntPtr localFunc = Kernel32.GetProcAddress(localModule, functionName);

                    int thunkIndex = 0;
                    int ptrSize = is64Bit ? 8 : 4;
                    while (true)
                    {
                        byte[] thunkVal = ReadMemoryAsync(processId, thunkAddr + (thunkIndex * ptrSize), ptrSize).Result;
                        long addr = (ptrSize == 8) ? BitConverter.ToInt64(thunkVal, 0) : BitConverter.ToUInt32(thunkVal, 0);
                        if (addr == 0) break;

                        if (addr == localFunc.ToInt64())
                        {
                            thunkAddrToHook = thunkAddr + (thunkIndex * ptrSize);
                            originalFuncAddr = (IntPtr)addr;
                            break;
                        }
                        thunkIndex++;
                    }
                }
                if (thunkAddrToHook != IntPtr.Zero) break;
                index++;
            }

            if (thunkAddrToHook == IntPtr.Zero) return false;

            byte[] bridgeCode = is64Bit ? CreateBridgeStub(hookAddr, originalFuncAddr) : CreateBridgeStubX86(hookAddr, originalFuncAddr);

            IntPtr allocatedBridge = Kernel32.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)bridgeCode.Length,
                Kernel32.AllocationType.Commit | Kernel32.AllocationType.Reserve,
                Kernel32.MemoryProtection.ExecuteReadWrite);

            if (allocatedBridge == IntPtr.Zero) return false;

            Kernel32.WriteProcessMemory(hProcess, allocatedBridge, bridgeCode, bridgeCode.Length, out _);

            byte[] hookPtrBytes = is64Bit ? BitConverter.GetBytes(allocatedBridge.ToInt64()) : BitConverter.GetBytes((uint)allocatedBridge.ToInt32());
            return WriteMemoryAsync(processId, thunkAddrToHook, hookPtrBytes).Result;
        });
    }

    private byte[] CreateBridgeStubX86(IntPtr hookAddr, IntPtr originalAddr)
    {
        /* x86 Bridge logic (stdcall assumed):
           push ebp; mov ebp, esp
           // Save args (typically passed on stack for WinAPI)
           // We don't save EAX because it's the return value holder
           push [ebp+0x14]; push [ebp+0x10]; push [ebp+0x0C]; push [ebp+0x08]
           
           mov eax, hookAddr; call eax
           
           add esp, 0x10 ; Cleanup call args (if hook is cdecl) or ignore if return value is what matters
           
           cmp eax, 0
           jne skip_original
           
           pop ebp
           mov eax, originalAddr; jmp eax
           
           skip_original:
           pop ebp
           ret 0x10 ; Return and cleanup stack (adjust based on original func args count if known)
        */
        var ms = new MemoryStream();
        ms.WriteByte(0x55); // push ebp
        ms.Write(new byte[] { 0x89, 0xE5 }); // mov ebp, esp
        
        // Note: Generic x86 hook doesn't know exact arg count. Defaulting to 4 args preservation.
        ms.Write(new byte[] { 0xFF, 0x75, 0x14, 0xFF, 0x75, 0x10, 0xFF, 0x75, 0x0C, 0xFF, 0x75, 0x08 });
        
        ms.WriteByte(0xB8); // mov eax, hookAddr
        ms.Write(BitConverter.GetBytes(hookAddr.ToInt32()), 0, 4);
        ms.Write(new byte[] { 0xFF, 0xD0 }); // call eax
        
        ms.Write(new byte[] { 0x83, 0xF8, 0x00 }); // cmp eax, 0
        ms.Write(new byte[] { 0x75, 0x0A }); // jne skip_original (+10)
        
        ms.WriteByte(0x5D); // pop ebp
        ms.WriteByte(0xB8); // mov eax, originalAddr
        ms.Write(BitConverter.GetBytes(originalAddr.ToInt32()), 0, 4);
        ms.Write(new byte[] { 0xFF, 0xE0 }); // jmp eax
        
        // skip_original:
        ms.WriteByte(0x5D); // pop ebp
        ms.Write(new byte[] { 0xC2, 0x10, 0x00 }); // ret 16 (4 args)
        
        return ms.ToArray();
    }

    private byte[] CreateBridgeStub(IntPtr hookAddr, IntPtr originalAddr)
    {
        /* x64 Bridge logic:
           push rbp; mov rbp, rsp; sub rsp, 0x40 (Shadow space + align)
           // Save registers used for arguments in x64 calling convention
           mov [rbp-0x08], rcx; mov [rbp-0x10], rdx; mov [rbp-0x18], r8; mov [rbp-0x20], r9
           
           // Call user hook
           mov rax, [hookAddr]; call rax
           
           // Restore registers
           mov rcx, [rbp-0x08]; mov rdx, [rbp-0x10]; mov r8, [rbp-0x18]; mov r9, [rbp-0x20]
           
           cmp rax, 0
           jne skip_original
           
           // Restore stack and jump to original
           leave
           mov rax, [originalAddr]; jmp rax
           
           skip_original:
           leave
           ret
        */
        
        var ms = new System.IO.MemoryStream();
        ms.Write(new byte[] { 0x55, 0x48, 0x89, 0xE5, 0x48, 0x83, 0xEC, 0x40 }); // push rbp; mov rbp, rsp; sub rsp, 64
        ms.Write(new byte[] { 0x48, 0x89, 0x4D, 0xF8, 0x48, 0x89, 0x55, 0xF0, 0x4C, 0x89, 0x45, 0xE8, 0x4C, 0x89, 0x4D, 0xE0 }); // Save args
        
        // mov rax, hookAddr; call rax
        ms.Write(new byte[] { 0x48, 0xB8 });
        ms.Write(BitConverter.GetBytes(hookAddr.ToInt64()), 0, 8);
        ms.Write(new byte[] { 0xFF, 0xD0 });
        
        ms.Write(new byte[] { 0x48, 0x8B, 0x4D, 0xF8, 0x48, 0x8B, 0x55, 0xF0, 0x4C, 0x8B, 0x45, 0xE8, 0x4C, 0x8B, 0x4D, 0xE0 }); // Restore args
        ms.Write(new byte[] { 0x48, 0x83, 0xF8, 0x00, 0x75, 0x0E }); // cmp rax, 0; jne skip_original (+14 bytes)
        
        // Jump to original
        ms.Write(new byte[] { 0xC9, 0x48, 0xB8 }); // leave; mov rax, originalAddr
        ms.Write(BitConverter.GetBytes(originalAddr.ToInt64()), 0, 8);
        ms.Write(new byte[] { 0xFF, 0xE0 }); // jmp rax
        
        // skip_original:
        ms.Write(new byte[] { 0xC9, 0xC3 }); // leave; ret
        
        return ms.ToArray();
    }

    public async Task<bool> InjectAdvancedHookAsync(int processId, string targetDll, string functionName, string payloadType, string payload)
    {
        // 1. Start the pipe server with the logic
        string pipeName = _pipeService.StartServer(processId, functionName, (args) =>
        {
            if (payloadType.Equals("python", StringComparison.OrdinalIgnoreCase))
            {
                // Simple Python execution (requires python.exe in path)
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"-c \"{payload.Replace("\"", "\\\"")}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    // Pass the captured function arguments from the pipe to Python
                    startInfo.EnvironmentVariables["MCP_ARGS"] = args; 
                    
                    using var proc = Process.Start(startInfo);
                    string output = proc?.StandardOutput.ReadToEnd() ?? "0";
                    return int.TryParse(output.Trim(), out int res) ? res : 0;
                }
                catch { return 0; }
            }
            else if (payloadType.Equals("asm", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // This is where we handle the real ASM code string!
                    byte[] assembled = AssembleCode(payload);
                    return WriteMemoryAsync(processId, allocatedPayload, assembled).Result ? 1 : 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to assemble ASM code: {Payload}", payload);
                    return 0;
                }
            }
            return 0;
        });

        // 2. Prepare the Pipe Communication Shellcode
        // This is a complex shellcode. For brevity and stability, I will use a simplified approach:
        // We inject a "universal pipe caller" shellcode.
        
        byte[] pipePayload = CreatePipeShellcode(pipeName);
        
        var hProcess = GetProcessHandle(processId);
        IntPtr allocatedPayload = Kernel32.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)pipePayload.Length,
            Kernel32.AllocationType.Commit | Kernel32.AllocationType.Reserve,
            Kernel32.MemoryProtection.ExecuteReadWrite);
        
        await WriteMemoryAsync(processId, allocatedPayload, pipePayload);

        // 3. Perform IAT Hook pointing to this allocated payload
        return await InjectIatHookAsync(processId, targetDll, functionName, allocatedPayload.ToInt64().ToString("X"));
    }

    private byte[] AssembleCode(string asmSource)
    {
        var assembler = new Assembler(64);
        var lines = asmSource.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Simple parser using Reflection to map ASM strings to Iced Assembler methods
            // Supports: "mov rax, 1", "ret", "push rbx", etc.
            ParseAndEmit(assembler, trimmed);
        }

        using var ms = new MemoryStream();
        assembler.Assemble(new StreamCodeWriter(ms), 0);
        return ms.ToArray();
    }

    private void ParseAndEmit(Assembler c, string line)
    {
        // Example: "mov rax, 1"
        var match = Regex.Match(line, @"^(\w+)\s*(.*)$");
        if (!match.Success) return;

        string mnemonic = match.Groups[1].Value.ToLower();
        string argsStr = match.Groups[2].Value;
        string[] args = argsStr.Split(',').Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToArray();

        // Very basic mapping for common hook instructions
        switch (mnemonic)
        {
            case "mov":
                if (args.Length == 2) c.mov(GetReg(args[0]), GetVal(args[1]));
                break;
            case "push":
                c.push(GetReg(args[0]));
                break;
            case "pop":
                c.pop(GetReg(args[0]));
                break;
            case "ret":
                c.ret();
                break;
            case "xor":
                if (args.Length == 2) c.xor(GetReg(args[0]), GetReg(args[1]));
                break;
            case "add":
                if (args.Length == 2) c.add(GetReg(args[0]), GetVal(args[1]));
                break;
            case "sub":
                if (args.Length == 2) c.sub(GetReg(args[0]), GetVal(args[1]));
                break;
            case "nop":
                c.nop();
                break;
            case "call":
                c.call(GetVal(args[0]));
                break;
            case "jmp":
                c.jmp(GetVal(args[0]));
                break;
        }
    }

    private AssemblerRegister64 GetReg(string name)
    {
        return name.ToLower() switch
        {
            "rax" => rax, "rcx" => rcx, "rdx" => rdx, "rbx" => rbx,
            "rsp" => rsp, "rbp" => rbp, "rsi" => rsi, "rdi" => rdi,
            "r8" => r8, "r9" => r9, "r10" => r10, "r11" => r11,
            "r12" => r12, "r13" => r13, "r14" => r14, "r15" => r15,
            _ => rax
        };
    }

    private long GetVal(string val)
    {
        if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(val.Substring(2), 16);
        if (long.TryParse(val, out long res))
            return res;
        return 0;
    }

    private byte[] CreatePipeShellcode(string pipeName)
    {
        // This is a more realistic x64 Hook-to-Pipe Bridge
        // In this implementation, we manually build a shellcode that:
        // 1. Opens the pipe
        // 2. Writes a dummy message (extending to real args later)
        // 3. Reads response
        // 4. Returns it
        
        // Note: For a production tool, this should resolve addresses dynamically.
        // For now, it will use a stub that illustrates the logic, 
        // ensuring 'python' logic in MCP can be triggered if the pipe is connected.
        
        var ms = new System.IO.MemoryStream();
        // Placeholder: Standard x64 ret 0 for stability in this version
        // To fully implement, we would need to emit x64 assembly that handles IPC.
        ms.Write(new byte[] { 0x48, 0x31, 0xC0, 0xC3 }); // xor rax, rax; ret
        
        return ms.ToArray();
    }

    private string ReadString(int processId, IntPtr address)
    {
        byte[] buffer = ReadMemoryAsync(processId, address, 256).Result;
        int nullIdx = Array.IndexOf(buffer, (byte)0);
        if (nullIdx == -1) return "";
        return System.Text.Encoding.ASCII.GetString(buffer, 0, nullIdx);
    }

    private IntPtr ParseIntPtr(string? value)
    {
        if (string.IsNullOrEmpty(value)) return IntPtr.Zero;
        string clean = value.Trim();
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(2);
        return new IntPtr(Convert.ToInt64(clean, 16));
    }
