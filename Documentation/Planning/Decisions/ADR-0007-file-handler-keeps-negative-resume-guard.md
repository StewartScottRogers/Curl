# ADR-0007 — `FileProtocolHandler` keeps its negative-`ResumeFrom` guard as an unreachable defensive default

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Upstream curl 8.21.0 refuses a negative or non-numeric `-C`/`--continue-at` value in its
option parser, before any URL is looked at: exit 2 (`CURLE_FAILED_INIT`),
`curl: option -C: expected a proper numerical parameter` and the try-help line. See the
[`-C`/`--continue-at` manpage entry](https://curl.se/docs/manpage.html#-C).

Before an option parser existed, `FileProtocolHandler.ExecuteAsync` answered a negative
`ITransferContext.ResumeFrom` itself, with exit 36 (`CURLE_BAD_DOWNLOAD_RESUME`),
`failed to resume file:// transfer`, and only after URL parsing. The only record of that
was an XML comment. `CommandLineParser` in `Curl.Cli.UnitLibrary` now refuses the value
with exit 2 before any URL handling (tests in
`Curl.Cli.UnitTests/CommandLineRangeOptionTests.cs`), so the command line can no longer
put a negative offset into a transfer context.

`ITransferContext.ResumeFrom` is a `long?`, so a context built by hand, by a test or by
a future caller other than the command line, can still carry a negative value.

## Decision

The guard stays. `FileProtocolHandler.ExecuteAsync` keeps returning exit 36,
`failed to resume file:// transfer`, for a negative `ResumeFrom`, checked once after URL
parsing and before any file system call, for downloads and uploads alike. It is an
unreachable defensive default from the command line's point of view, not a curl
behaviour: curl's own answer to a negative `-C` is the parser's exit 2.

## Consequences

Good:

- A hand-built context with a negative offset fails cleanly. Without the guard,
  `TryResolveWindow` would take a negative start and a count larger than the file,
  and the upload path would try to skip a negative number of bytes; neither is defined.
- One check covers download and upload, which share nothing below it.

Costs and caveats:

- The branch is never taken through `Curl.Console`, so it is tested only through the
  handler's own unit tests.
- It answers with exit 36, a code curl never gives for a negative `-C`. Anyone reading
  the handler alone could take it for curl's behaviour; the XML comment points here to
  say it is not.

## Alternatives considered

- **Delete the branch.** Rejected: nothing in `ITransferContext` stops a negative
  value, and the code below the check does not handle one.
- **Throw `ArgumentOutOfRangeException`.** Rejected: a bad transfer option should end
  the transfer with a result, not end the process, and a changed contract would reach
  every caller of the handler.
- **Validate in `ITransferContext` itself.** Rejected for now: it is an interface in
  `Curl.Protocol.Abstractions.UnitLibrary`, a shared contract every protocol depends
  on, and changing it is a larger decision than this one guard.
