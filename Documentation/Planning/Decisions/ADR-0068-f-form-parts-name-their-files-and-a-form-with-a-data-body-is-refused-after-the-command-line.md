# ADR-0068 — `-F` parts name their files, header files are read as UTF-8, and a form with a `-d` body is refused after the command line

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"; made in task
BL-189 on 2026-09-26, recorded here by task BL-258).

## Context

BL-189 ported curl 8.21.0's `-F`/`--form` and `--form-string` parser (`formparse`,
`get_param_part` and `get_param_word` in `src/tool_formparse.c` at tag `curl-8_21_0`) into
`Curl.Cli.UnitLibrary`: `MultipartFormField` reads a value into a tree of
`FormPartSpecification`, and `FormPartParameterReader` reads the `;type=`, `;filename=`,
`;encoder=` and `;headers=` parameters. Tests are in `Curl.Cli.UnitTests/CommandLineFormOptionTests.cs`;
every pinned case was measured against curl 8.21.0 (`/mingw64/bin/curl`, Schannel), as BL-189's
Notes record.

Four places could not follow curl line for line without breaking a rule of this solution: the
argument parser reads files only through the injected `IDataFileReader`, which returns bytes or
failure and never why; .NET has no C runtime text mode; and curl reports one refusal from a
point in `main` that the per-option refusal model does not cover. BL-189 could not write this
ADR itself because another lane held `Documentation/Planning/Decisions`, so the decisions were
kept in its Notes until now.

## Decision

1. **`@file`, `<file`, `@-` and `<-` are named, not read, while parsing.** The part
   specification carries the file name (or standard input) and its `Kind`
   (`FileUpload` or `FileContent`); the parser reads nothing. curl reads a non-regular
   standard input at parse time; here the read moves to the transfer layer
   (`MultipartFormBodyBuilder`, see ADR-0062), which sends the same bytes.
   *Why:* the parser stays a pure function of its arguments and the injected reader, a
   file's bytes are read once, when the body is built, and the request on the wire does not
   change.
2. **An unreadable `;headers=@file` always warns
   `Cannot read from <file>: No such file or directory`.** curl prints `strerror(errno)`, so
   a directory or a file it may not open reads differently there.
   *Why:* `IDataFileReader.TryReadFile` says only that the read failed, not why. The missing
   file is the common case and matches curl byte for byte; carrying the operating system's
   reason through the reader would widen a contract every option that reads a file shares, for
   two rarer messages.
3. **A `;headers=@file` file is decoded as UTF-8 and split at LF**, not read through the C
   runtime's text mode. Each line is trimmed of trailing blanks, CR and LF; `#` lines and empty
   lines are skipped; a line starting with a space folds onto the previous header, as curl's
   `read_field_headers` does.
   *Why:* .NET has no text mode. The consequence is that a Ctrl-Z byte does not end the file
   early on Windows, and a line longer than curl's 8192-byte buffer is kept whole rather than
   failing the read. Both are cases no working script depends on, and matching them would mean
   hand-rolling C runtime quirks.
4. **`-F` with a `-d`/`--data*`/`--json` body is refused once the whole command line is read**,
   as `CommandLineRefusal.FormAndDataBoth`: exit 2, with no lines of its own after the
   request-method warning (`Warning: You can only select one HTTP request method! You asked for
   both POST (-d, --data) and multipart formpost (-F, --form).`, or `GET (-G, --get)` with `-G`).
   *Why:* curl 8.21.0 reports this conflict after the no-URL check, not when it reads the
   second option, and without the `curl: option ...: is badly used here` and try-help lines, in
   either option order. `CommandLineParser` therefore checks it after parsing ends, and `-s`
   anywhere prints nothing.

## Consequences

- The command line and the request bytes match curl 8.21.0 for every measured `-F` case,
  including the refusals and warnings.
- A `;headers=@` file that is a directory or unreadable warns `No such file or directory` where
  curl names the real reason; the exit code and the part sent are the same.
- A header file with a Ctrl-Z byte, or a line longer than curl's 8192-byte buffer, is read
  whole here, where curl on Windows stops early or fails the read.
- Reading `@-`/`<-` at transfer time rather than parse time is what ADR-0062 builds on for
  several URLs sharing standard input.

## Alternatives considered

- **Read form files while parsing, as curl does for standard input.** Puts I/O in the parser and
  holds file bytes in `CommandLineOptions`; the wire bytes would not change.
- **Give `IDataFileReader` a failure reason.** Exact `strerror` text, at the cost of changing a
  reader every file-reading option uses; left until a case needs it.
- **Emulate text mode (stop at Ctrl-Z, fail past 8192 bytes).** Reproduces a C runtime artefact
  that no script relies on.
- **Refuse `-F` + `-d` when the second option is read.** Would print the option and try-help
  lines and would refuse before the no-URL check, both unlike curl 8.21.0.
