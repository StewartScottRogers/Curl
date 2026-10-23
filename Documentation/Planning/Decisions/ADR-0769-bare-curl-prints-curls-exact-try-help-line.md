# ADR-0769 — Bare curl prints curl's exact try-help line

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3185
- Decided by Stewart (2026-10-22, BL-3171 item 3); the per-platform pinning decided by Claude under Stewart's delegation.
- Supersedes: ADR-0762

## Context

ADR-0762 had bare `curl` (no arguments) name `--ai-help` in its try-help line, the one deliberate
difference from curl's bytes on a refusal. Stewart reversed that on 2026-10-22: a drop-in replacement
prints curl's bytes, and `curl --help` already names `--ai-help` in its footer (ADR-0224).

## Decision

`curl` with no arguments writes `curl: try 'curl --help' or 'curl --manual' for more information` alone on
standard error and exits 2 (`CommandLineRefusal.EmptyCommandLine`, built on `CommandLineRefusal.TryHelpLine`).
Measured with curl 8.22.0 (Schannel, x86_64-w64-mingw32) on Windows on 2026-10-22: exactly that line, exit 2.
Off Windows the line is upstream 8.22.0's `helpf` for a build with the manual, which the OpenSSL builds
have, so it is the same text. Each platform's answer is pinned in its own `[OSCondition]` test
(`CommandLineRefusalEmptyCommandLineTests`), so a platform whose curl differs can change alone.

## Consequences

`CommandLineRefusal.TryHelpLineNamingAiHelp` is removed. Every refusal now ends in curl's exact try-help line,
and `--ai-help` is named only in `curl --help`'s footer (`CurlHelpText.AiHelpUsageLine`).
