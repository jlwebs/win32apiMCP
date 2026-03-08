using System;
using System.Threading.Tasks;

namespace WinAPIMCP.Services.Interfaces;

public interface IMemoryHijacker
{
    /// <summary>
    /// Injects a hook into a target process API.
    /// </summary>
    /// <param name="processId">Process ID to target</param>
    /// <param name="dllName">Name of the DLL (e.g., user32.dll)</param>
    /// <param name="functionName">Name of the function to hook</param>
    /// <param name="shellcodeHex">Hex string of the custom code to execute</param>
    /// <returns>True if successful</returns>
    Task<bool> InjectInlineHookAsync(int processId, string dllName, string functionName, string shellcodeHex);

    /// <summary>
    /// Injects an IAT hook into a target process API.
    /// This intercepts calls from the target process to external DLL functions.
    /// </summary>
    Task<bool> InjectIatHookAsync(int processId, string targetDll, string functionName, string hookFuncAddrHex);

    /// <summary>
    /// Injects an advanced hook that calls back to the MCP server for logic (supports Python/ASM/etc.)
    /// </summary>
    Task<bool> InjectAdvancedHookAsync(int processId, string targetDll, string functionName, string payloadType, string payload);

    /// <summary>
    /// Writes bytes directly to a memory address in a target process.
    /// </summary>
    Task<bool> WriteMemoryAsync(int processId, IntPtr address, byte[] data);

    /// <summary>
    /// Reads bytes from a memory address in a target process.
    /// </summary>
    Task<byte[]> ReadMemoryAsync(int processId, IntPtr address, int size);
}
