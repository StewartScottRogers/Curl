---
id: BL-5344
title: Make extension option error messages one style without .NET exception text
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitLibrary/ObliviousHttpExtension.cs, Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-5344 — Make extension option error messages one style without .NET exception text

## Goal

Extension option errors use one message style with no .NET `(Parameter '...')` text; `--extended-help` shows the diagnostic-log option placeholders as `--ai-help extensions` does; and a `-K` refusal of an extension option does not add a misleading line blaming `-K` after a correct first line.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-072 (Low, Confirmed; message text only). Area: extension option validation and help; Windows.

**Repro.**
1. `curl -sS --ohttp-keys k http://127.0.0.1:38999/` prints `curl: (2) --ohttp-keys needs --ohttp-relay (Parameter 'values')`.
2. `curl -sS --npipe x http://127.0.0.1:38999/` prints `curl: --npipe needs a named pipe ...` with no `(2)`; the same style for `--odoh-proxy`, `--masque`, `--ssh-jump`, `--vm-socket`, `--wireguard`, `openapi:` and `http-file:`, while `--checksum`, `--segments` and others print `curl: (2) ...`.
3. `curl -sS -K k3.cfg http://127.0.0.1:38999/` where `k3.cfg` holds `--http-file x.http`: a correct first line, then a misleading `curl: option -K: cannot be used in a config file`.
4. `curl --extended-help | tail -1` shows `Options: --log-level, --log-file` without value placeholders, while `--ai-help extensions` shows `--log-level <level>` and `--log-file <file>`.

**Expected.** One message style (`curl: (2) ...` or curl's `curl: option ...:` form, consistently; Claude chooses and records it); no `(Parameter 'values')`; placeholders as `--ai-help` gives them; the `-K` refusal should not blame `-K`.

**Actual.** As listed.

**Where to start.** The `ArgumentException` message used as-is in `Curl.Extension.ObliviousHttp.UnitLibrary/ObliviousHttpExtension.cs` (line 48, `throw new ArgumentException("--ohttp-keys needs --ohttp-relay", nameof(values))`); the catch sites in `Curl.Console/CurlComposition.cs` (lines about 787, 837, 871) that turn an `ArgumentException` into the message (normalising there fixes every extension, e.g. `NamedPipeExtension.cs`, without touching each); the diagnostic-log help lines in `Curl.Console/CurlCommandRunner.cs` (`CurlOnlyOptionLines`, line about 6129, beside `CurlOnlyOptionsMarkdown`); the `-K` refusal in `Curl.Cli.UnitLibrary/CommandLineParser.cs` and `CommandLineRefusal.cs` (`cannot be used in a config file`).

## Acceptance criteria

- [x] A short ADR in `Documentation/Planning/Decisions/` records the chosen single message style, marked "Decided by Claude under Stewart's delegation".
- [x] New tests in `Curl.Console.UnitTests` prove command 1 prints exactly `curl: --ohttp-keys needs --ohttp-relay` (or the chosen consistent form) with no `(Parameter`, and that bad values for `--npipe`, `--odoh-proxy`, `--masque`, `--ssh-jump`, `--vm-socket`, `--wireguard`, `openapi:` and `http-file:` print the same style as `--checksum` and `--segments`, with the exit code that style names.
- [x] A new test in `Curl.Console.UnitTests` proves the last `--extended-help` line contains `--log-level <level>` and `--log-file <file>`, in agreement with `--ai-help extensions`; `--ai-help` and `--extended-help` are both right.
- [x] A new test in `Curl.Cli.UnitTests` proves a `-K` config file holding `--http-file x.http` prints only the config-file line and the `try 'curl --help'` line, without a second `option -K: cannot be used in a config file` line.
- [x] Tests are platform-neutral.
- [x] Hand check recorded in the Log: `curl --extended-help | grep -c -- "--log-level <level>"` prints 1 and the three repro commands print the new messages.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Extensions are tested against their specification, not curl (CLAUDE.md, "Extensions").
- Review acceptance check: command 1 prints exactly `curl: --ohttp-keys needs --ohttp-relay` (or the chosen consistent form without the .NET suffix); `curl --extended-help | grep -c -- "--log-level <level>"` prints 1; command 3 prints only the config-file line and the `try 'curl --help'` line.

## Log

- 2026-11-16: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 8fbe307b facd2dc1 568a25f4 21088e1a c1cf21f9 8d812a90 a0b49a29.
- 2026-11-17: Done. ADR-1289 records the style: option refusal at hook activation is `curl: <message>` + try-help line, exit 2, no .NET text (`ExtensionTransferHooksOf` now catches like the other hook kinds). `--checksum`/`--segments` stay `curl: (2) ...` as they report a transfer-time failure (Notes decision). Expander-only option in `-K` prints the file line plus try-help only. Hand check: `--extended-help | grep -c -- "--log-level <level>"` prints 1; `--ohttp-keys k` prints `curl: --ohttp-keys needs --ohttp-relay` + try-help, exit 2; `-K` with `--http-file` prints the config-file line(s) and try-help. Tests are platform-neutral; Measure-CodeQuality for Curl.Cli.UnitLibrary and Curl.Console: 0 failing members.
- 2026-11-17: Doing -> Done. Extension option errors one style; help placeholders; -K refusal fixed (ADR-1289)
