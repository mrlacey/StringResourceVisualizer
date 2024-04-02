namespace SrvWorker;

public class Worker
{
	public static bool dataLoaded = false;

	public bool LoadData(string rootPath)
	{
		// Log to STDERR so as to not corrupt the STDOUT pipe that we may be using for JSON-RPC.
		Console.Error.WriteLine($"Start Loading data for {rootPath}");

		Thread.Sleep(5000);

		Console.Error.WriteLine($"END Loading data for {rootPath}");

		dataLoaded = true;
		return dataLoaded;
	}

	public string GetValue(string source)
	{
		if (dataLoaded)
		{
			return $"Source: {DateTime.Now.ToLongTimeString()}";
		}
		else
		{
			return string.Empty;
		}
	}
}