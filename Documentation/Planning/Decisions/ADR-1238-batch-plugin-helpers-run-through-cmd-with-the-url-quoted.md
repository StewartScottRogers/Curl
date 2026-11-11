# ADR-1238 — Batch plugin helpers run through cmd.exe with the URL quoted and `%` neutralised

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4898
- Decided by Claude under Stewart's delegation.

## Context

ADR-1142 finds `curl-proto-<scheme>` helpers through `PATHEXT` on Windows, so a `.bat` or `.cmd` file can be
a helper. `SystemHelperProcessStarter` started every helper with `ProcessStartInfo(program)` and
`ArgumentList.Add(url)`. Windows runs a batch file through `cmd.exe /c`, which reads the whole command line
again, and .NET quotes arguments by the C runtime's rules, not cmd's. The outside fitness review of
2026-11-11 (EXT-001) showed the result: `demo://host/a&echo.>pwn1.txt` ran `echo.>pwn1.txt` as a second
command, a URL with `"` broke out of the quotes the same way, and `%PATH%` expanded and split into many
arguments. This is the "BatBadBut" class (CVE-2024-24576). Anyone who can supply a URL could run commands.

Three options were weighed: refuse batch helpers outright; refuse every URL holding a cmd metacharacter;
or start `cmd.exe` ourselves with an argument cmd cannot misread. Refusing batch helpers drops a documented
`PATHEXT` default. Refusing metacharacters refuses nearly every real URL, since `&` and `%` are in most
query strings and percent-encodings.

## Decision

On Windows, a helper whose name ends in `.bat` or `.cmd` (ignoring case, and the trailing dots and spaces
Windows drops) is started as `%SystemRoot%\System32\cmd.exe /e:ON /v:OFF /d /c ""<program>" "<url>""`,
built by `BatchHelperCommandLine`, the scheme Rust adopted for CVE-2024-24576:

- `/d` skips AutoRun commands, `/v:OFF` keeps `!` literal, `/e:ON` fixes the extensions on.
- `cmd /c` strips the outer pair of quotes; the program and the URL are each one quoted value, inside
  which `& | < > ^` are literal.
- Every `%` is written `%%cd:~,%`: `%cd:~,%` expands to nothing, so the helper receives a single `%` and
  no `%NAME%` can expand.
- A program or URL holding a double quote, carriage return, line feed or NUL, which cmd cannot carry in
  one argument, is refused before anything runs: `curl: (7) Failed to run <helper>: a batch helper
  cannot receive a double quote, carriage return, line feed or NUL`. Exit 7 is the code every other
  failure to start a helper already gives (ADR-1142).

The helper reads the URL byte-equal through `%~1`. Every other helper, and every helper off Windows, is
started as before.

## Consequences

- None of the review's three repro commands runs a second command; `%PATH%` arrives literal.
- A URL with a raw `"` cannot reach a batch helper; an `.exe` helper still receives it.
- The command line is pure string building, unit tested in `BatchHelperCommandLineTests` on every platform;
  only the `Process` call stays excluded from coverage (ADR-0083).
