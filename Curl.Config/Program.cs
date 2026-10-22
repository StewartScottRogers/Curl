using Curl.Config;

return await CurlConfigProgram.RunAsync(args, Console.Out, OperatingSystem.IsWindows(), AppContext.BaseDirectory).ConfigureAwait(false);
