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
completed:
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

- [ ] `Notes` in this task lists every measured character and reserved name with curl
      8.21.0's resulting file name, exit code and stderr, including the explanation of the
      `Z:/a<b` and `Z:/a|b` cases.
- [ ] A named test in `Curl.Console.UnitTests` exists for each measured character and
      reserved name and passes, including `C:/x?y` becoming `C:/x_y` and `*` becoming `_`.
- [ ] A test shows that, with the injected platform check reporting Windows, an `-o C:/x?y`
      whose open fails with access denied writes
      `Warning: Failed to open the file C:/x_y: Permission denied` to stderr.
- [ ] A test shows the name is left unchanged when the platform check reports non-Windows.
- [ ] `dotnet build Curl.Console -warnaserror` and
      `dotnet build Curl.Console.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test needs
      `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
