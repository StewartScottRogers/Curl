---
id: BL-091
title: Replace ? and * in -o file names on Windows as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-087]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-091 — Replace ? and * in -o file names on Windows as curl does

## Goal

On Windows, `Curl.Console` sanitises an `-o` file name exactly as curl 8.21.0 does before
opening it, so the file created (and the name in any `Failed to open the file` warning) is
the one upstream would use.

## Context

Measured 2026-09-26 on Windows with the local curl 8.21.0 (x86_64-w64-mingw32):
`curl --no-progress-meter -o "C:/x?y" file:///C:/Windows/win.ini` prints
`Warning: Failed to open the file C:/x_y: Permission denied`. curl passes the `-o` name
through its tool's `sanitize_file_name` (`src/tool_doswin.c`) before opening it, and `?`
and `*` become `_`. `Curl.Console/DeferredOutputFileStream.cs` today opens the name exactly
as typed, and `Curl.Console/OutputFileOpenWarning.cs` (BL-087) reports that unsanitised
name.

Not yet known, and the first step of this task: which other characters and names curl
sanitises. Measure with the local curl 8.21.0, one `-o` value per case, recording the exit
code, stderr and the file actually created (or not) for each of `<`, `>`, `|`, `"`, `:`
(outside the drive letter), control characters, and the reserved device names `con`,
`nul`, `prn`, `aux`, `com1`, `lpt1` (bare and with an extension, e.g. `nul.txt`).
`-o "Z:/a<b"` and `-o "Z:/a|b"` were seen to exit 0 printing nothing: find out which file
(if any) they wrote, and explain it from the measurement and `sanitize_file_name`'s source
(for example `<`/`|` being replaced, or the name mapping to a device). Record every
measured result, with the curl version and date, in the XML doc comment of the sanitising
type and in this task's `Notes`. Only implement what was measured; do not infer behaviour
from the source alone.

Where it lands: a `Curl.Console` type (for example `WindowsOutputFileNameSanitizer`) that
returns the sanitised name, applied in `DeferredOutputFileStream` before the file is opened
and before the warning text is built, and applied only on Windows through an injected
platform check so both branches are unit tested on any machine. Use the in-memory file
system in `Curl.Console.UnitTests/InMemoryFileSystem.cs`; no test touches the real disk.
Keep each method at cyclomatic complexity 10 or less; `Curl.Console` is held to 100% line
and branch coverage.

## Acceptance criteria

- [x] `Notes` in this task lists every measured character and reserved name with curl
      8.21.0's resulting file name, exit code and stderr, including the explanation of the
      `Z:/a<b` and `Z:/a|b` cases.
- [x] A named test in `Curl.Console.UnitTests` exists for each measured character and
      reserved name and passes, including `C:/x?y` becoming `C:/x_y` and `*` becoming `_`.
- [x] A test shows that, with the injected platform check reporting Windows, an `-o C:/x?y`
      whose open fails with access denied writes
      `Warning: Failed to open the file C:/x_y: Permission denied` to stderr.
- [x] A test shows the name is left unchanged when the platform check reports non-Windows.
- [x] `dotnet build Curl.Console -warnaserror` and
      `dotnet build Curl.Console.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test needs
      `TestCategory=Integration`.

## Notes

Measured 2026-09-26 on Windows 11 with the local curl 8.21.0 (x86_64-w64-mingw32):
`curl --no-progress-meter -o <name> file:///C:/Windows/win.ini` (92-byte source), one name
per case, run from an empty directory.

| `-o` value | Exit | stderr | File written |
| --- | --- | --- | --- |
| `a?b`, `a*b`, `a<b`, `a>b`, `a\|b`, `a"b` | 0 | none | `a_b` |
| `a` U+0001 … U+001F `b` (all 31 measured) | 0 | none | `a_b` |
| `a` U+007F `b` | 0 | none | `a<U+007F>b` (kept) |
| `ab:c` | 0 | none | `ab` (0 bytes), body in its NTFS `c` stream: `:` kept |
| `C:/…/sub/x:y` | 0 | none | `sub/x` (0 bytes) + stream `y` |
| `a:b` | 23 | `Warning: Failed to open the file a:b: No such file or directory` + `curl: (23) client returned ERROR on write of 92 bytes` | none (`a:` is drive A:) |
| `C:/…/sub/x?y` | 0 | none | `sub/x_y` (drive letter and `/` kept) |
| `sub?b` | 0 | none | `sub/a_b` (`\` kept) |
| `sub?/x`, no `sub_` dir | 23 | `Warning: Failed to open the file sub_/x: No such file or directory` + the (23) line | none: directory parts are rewritten and the warning names the rewritten file |
| `con`, `nul` | 0 | none, stdout empty | none (the devices) |
| `prn`, `aux`, `com1`, `lpt1` | 23 | `Warning: Failed to open the file <name>: No such file or directory` + `curl: (23) client returned ERROR on write of 92 bytes` | none |
| `con.txt`, `nul.txt`, `prn.txt`, `aux.txt`, `com1.txt`, `lpt1.txt` | 0 | none | that name, 92 bytes |
| `-C - -o a?b` with `a_b` = `XYZ` | 0 | none | `a_b` = `XYZ` + bytes 3.. : resume sizes the rewritten file |
| `-C 3 -o sub?` with `sub_` a directory | 23 | `curl: cannot open 'sub_'` + `curl: (23) Failed writing received data to disk/application` (also under `-sS`) | none |

`Z:/a<b` and `Z:/a|b` exit 0 printing nothing because `<` and `|` become `_`: they write
`Z:/a_b`. Reserved device names are not rewritten by 8.21.0 (bare names open the device;
with an extension, Windows 11 treats them as ordinary files), so the sanitiser keeps them.

Choices:
- The rewrite is applied once in `CurlCommandRunner.TransferAsync`, not inside
  `DeferredOutputFileStream`, because the measurement shows curl uses the rewritten name for
  the `-C -` size and the `curl: cannot open` line as well as the open and the warning; the
  stream and `OutputFileOpenWarning` receive the rewritten name.
- The platform check is a `bool runsOnWindows` constructor parameter on
  `CurlCommandRunner`, fed `OperatingSystem.IsWindows()` by `CurlComposition`, the same shape
  `StandardOutputOpener` uses.
- Not handled (not measured, left as upstream would have it or unreachable): U+0000
  (cannot appear in an argument), trailing dots and spaces.
- `--no-progress-meter` is not yet accepted by `CommandLineParser` (exit 2), so the runner
  test uses `-o` alone; the progress meter never prints for these cases anyway. The option belongs with BL-102 (progress meter).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. On Windows, -o names have "*<>?| and control characters rewritten to _ as curl 8.21.0 does, for the file, the -C - size and every message
