# ADR-0434 — The native `curl.exe` is optimized for size and carries no stack-trace data

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

AF-0048 found Curl's large-get scenario slower than real curl. BL-1675 made the transfer
itself as fast as curl's. What remained was about 40 ms before `Main` on launches spaced
more than about 5 seconds apart. An AOT hello-world padded to the same size reproduces it,
so the cost tracks binary size. The likeliest cause is Windows Defender's on-access scan of
each launch. The published `curl.exe` was 17,853,440 bytes (17.0 MiB).

## Decision

`Curl.Console.csproj` sets, for the native publish only (ILC reads them; `dotnet build`, the
JIT-run tests and every library are unaffected):

- `OptimizationPreference=Size`
- `StackTraceSupport=false`

Together they take `curl.exe` to 14,384,640 bytes (13.7 MiB), 19.4% smaller.

Measured on win-x64 (BL-1749 Notes hold the raw numbers):

| Setting | `curl.exe` bytes | Change |
|---|---|---|
| none (before) | 17,853,440 | — |
| `OptimizationPreference=Size` | 15,348,736 | −14.0% |
| `StackTraceSupport=false` + `IlcFoldIdenticalMethodBodies=true` | 16,869,888 | −5.5% |
| `Size` + `StackTraceSupport=false` (chosen) | 14,384,640 | −19.4% |
| chosen + `IlcFoldIdenticalMethodBodies=true` | 14,384,640 | −19.4% |

Hot paths, before → after. Native AOT, best of many interleaved runs on a machine shared with
other lanes' builds:

- X25519 shared secret ×2000: 135 → 139 ms (+3.0%)
- Ed25519 sign+verify ×1000: 686 → 692 ms (+0.9%)
- RSA-2048 CRT private operation ×200: 1338 → 1347 ms (+0.7%)
- HTTPS GET against `Record-CurlExchange.ps1 -Tls`, median of 9: `%{time_appconnect}`
  31.6 → 32.5 ms (+3.1%), `%{time_total}` 42.5 → 43.5 ms (+2.4%)

All of them stay within the 5% budget. `--version`, `-V`, `--help all`, `--ai-help all`, an
unknown option, a refused connection (http and https) and an unopenable `file://` give the
same bytes before and after; only the measured milliseconds in a connect error differ. Curl
never prints a stack trace on a curl-compatible path, so `StackTraceSupport=false` changes
nothing a script can see.

## Rejected

- **`IlcFoldIdenticalMethodBodies`**: saves nothing on top of the chosen pair. Not set, so the
  build carries no setting that does no work.
- **`UseSystemResourceKeys`**: replaces BCL exception messages with resource keys. Curl writes
  some OS and BCL messages into its own error text (strerror-style reasons), so this could change
  output bytes. Not worth the risk.
- **`InvariantGlobalization`**: already on since before this decision.

## Consequences

- An unhandled exception in the native build prints no method names. That only affects
  Curl's own crash diagnostics, never curl-compatible output. The JIT build and tests still
  print full stack traces.
- A future setting that moves size or speed is measured the same way: size, the three
  native crypto loops, and the loopback TLS GET.
