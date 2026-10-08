# ADR-0431: The native curl.exe delay-loads the DLLs a plain transfer never calls

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1682 (audit finding AF-0063)
- Decided by Claude under Stewart's delegation.

## Context

AF-0063 measured the native `curl.exe` at 2.017x real curl's median peak working set for a small
`http://` GET (12,296,192 bytes against 6,094,848), over the audit's 2x threshold. BL-1715 built the
protocol handlers lazily; the gap stayed at 2.02x on the lane's machine (14.00 MB against 6.93 MB).

ILC links `ncrypt.dll`, `Secur32.dll`, `ADVAPI32.dll`, `CRYPT32.dll` and `IPHLPAPI.DLL` as static
imports, so Windows maps them at startup - with `NTASN1`, `msvcrt`, `kernel.appcore` and
`bcryptPrimitives` behind them - though a plain HTTP transfer calls none of them. Real curl's
process list for the same GET has none of `ncrypt`, `Secur32`, `NTASN1`, `ADVAPI32` or `msvcrt`.

Runtime knobs were measured first and changed nothing: `UseWindowsThreadPool`,
`UseSystemResourceKeys`, `DOTNET_GCConserveMemory`, and those BL-1682's Notes list.

## Decision

`Curl.Console.csproj`'s `DelayLoadDllsPlainTransfersNeverCall` target, beside ADR-0402's
`LinkComThroughApiSetsNotOle32`, adds `delayimp.lib` and `/DELAYLOAD` for those five DLLs on
Windows. Each is mapped on its first call: a TLS handshake (Schannel through `Secur32`), certificate
work, Negotiate, an interface lookup.

## Consequences

- A small `http://` GET peaks at 13.11 MB against curl's 6.93 MB on the lane's machine: 1.89x, down
  from 2.02x (median of 15, 1 KiB body over loopback).
- `https://` still works: the first Schannel call loads `Secur32` as before.
- A DLL that failed to load would now fail at its first call rather than at startup; these are all
  in-box Windows DLLs, so that cannot happen on a supported Windows.
- Linux and macOS builds are unchanged: the target runs only for `win-*` runtime identifiers.
