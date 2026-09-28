# ADR-0064 — `--variable` content is bytes and expands through UTF-8 option values

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-199, 2026-09-27).

## Context

BL-199 adds `--variable` and `--expand-<option>` to `Curl.Cli.UnitLibrary`. The behaviour was
measured with curl 8.21.0 (the mingw Schannel build, `/mingw64/bin/curl`) on 2026-09-27,
through `--expand-data` against a loopback server that echoes the request body, and checked
against `src/var.c`, `src/tool_getparam.c` and `lib/curlx/base64.c` at tag `curl-8_21_0`.
The measurements are recorded in the doc comments of `CommandLineVariableOptionTests` and
`CommandLineExpandOptionTests`, which pin them.

Git Bash rewrites some arguments before a native program sees them (`%NAME[1-2]=zzzz`
reached curl changed), so the environment-import cases were measured again from PowerShell,
where they agree with the source.

Three choices were left open by curl's behaviour:

1. curl works on the bytes of its narrow `argv`; `CommandLineOptions` holds every option value
   as a .NET string, and each option turns its string into bytes as UTF-8.
2. `--variable %name` reads the process environment. The parser takes its file system and
   console access as parameters, but no environment reader.
3. `--variable name@file` reports `Failed to open <file>: <strerror>`, and `IDataFileReader`
   answers only whether a file was read, not why not.

## Decision

1. A variable holds bytes: the UTF-8 bytes of `=content` or of an imported environment value,
   or the bytes of the file or standard input `@` names, cut to its `[start-end]` range. An
   `--expand-` value is expanded as UTF-8 bytes and the result read back as UTF-8 before the
   option applies it. Every text that is UTF-8 goes through unchanged.
2. `--variable %name` reads this process's environment with `Environment.GetEnvironmentVariable`,
   in every `CommandLineParser.Parse` overload. Tests use names no other test uses.
3. The reason after `Failed to open <file>:` is the one the Windows curl 8.21.0 prints:
   `Invalid argument` for the empty name, `Permission denied` for a path the parse's
   path-existence check finds (a directory, which cannot be opened), and
   `No such file or directory` for any other.

## Consequences

- Every function, warning and refusal matches curl byte for byte for UTF-8 content.
- Bytes that are not UTF-8 (a binary file, or `64dec` of arbitrary data, such as `////`
  giving `FF FF FF`) become U+FFFD where curl passes them on, so the option receives
  `EF BF BD` for each. `url`, `b64` and `json` of such content are unaffected, because they
  work on the variable's bytes before the result is read back; only unencoded binary
  expansion differs.
- On Windows curl reads its arguments in the ANSI code page, so `--variable a=é` URL-encodes
  as `%E9` there and as `%C3%A9` here, as UTF-8 does on Linux and macOS. This follows the
  solution's existing treatment of option values as UTF-8.
- A file that exists but cannot be read for another reason (locked, no access) is reported as
  `Permission denied`, which is what the C library reports for most of those on Windows.

## Alternatives considered

- **Carry option values as bytes end to end.** Faithful for binary content, but it changes
  the type of every option on `CommandLineOptions` and every applier, for a case (binary
  bytes expanded unencoded into an option) curl's own manual steers away from.
- **Inject an environment reader into the parser.** Adds a parameter to the public
  `Parse` overloads and to `Curl.Console`, which is another task's project, to replace a
  single, side-effect-free read that tests can already control.
