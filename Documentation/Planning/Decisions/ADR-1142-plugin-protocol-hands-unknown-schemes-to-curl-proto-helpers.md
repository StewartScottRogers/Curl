# ADR-1142 — The plugin protocol extension hands unknown schemes to curl-proto-<scheme> helpers

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4160.

## Context

BL-4160 adds `Curl.Extension.Protocol.Plugin.UnitLibrary` (ADR-0999): a URL whose scheme Curl does not serve
runs through a program the user installs, `curl-proto-<scheme>`, found on `PATH`. Upstream curl has nothing
like it, so the specification is git's remote-helper model (gitremote-helpers, git 2.47 documentation,
<https://git-scm.com/docs/gitremote-helpers>), adapted: git runs `git-remote-<transport>` for a URL
`<transport>::<address>` or `<transport>://...` and talks to it over its standard input and output.

## Decision

- **No option and no fixed scheme.** At start-up the extension lists every `PATH` directory's
  `curl-proto-*` files. A file name serves the scheme after the prefix when that is an RFC 3986 scheme
  (`ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )`), lowercased. On Windows `PATH` is `;`-separated, the prefix
  matches without case and the name must end in one of `PATHEXT`'s extensions (default
  `.COM;.EXE;.BAT;.CMD`), which is dropped; elsewhere `PATH` is `:`-separated and the whole rest of the
  name is the scheme. The first directory holding a scheme's program wins, as a shell's command lookup.
  Empty `PATH` entries and missing or unreadable directories are skipped.
- **It never changes what curl does.** A helper is never taken for one of curl's own schemes (every
  scheme upstream curl can be built with, `PluginProtocolExtension.CurlSchemes`) nor for one another
  extension's description lists, so a `curl-proto-http` is ignored. With no helper for a scheme, or with
  `CURL_EXTENSIONS=0` (no extension handler at all), the dispatcher refuses it as curl refuses any unknown
  scheme: exit 1, `curl: (1) Protocol "demo" not supported`.
- **Invocation** (gitremote-helpers "INVOCATION"): the program runs with the URL as typed as its one
  argument; its standard error is Curl's.
- **Capabilities** ("Capabilities", "capabilities" command): Curl writes `capabilities` and a newline; the
  helper writes one capability per line and an empty line. Curl knows `get` and `put`. A capability starting
  with `*` is mandatory: one Curl does not know ends the transfer with exit 1
  (`curl-proto-demo requires the unknown capability connect`), as git aborts on one.
- **Commands** ("Commands", adapted from `fetch` and `push`): a transfer with neither `-T` nor `-d` writes
  `get <url>`; one with an upload writes `put <url>` and then the `-T` file or the `-d` data. Curl then
  closes the helper's input, the end of the command stream. A helper lacking the capability the transfer
  needs fails it with exit 1 (`curl-proto-demo does not offer the get capability`).
- **Reply**: one line, `ok`, after which everything to the end of the helper's output is the body Curl
  writes; or `error <message>`, exit 56 `curl-proto-demo: <message>`. Anything else, or the output ending
  first, is exit 8. Lines end with LF; a CR before it is dropped, since a Windows batch file's `echo` ends
  lines with CRLF. A line before the body may be at most 8192 bytes (exit 8 past it).
- **Exit status**: a helper exiting non-zero after its body fails the transfer with exit 56
  (`curl-proto-demo exited with status 3`); a program that cannot be started is exit 7
  (`Failed to run curl-proto-demo: <reason>`); a pipe breaking mid-conversation is exit 56.
- The `Process` adapter (`SystemHelperProcessStarter`) and the directory listing are excluded from coverage
  (ADR-0083); the conversation and the search are unit tested through `IHelperProcess` and a listing function.

## Left out, and why

- git's `option`, `list`, `connect`, `stateless-connect`, `import`/`export`, refspecs and batching: they
  carry git's object model, which a single URL transfer has no use for. A helper that requires one (`*`)
  is refused, as above.
- Curl's options (`-v`, `-u`, `-H`, timeouts) are not passed to the helper: the protocol has no channel for
  them yet, and adding one later stays compatible (a new capability).
- No executable-bit check on Linux and macOS: a non-executable `curl-proto-x` claims its scheme and then
  fails to start with exit 7, which says more than refusing the scheme silently would.

## Consequences

Every start-up with extensions on lists the `PATH` directories once; `CURL_EXTENSIONS=0` skips it.
`ExtensionCatalogue` passes the schemes of `ExtensionDescriptionCatalogue` to the factory, so an extension
whose handler serves a scheme its description does not list could clash with a helper; every extension's
description lists its schemes today.
