using System.IO.Pipes;
using SrvShared;
using StreamJsonRpc;

namespace SrvWorker;

internal class Program
{
	static async Task Main(string[] args)
	{
		//System.Diagnostics.Debugger.Launch();
		await Console.Error.WriteLineAsync("Waiting for client to make a connection...");
		var stream = new NamedPipeServerStream(GlobalConstants.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
		await stream.WaitForConnectionAsync();
		await RespondToRpcRequestsAsync(stream);
	}

	private static async Task RespondToRpcRequestsAsync(Stream stream)
	{
		await Console.Error.WriteLineAsync("Connection request # received. Spinning off an async Task to cater to requests.");
		var jsonRpc = JsonRpc.Attach(stream, new Worker());
		await Console.Error.WriteLineAsync("JSON-RPC listener attached to . Waiting for requests...");
		await jsonRpc.Completion;
		await Console.Error.WriteLineAsync($"Connection  terminated.");
	}
}
