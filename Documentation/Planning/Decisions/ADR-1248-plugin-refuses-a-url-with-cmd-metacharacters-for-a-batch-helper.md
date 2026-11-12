# ADR-1248 — A plugin refuses a URL holding cmd.exe metacharacters for a .cmd or .bat helper

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4971 (the code is BL-4977)
- Decided by Claude under Stewart's delegation.

## Context

A plugin helper (`curl-proto-<scheme>`, ADR-1142) is started with the URL as its one argument. On
Windows a helper may be a `.cmd` or `.bat` file, and .NET then runs it through `cmd.exe`, which
parses the command line itself: a URL holding `&`, `|`, `<`, `>`, `^`, `"` or `%` can end the
argument and run the rest as another command (BatBadBut, CVE-2024-24576). Upstream curl has no
plugin helpers, so there is no curl output to measure.

## Decision

- `SystemHelperProcessStarter.RefuseUnsafeBatchArgument` refuses the transfer when the helper's
  name ends in `.cmd` or `.bat` (any case) and the URL holds any of `& | < > ^ " %` or a control
  character. It throws `IOException`, which `HelperProtocolHandler` turns into exit 7 with
  `Failed to run <helper>: the URL holds a character cmd.exe would run as a command (& | < > ^ " % or a control character), so it is not passed to a .cmd or .bat helper`.
- Any other URL, and every other kind of helper, is started as before with the URL as one argument.
- Refuse, not escape: launching through an escaped `cmd.exe /d /s /c` cannot be made reliable,
  because cmd's escaping rules differ by context (quotes, `%` expansion, `^` doubling) and a
  single missed case is a command injection. A refusal fails closed.

## Consequences

A URL with a query string holding `&` or `%20` cannot reach a batch helper; the helper has to be
an `.exe` for such a URL. Other helpers are unaffected.
Plugin.md Discovery-3 states the behaviour.
