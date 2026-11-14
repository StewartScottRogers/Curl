---
id: BL-5162
title: Export the OpenTelemetry span when a followed redirect is refused and keep first-hop state per transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-5164]
touches: [Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs, Curl.Extension.OpenTelemetry.UnitTests]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5162 — Export the OpenTelemetry span when a followed redirect is refused and keep first-hop state per transfer

## Goal

Every `--otel-endpoint` HTTP transfer exports exactly one span with its own trace, even when its redirect chain ends in a refused hop or when `-Z` runs several followed transfers at the same time.

## Context

Found by the shift-end review of 2026-11-13 (ADR-0710), silent-failure-hunter and code-reviewer, confirmed against the code. Introduced with BL-5098.

`Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:34-42, 61-72, 78-82`:

1. **Refused redirect loses the span.** When a hop returns a 3xx with a `RedirectUrl` and another redirect is allowed, `IsFollowedRedirect` returns early without exporting, on the guess that the follower will call the hook again. `RedirectFollower.StopBeforeHop` (`Curl.Core.UnitLibrary/RedirectFollower.cs`) can still refuse the hop: an unparsable target (exit 3), a scheme refused by `--proto-redir` (exit 1), `--disallow-username-in-url`, a failed proxy choice, or a multipart body that cannot be rewound. The hook is then never called again, so the failed transfer exports nothing and logs no warning. The hook's doc says every HTTP transfer gets a span.
2. **Shared first-hop state.** `_firstHop` is a field on the hook. `CurlComposition.ExtensionTransferHooksOf` (`Curl.Console/CurlComposition.cs`) builds one hook per option group, and `-Z` runs that group's transfers at the same time. Transfer B's first hop overwrites A's trace-id and start. A's next hop then sends B's `traceparent`, and one transfer finishing clears the state the other still needs. The comment at line 61 states the assumption but nothing enforces it.

## Acceptance criteria

- [x] With `-L`, a 302 whose next hop the follower refuses still exports one span for the transfer, carrying the 302 response.
- [x] Two concurrent followed transfers through one hook each export one span with their own trace-id and start time, and neither sends the other's `traceparent`.
- [x] The comment saying the hook "assumes one followed transfer at a time" is removed or made true.
- [x] Unit tests in `Curl.Extension.OpenTelemetry.UnitTests` cover both cases. `dotnet build` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.

## Notes

No code written. The hook runs once per hop and has no end-of-chain signal, so it cannot tell a refused redirect; BL-5164 adds that signal. Plan once it is Done: keep first-hop state in a map keyed by reference of Output+Progress+Events (all copied unchanged into every hop by `RedirectFollower.NextHop`), export on the end-of-chain call, remove the "one followed transfer at a time" comment.

Done (2026-11-13): `OpenTelemetryTransferHook` keeps each chain waiting on its next hop in a `ConcurrentDictionary` keyed by the hop's (Progress, Events) sinks, which `RedirectFollower.NextHop` copies unchanged and the tool gives every transfer its own (a `TransferProgressRecorder` per transfer), so `-Z` transfers through one hook keep their own trace-id and start. Output was left out of the key: `TransferHookingProtocolHandler` wraps it in the hooks' streams on each hop. `TransferChainFinishedAsync` (BL-5164) exports the span of a chain still waiting, that is one whose next hop was refused, with the chain's exit code and message and, when the refusal carries no report (`TransferResult.Failure`), the last hop's report, so the span carries the 302. Decided by Claude under Stewart's delegation; recorded here rather than in an ADR because it only implements the plan above. The "one followed transfer at a time" comment is gone. Tests: `OpenTelemetryChainEndTests` (4). Measure-CodeQuality for the library: 0 failing members. Full build clean, fast tests green. The Linux and macOS side is met by keeping the tests platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 3ea5d466 215dfa67 462da442 20333247.
- 2026-11-13: Doing -> Backlog. Waits on BL-5164: hooks see each hop but not the end of a redirect chain, so a refused redirect cannot be detected
- 2026-11-13: Backlog -> Doing. Criteria at claim: 3ea5d466 215dfa67 462da442 20333247.
- 2026-11-13: Doing -> Done. A refused followed redirect still exports its span, and concurrent followed transfers keep their own trace
