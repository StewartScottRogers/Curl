# ADR-0098 — An unrewindable `-F` body on a redirect fails as the platform's build fails it

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; recorded
  by BL-402, 2026-09-27).

## Context

Under `-L`, a 307 or 308 keeps the request body for the next hop, so curl must rewind it.
A `-F` file part read from a pipe cannot be rewound. BL-359 measured the Schannel build on
Windows with a named pipe: exit 26, `curl: (26) read error getting mime data`, and
`RedirectFollower` returned that on every platform.

BL-402 measured the OpenSSL build on Linux with a FIFO (`mkfifo`), 307 then 200, with
`Record-CurlExchange.ps1` driving curl under WSL 2. The exact commands and bytes are in
BL-402's `Notes`:

| Build | Exit | stderr | `-w "[%{http_code}\|%{num_redirects}\|%{url_effective}\|%{redirect_url}]"` |
| --- | --- | --- | --- |
| Schannel, Windows (BL-359) | 26 | `curl: (26) read error getting mime data` | `[307\|1\|<307's target>\|]` |
| OpenSSL, Linux (BL-402) | 65 | `curl: (65) Cannot rewind mime/post data` | `[307\|1\|<307's target>\|]` |

Only curl 8.18.0 (Ubuntu's OpenSSL package, `8.18.0-1ubuntu2.4`) was available on Linux,
the same build ADR-0009's Linux strings come from. The rewind path in libcurl's `lib/mime.c`
and `lib/transfer.c` has kept this message since well before 8.18.0.

On Windows the C runtime's `stat` calls a named pipe a regular file (ADR-0097), so libcurl
believes the part seekable and fails later, reading it; on Linux and macOS a FIFO is not a
regular file, `fseek` fails with `ESPIPE`, and libcurl reports the failed rewind itself.

## Decision

`RedirectFollower` takes `runsOnWindows` (default `OperatingSystem.IsWindows()`). On
Windows an unrewindable body ends the chain with exit 26, `read error getting mime data`;
elsewhere with exit 65 (`CurlExitCode.SendFailRewind`), `Cannot rewind mime/post data`
(`RedirectFollower.CannotRewindMessage`). Either way the redirect counts as followed,
`%{url_effective}` is its target and `%{redirect_url}` is empty.

macOS is not measured. It is given the Linux answer because it uses the same OpenSSL build
rule (ADR-0009) and its FIFOs fail `fseek` the same way.

## Consequences

- A script on Linux or macOS sees curl's exit 65 and message rather than Windows' 26.
- Tests pin each platform's answer with the flag set, and the default with
  `[OSCondition]`, so they pass on all three CI platforms.
- If a curl 8.21.0 OpenSSL build on Linux measures differently, this is re-measured.
