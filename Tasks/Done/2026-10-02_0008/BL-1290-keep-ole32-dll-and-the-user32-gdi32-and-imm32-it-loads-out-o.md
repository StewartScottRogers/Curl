---
id: BL-1290
title: Keep ole32.dll and the user32, gdi32 and imm32 it loads out of the native curl.exe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1290 — Keep ole32.dll and the user32, gdi32 and imm32 it loads out of the native curl.exe

## Goal

The published native `curl.exe` on Windows imports none of its COM functions from `ole32.dll`, so a plain `http://` GET never loads `ole32.dll`, `USER32.dll`, `GDI32.dll`, `gdi32full.dll`, `win32u.dll` or `IMM32.DLL` (BL-1288).

## Context

Found in BL-1288 (its Notes) on 2026-10-02. `dumpbin /imports` of the published `curl.exe` shows
ILC binding seven COM functions to `ole32.dll`: `CoCreateGuid`, `CoGetApartmentType`, `CoInitializeEx`,
`CoTaskMemAlloc`, `CoTaskMemFree`, `CoUninitialize`, `CoWaitForMultipleHandles`. `ole32.dll` statically
imports `USER32.dll` and `GDI32.dll`, which bring `gdi32full.dll`, `win32u.dll`, `IMM32.DLL` and
`msvcp_win.dll`; real `curl.exe` loads none of them. Measured with `QueryWorkingSet` while the
transfer waits for the response, those modules hold about 1.1 MB of the working set (`combase.dll`
stays: it serves the COM API set). All seven functions are in the `api-ms-win-core-com-l1-1-*` API
sets, which `combase.dll` serves without user32.

Tried in BL-1288:
- `<NativeLibrary Include="mincore.lib" />` in an `ItemGroup` of `Curl.Console.csproj`: linked ahead of
  ILC's defaults, it bound six of the seven to `api-ms-win-core-com-l1-1-0.dll`, but
  `CoWaitForMultipleHandles` (referenced from the runtime's own `.lib`, which links after it) still
  came from `ole32.lib`, so `ole32.dll` still loaded. `mincore.lib` exports it, so this is link order.
- `onecore.lib` the same way: no change at all to the imports.
- A target with `BeforeTargets="LinkNative"` removing `ole32.lib` from `@(NativeLibrary)` and adding
  `mincore.lib`: no change to the imports, so the link step does not read `NativeLibrary` there; read
  `Microsoft.NETCore.Native.targets` (`LinkNative`, the `link.rsp` it writes) for the item or property
  it does read.
Publish with `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH, and delete
`Curl.Console/obj/Release/net10.0/win-x64/native` first or ILC does not relink. Native AOT on .NET 10
needs Windows 10 or later, where the API sets exist.

## Acceptance criteria

- [x] `dumpbin /imports` of `dotnet publish Curl.Console -c Release`'s `curl.exe` lists no `ole32.dll`, and a 50 MiB loopback GET's process does not load `ole32.dll` or `USER32.dll` (listed through `Process.Modules` while the server holds the response), with the commands and output recorded under Notes.
- [x] Off Windows the publish is unchanged (the change is conditioned on the Windows runtime identifier).
- [x] No output byte, exit code or `-v` line changes: `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Decision recorded in ADR-0402. Two causes, two fixes:

1. **ole32.dll.** ILC's `SetupOSSpecificProps` (Microsoft.NETCore.Native.Windows.targets, ILCompiler
   10.0.9) turns `@(SdkNativeLibrary)` into `@(LinkerArg)`, and `LinkNative` writes `@(LinkerArg)` to
   `link.rsp`; that is why BL-1288's `NativeLibrary` edits before `LinkNative` changed nothing.
   `Curl.Console.csproj`'s target `LinkComThroughApiSetsNotOle32` runs after `SetupOSSpecificProps`,
   only when `$(RuntimeIdentifier)` starts with `win`, and replaces `"ole32.lib"` in `@(LinkerArg)` with
   `"mincore.lib"` (linked last, after kernel32.lib and the rest, so only the COM functions bind to it).
2. **USER32 still loaded without ole32.** After fix 1 the process still had `shell32.dll`, `SHCORE.dll`,
   `windows.storage.dll`, `USER32.dll`, `GDI32.dll`, `gdi32full.dll`, `win32u.dll`, `IMM32.DLL`:
   `Environment.GetFolderPath(SpecialFolder.UserProfile)` calls `SHGetKnownFolderPath` in shell32.
   Called eagerly for `DefaultConfigFileSearch.ForProcess` and `CurlComposition`'s
   `accountHomeDirectory`, both read only off Windows. New `Curl.Cli.AccountHomeDirectory.ForProcess`
   is `null` on Windows, so neither search changes. This needed `Curl.Cli.UnitLibrary` and
   `Curl.Cli.UnitTests`, added to `touches`: no task in Doing on `origin/work/dark-factory` named them
   (only BL-1289: Http, Networking, Core).

Measured 2026-10-02 (`C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH,
`Curl.Console/obj/Release/net10.0/win-x64/native` deleted first):

```
dotnet publish Curl.Console -c Release -o out-bl1290
dumpbin /imports out-bl1290\curl.exe
  ADVAPI32.dll api-ms-win-core-com-l1-1-0.dll api-ms-win-crt-{convert,heap,locale,math,runtime,stdio,string}-l1-1-0.dll
  bcrypt.dll CRYPT32.dll IPHLPAPI.DLL KERNEL32.dll ncrypt.dll Secur32.dll WS2_32.dll
  api-ms-win-core-com-l1-1-0.dll: CoGetApartmentType CoInitializeEx CoTaskMemAlloc CoTaskMemFree
    CoUninitialize CoWaitForMultipleHandles CoCreateGuid
  (no ole32.dll line)
```

A PowerShell `TcpListener` on 127.0.0.1 accepted `curl -s -o NUL http://127.0.0.1:<port>/big`, sent
`Content-Length: 52428800` and 1 MiB, held 1.5 s, listed `Process.Modules`, then sent the rest:

```
Before the fix (only fix 1): 34 modules, ole32.dll False, USER32/GDI32/gdi32full/win32u/IMM32 True
After both fixes: 22 modules:
ADVAPI32.dll, bcrypt.dll, bcryptPrimitives.dll, combase.dll, CRYPT32.dll, curl.exe, IPHLPAPI.DLL,
kernel.appcore.dll, KERNEL32.DLL, KERNELBASE.dll, msvcrt.dll, mswsock.dll, ncrypt.dll, NTASN1.dll,
ntdll.dll, RPCRT4.dll, sechost.dll, Secur32.dll, SSPICLI.DLL, ucrtbase.dll, WS2_32.dll, wshunix.dll
ole32.dll: False  USER32.dll: False  GDI32.dll: False  gdi32full.dll: False  win32u.dll: False  IMM32.DLL: False
exit code: 0
```

Off Windows: the target's condition is false for `linux-*` and `osx-*` runtime identifiers, and
`AccountHomeDirectory.ForProcess` still calls `GetFolderPath` there (pinned by
`ForProcess_OffWindows_IsTheUserProfileFolder`, run off Windows only).

`dotnet build`: 0 errors. Fast tests: all 33 test projects passed (Curl.Cli.UnitTests 3755 passed,
Curl.Console.UnitTests 2453 passed).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The native curl.exe imports no ole32.dll and a plain GET loads no ole32, user32, gdi32 or imm32 (34 modules down to 22)
