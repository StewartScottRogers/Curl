# Curl.Console

A complete managed C# reimplementation of curl and libcurl for .NET 10: no native libcurl, native-AOT ready, and it behaves like curl 8.22.0. Run its `curl` command from nuget.org with `dnx Surly.Curl@<version> --yes -- <curl arguments>`.

## Run it from nuget.org

All it needs is the .NET 10 SDK. To run it without installing it:

```sh
dnx Surly.Curl@<version> --yes -- <curl arguments>
dnx Surly.Curl@<version> --yes -- -sS https://example.com/
```

In PowerShell write the separator as `'--'`: PowerShell takes a bare `--` for itself, and `dnx`
then reads curl's options as its own.

To install it as the command `curl`:

```sh
dotnet tool install -g Surly.Curl --prerelease
curl --version
```

A global install can shadow the system curl on PATH: which `curl` runs then depends on the order
of the directories in PATH (on Windows, `curl.exe` in `System32` normally comes first).
`dotnet tool uninstall -g Surly.Curl` removes it. `dnx` installs nothing on PATH.

`Surly.Curl` is a .NET tool whose per-platform packages (`win-x64`, `win-arm64`, `linux-x64`,
`linux-arm64`, `osx-x64`, `osx-arm64`) each hold the native ahead-of-time `curl`; .NET picks the
one for your machine. Linux needs glibc 2.35 or newer.

## About the project

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

`dotnet pack Curl.Console -c Release -o <folder>` makes the `Surly.Curl` tool package, and
`dotnet pack Curl.Console -c Release -r <rid> -o <folder>`, run on that runtime's operating
system, makes its native package; the [Release workflow](../.github/workflows/release.yml) packs
all seven (ADR-1464).

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
