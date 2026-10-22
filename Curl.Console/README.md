# Curl.Console

`Curl.Console` is the `curl` executable: a drop-in replacement for the `curl` command-line tool,
written in C# on .NET 10. It takes the same options and gives the same exit codes and output bytes
as curl, so a script cannot tell which binary it started. Its assembly name is `curl`, so the
build produces `curl.exe` (`curl` off Windows).

It is the only project without a `.UnitLibrary` suffix, because it is an executable, and the only
one that references every protocol library. Composing the services is its job; the transfers
themselves are done by the libraries it composes.

## Build and run

```powershell
dotnet build Curl.Console
dotnet run --project Curl.Console -- -sS https://example.com/ -o page.html -w "%{http_code}\n"
```

Native ahead-of-time is the shipping configuration (`PublishAot` is on by default), so
`dotnet publish Curl.Console -c Release -r win-x64` produces the single native file that drops onto
`PATH` as `curl.exe`. It needs the MSVC linker from the "Desktop development with C++" workload and
`vswhere.exe` on `PATH`; a machine that cannot link native code publishes with
`-p:PublishAot=false`. `dotnet build` and `dotnet test` are unaffected by `PublishAot`.
`curl --ai-help` prints help in Markdown for AI agents.

## How the source files group

`Program.Main` opens the standard streams and hands the arguments to `CurlCommandRunner`;
everything else is reached from there.

- **Entry and composition**: `Program`, `CurlComposition` (the composition root, plain constructor
  calls with no container or reflection, so native AOT sees every type; `CreateRunner` builds the
  runner), `CurlCommandRunner` (parses the command line, runs each URL, prints `curl: (N) <message>`
  lines and returns the exit code), `InProcessCurl`.
- **Command line to options**: `EasyOptionsFromCommandLine` maps each option group onto
  `Curl.Api.UnitLibrary`'s easy option groups, as curl's `config2setopts.c` does;
  `EasySetoptsFromCommandLine` (with its `.TlsAuthenticationProxy` and `.HttpProtocols` parts) turns
  them into one `EasySetopt` per `CURLOPT_*`; `ToolCurlEngine` and `ToolEasyHandle` make those calls
  on `Libcurl`'s `CurlLibrary`; `CurlMimeFromFormBody` builds the `-F` form as a `curl_mime` tree;
  `RetryPolicyMapping`; `UrlTransfer` (one URL after glob expansion).
- **Standard streams, input and output streams**: `StandardOutputOpener`,
  `ClosedStandardOutputStream`, `StandardOutputFailureDeferringStream`, `StandardOutputVirtualTerminal`,
  `TerminalBinaryOutputRefusingStream`, `LineFeedToCrLfStream`, `FlushEachWriteStream`,
  `TextModeUntilBinaryStream`, `HoldableStream`, `DumpHeaderOutputStream`, `EtagSaveStream`,
  `StandardInputUploadStream`, `RateLimitedUploadStream`.
- **Output files**: `OutputFileTarget`, `DeferredOutputFileStream`, `RemoteFileName`,
  `RemoteHeaderNameStream`, `ContentDispositionFileName`, `RedirectLocationFileName`,
  `WindowsOutputFileNameSanitizer`, `OutputFileDirectories`, `DirectoryCreationFailure`,
  `OutputFileRemoval`, `OutputFileOpenWarning`, `OutputFileExtendedAttributes` with
  `IExtendedAttributeWriter` and `NativeExtendedAttributeWriter`, `IOutputPaths` with
  `PhysicalOutputPaths`, `NativeRegularFileTest`, `DiskWriteOutFileOpener` and
  `RefusingWriteOutFileOpener` (the `%output{file}` targets).
- **Parallel transfers and progress** (`-Z`): `ParallelRun`, `ParallelTransferQueue`,
  `ParallelHostQueue`, `ParallelTransferMulti`, `ParallelTransferProgress`, `ParallelProgressMeter`,
  `WriteGate`, `WriteGateStream`, `RunningTransferState`, `TransferProgressRecorder`,
  `ProgressBarRecorder`, `ProgressMeterLines`.
- **Diagnostics and notes**: `RunDiagnosticLog`, `GatedDiagnosticLog`, `StandardErrorLogTarget`,
  `TransferDiagnosticLines`, `RecordingDataFileReader`, `TransferEventOutput`,
  `ConnectionIdRecordingTransferEvents`, `TlsResultRecordingTransferEvents`,
  `MultiStateTraceEvents`, `TlsSessionFileLines`, `ToolNotes`, `WarningLineWrapper`,
  `TerminalColumns`, `RemoteTimeFailureWarning`, `StandardInputAnyAuthUploadWarning`,
  `EasyHandleWriteOutInfo`, `DebugWriteOutStandIn`.

[CLAUDE.md](CLAUDE.md) holds the detailed behaviour of each of these, option by option.

## Quality gates and sibling projects

Like every executable and `*.UnitLibrary`, the project is held to 100% line and branch coverage,
cyclomatic complexity of at most 10 per method and a CRAP score of at most 30. Coverage comes from
the fast tests alone: a line only an Integration test reaches counts as uncovered.

- `Curl.Console.UnitTests` tests it (`dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"`).
- `Curl.Console.IntegrationTests` holds the tests that touch something real outside the process.
- It references `Libcurl` for the transfer engine and `curl_easy_strerror`'s text, and the
  protocol, output, CLI and core libraries for everything else.
- Its siblings among the hosts are [Curl.Api](../Curl.Api/README.md) and
  [Curl.Mcp](../Curl.Mcp/README.md).
