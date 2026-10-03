---
id: BL-1288
title: Cut the native build's startup working set so a 50 MiB GET peaks at most 2x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
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

## Log

- 2026-10-02: Created.
