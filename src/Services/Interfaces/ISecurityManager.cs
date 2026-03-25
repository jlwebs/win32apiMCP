namespace WinAPIMCP.Services;

/// <summary>
/// Interface for security-related operations and validation
/// </summary>
public interface ISecurityManager
{
    /// <summary>
    /// Validates that the current process has permission to access the specified window
    /// </summary>
    /// <param name="windowHandle">Handle of the window to validate access for</param>
    /// <returns>A task representing the asynchronous validation operation</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if access is not allowed</exception>
    Task ValidateWindowAccessAsync(IntPtr windowHandle);

    /// <summary>
    /// Validates that the current process has permission to access the specified process
    /// </summary>
    /// <param name="processId">ID of the process to validate access for</param>
    /// <exception cref="UnauthorizedAccessException">Thrown if access is not allowed</exception>
    void ValidateProcessAccess(int processId);

    /// <summary>
    /// Checks if elevated processes are allowed to be accessed
    /// </summary>
    /// <returns>True if elevated processes can be accessed</returns>
    bool IsElevatedAccessAllowed();

    /// <summary>
    /// Checks if a process is running with elevated privileges
    /// </summary>
    /// <param name="processId">Process ID to check</param>
    /// <returns>True if the process is elevated</returns>
    bool IsProcessElevated(int processId);

    /// <summary>
    /// Checks if a window belongs to an elevated process
    /// </summary>
    /// <param name="windowHandle">Window handle to check</param>
    /// <returns>True if the window belongs to an elevated process</returns>
    bool IsWindowFromElevatedProcess(IntPtr windowHandle);
}