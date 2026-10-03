---
id: BL-1288
title: Cut the native build's startup working set so a 50 MiB GET peaks at most 2x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1289, BL-1290]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1288 — Cut the native build's startup working set so a 50 MiB GET peaks at most 2x curl's

## Goal

The native AOT `curl.exe`, downloading a 50 MiB `Content-Length` body from a loopback server with
`-s -o <file>` and with `-s` to a piped standard output, peaks at no more than twice the working set
real curl peaks at on the same machine (audit finding AF-0018, task BL-1274).

## Context

BL-1274 (AF-0018) found the finding's named cause is not there: `HttpResponseBodyReader.CopyFramedAsync`
allocates its 16 KiB buffer once, outside the loop, and the body reader's private memory is small.
The excess is the native build's fixed startup footprint. Measured on Windows 11 on 2026-10-02 with
`Process` peak working set (`K32GetProcessMemoryInfo` after exit), median of 5 runs:

| Run | Peak working set | Private |
| --- | --- | --- |
| Windows `curl.exe` 50 MiB GET, `-o` file | 7.47 MB | 1.3 MB |
| Curl before any change, `-o` file | 20.38 MB (2.73x) | 7.2 MB |
| Curl before any change, piped stdout | 21.61 MB (2.89x) | 8.6 MB |
| Curl `--version` before any change | 15.71 MB | 5.4 MB |
| bare NativeAOT hello-world (`net10.0`, InvariantGlobalization) | 9.54 MB | 3.0 MB |

Step 1, already worked out in BL-1274 (its Notes): `Environment.GetFolderPath(SpecialFolder.UserProfile)`
in `Curl.Console/CurlComposition.cs` (the `accountHomeDirectory:` argument) and in
`Curl.Cli.UnitLibrary/DefaultConfigFileSearch.ForProcess` loads `shell32.dll` and `windows.storage.dll`
on every run, for a value curl only reads off Windows. Returning `null` on Windows
(`AccountHomeDirectory.ForPlatform(bool isWindows)` in `Curl.Cli.UnitLibrary`, used by both) cuts the
`-o` run to 17.56 MB (2.35x) and the stdout run to 18.78 MB (2.51x), `--version` to 13.6 MB.

Still to find, about 2.6 MB for `-o` and 3.8 MB for stdout. Tried in BL-1274 with no effect:
`DOTNET_gcConcurrent=0`, `DOTNET_GCgen0size`, `DOTNET_GCHeapHardLimit`, `DOTNET_GCRegionSize`,
`DOTNET_GCConserveMemory`, `IlcOptimizationPreference=Size`, and removing ILC's default
`DirectPInvokeList`. Leads: private memory at `--version` is 5.1 MB against hello-world's 3.0 MB
(startup allocations in `CurlComposition`/option table/static initializers); the piped-stdout path costs
1.4 MB private more than `-o`; Curl loads `ncrypt`, `Secur32`, `CRYPT32`, `NTASN1` and `IPHLPAPI` for a
plain `http://` GET where curl does not.

Measure with a C# file-based app (no Python): a `TcpListener` on loopback answering one
`HTTP/1.1 200` with `Content-Length: 52428800` and `Connection: close`, starting the client with
`ProcessStartInfo`, and reading `PeakWorkingSetSize` through `K32GetProcessMemoryInfo` on the process
handle after it exits (`Process.PeakWorkingSet64` throws once it has exited). Publish with
`dotnet publish Curl.Console -c Release -o <dir>`; delete `Curl.Console/obj/Release/net10.0/win-x64/native`
first when only MSBuild properties changed, or ILC does not relink.

## Acceptance criteria

