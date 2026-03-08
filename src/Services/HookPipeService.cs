using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WinAPIMCP.Services;

public interface IHookPipeService
{
    string StartServer(int processId, string functionName, Func<string, int> logicHandler);
    void StopServer(string pipeName);
}

public class HookPipeService : IHookPipeService
{
    private readonly ILogger<HookPipeService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _servers = new();

    public HookPipeService(ILogger<HookPipeService> logger)
    {
        _logger = logger;
    }

    public string StartServer(int processId, string functionName, Func<string, int> logicHandler)
    {
        string pipeName = $"WinapiMCP_Hook_{processId}_{functionName}";
        var cts = new CancellationTokenSource();
        _servers[pipeName] = cts;

        Task.Run(() => RunServerAsync(pipeName, logicHandler, cts.Token));

        return pipeName;
    }

    private async Task RunServerAsync(string pipeName, Func<string, int> logicHandler, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);

                _logger.LogInformation("Pipe {Pipe} connected!", pipeName);

                using var reader = new BinaryReader(server);
                using var writer = new BinaryWriter(server);

                // Protocol: 
                // Target sends: [4 bytes size][string data/args]
                // Server sends: [4 bytes return_value (0 or 1)]

                int size = reader.ReadInt32();
                byte[] buffer = reader.ReadBytes(size);
                string args = Encoding.UTF8.GetString(buffer);

                _logger.LogDebug("Received hook data from pipe: {Data}", args);

                int result = logicHandler(args);

                writer.Write(result);
                server.Flush();
                server.WaitForPipeDrain();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, "Error in pipe server {Pipe}", pipeName);
                await Task.Delay(1000); // Prevent tight loop on error
            }
        }
    }

    public void StopServer(string pipeName)
    {
        if (_servers.TryRemove(pipeName, out var cts))
        {
            cts.Cancel();
        }
    }
}
