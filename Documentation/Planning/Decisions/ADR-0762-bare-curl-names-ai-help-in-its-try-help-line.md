# ADR-0762 — Bare curl names --ai-help in its try-help line

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3145
- Decided by Stewart; scope decided by Claude under Stewart's delegation.

## Context

Curl is a drop-in replacement for curl 8.22.0, so every command-line error ends in curl's exact line
`curl: try 'curl --help' or 'curl --manual' for more information`. Stewart asked that bare `curl`
(no arguments) also name `--ai-help`, Curl's Markdown help for AI agents (ADR-0224), so an agent that
runs bare `curl` learns it exists.

## Decision

`curl` with no arguments writes `curl: try 'curl --help', 'curl --manual' or 'curl --ai-help' for more information`
(`CommandLineRefusal.TryHelpLineNamingAiHelp`). Exit code (2) and every other byte are unchanged. This
holds whenever the command line is empty, including after a `.curlrc` error, since the refusal is
`CommandLineRefusal.EmptyCommandLine`.

Every other refusal (unknown option, missing argument, no URL specified, and the rest) keeps
`CommandLineRefusal.TryHelpLine`, curl's exact text. Upstream cases 322, 411, 422, 481, 482, 484, 485,
746 and 1179 pin those outputs, and scripts parse them; an empty command line is the one place an
agent looks for guidance and no script parses it for a reason.

## Consequences

This is a deliberate, single-output difference from curl 8.22.0. The `--ai-help` index lists no entry
points, so it is unchanged.