- [ ] On Windows, `AccountHomeDirectory.ForPlatform(true)` returns `null` and neither `CurlComposition` nor `DefaultConfigFileSearch.ForProcess` calls `Environment.GetFolderPath` on Windows; off Windows the `.curlrc` and `known_hosts` searches still end with the account's home directory, pinned by unit tests in `Curl.Cli.UnitTests`.
- [ ] Measured as in Context, the published `curl.exe`'s median peak working set over 5 runs is at most 2x Windows `curl.exe`'s for both the `-o` run and the piped-stdout run, with the numbers recorded under Notes.
- [ ] No output byte, exit code or `-v` line changes: `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

### Run of 2026-10-02 (lane 1): back to Backlog behind BL-1289 and BL-1290

Measured as in Context (median of 5, Windows 11, `C:\windows\system32\curl.exe` 7.43 MB both runs, so
the 2x limit is 14.86 MB):

| Build | `-o` file | piped stdout |
| --- | --- | --- |
| Step 1 only (home directory) | 17.48 MB (2.35x) | 18.76 MB (2.52x) |
| Step 1 + synchronous stdout writes | 17.43 MB (2.35x) | 16.73 MB (2.25x) |

Code written in this run, left uncommitted for the shift to stash (rule 6); it builds and the
`Curl.Cli.UnitTests` (3754) and `Curl.Console.UnitTests` (2453) fast tests pass with it:
- `Curl.Cli.UnitLibrary/AccountHomeDirectory.cs`: `ForPlatform(bool isWindows)`, `null` on Windows,
  `Environment.GetFolderPath(UserProfile)` elsewhere; used by `DefaultConfigFileSearch.ForProcess` and
  `CurlComposition`'s `accountHomeDirectory:`. Tests in `Curl.Cli.UnitTests/AccountHomeDirectoryTests.cs`
  (the off-Windows `.curlrc` one is `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`).
  Criterion 1 is met by it.
- `Curl.Console/SynchronousWriteStream.cs`, wrapped around `StandardOutputOpener.OpenUnbufferedFileStream`'s
  `FileStream`: a `FileStream` over the non-overlapped stdout handle runs every `WriteAsync` as a thread-pool
  work item, which cost 1.8 MB of allocation on the 50 MiB pipe run (3.36 MB total allocated, down to
  1.56 MB with it) and 2 MB of peak working set. curl writes stdout with `fwrite`, synchronously.
  Still needs its own unit tests in `Curl.Console.UnitTests` (Write, WriteAsync, a failed write's
  `IOException` as a faulted `ValueTask`, a cancelled token, Flush, Dispose owning the inner stream).
  If the stash is gone, both are about 15 minutes to rewrite from this note.

What the rest of the excess is (all measured in this run):
- The GC never runs in a 50 MiB GET (collection count 0 at exit), so every allocated byte stays in the
  working set; private memory grows with the body (5.32 MB at 0 bytes, 7.72 MB after 40 MiB). What is
  left allocates about 1.5 MB on both runs, on the read side: BL-1289. `DOTNET_GCgen0size`,
  `DOTNET_GCgen0MaxBudget`, `DOTNET_gcServer` and a `RuntimeHostConfigurationOption` for `GCgen0size`
  changed nothing; the native build does not take them.
- `ole32.dll` loads `USER32`, `GDI32`, `gdi32full`, `win32u`, `IMM32` and `msvcp_win`, about 1.1 MB: BL-1290.
- `QueryWorkingSet` while waiting for the response: 14.2 MiB, of which `curl.exe`'s own image pages are
  4.9 MB (`.text` 2.7 MB, `.rdata` 1.5 MB, `.data` 0.6 MB) against 0.5 MB for real curl, and non-image
  private memory 1.4 MB. So even with BL-1289 and BL-1290 the `-o` run lands near 15 MB; if it is still
  over 14.86 MB once they are Done, the startup code paged in (`.text`) is the next lead.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Backlog. Waits on BL-1289 (read-path allocations) and BL-1290 (ole32/user32 imports); after this run's changes the 50 MiB GET peaks at 2.35x (-o) and 2.25x (pipe)
- 2026-10-03: Backlog -> Doing.
