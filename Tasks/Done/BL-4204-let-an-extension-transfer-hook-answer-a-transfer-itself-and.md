---
id: BL-4204
title: Let an extension transfer hook answer a transfer itself and replace curl's response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4204 — Let an extension transfer hook answer a transfer itself and replace curl's response

## Goal

An extension's `ITransferHook` can answer a transfer without running curl's handler (a fresh cached response) and can replace the response curl's handler received (a stored response in place of a 304), with curl's handler, output and `-i`/`-D` header output unchanged when no hook does so.

## Context

Found by BL-4133 (`--cache-dir`, RFC 9111) in its prerequisite pass. ADR-1007's `ITransferHook` can only add request headers, pass the body through a stream, read the result and repeat the transfer (ADR-1009, BL-4174); `TransferHookingProtocolHandler` always runs curl's handler, and the body stream is flushed before `TransferFinished`, so it never knows the status. RFC 9111 §4.2 (serve a fresh stored response without contacting the origin) and §4.3.4 (on 304 Not Modified, send the stored response, updated) need both. Suggested shape (decide in an ADR): a default interface member such as `ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) => next(context)`, run outermost-first in `TransferHookingProtocolHandler`, so a hook can skip `next`, or call it with a context whose `Output`/`HeaderOutput` it buffers and then write what it chooses. Start at `Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs` and `HookedTransferContext.cs`.

## Acceptance criteria

- [x] A transfer hook can produce the transfer's `TransferResult`, body and header output without curl's handler running; unit test with a fake handler that fails if called.
- [x] A transfer hook can run curl's handler with its body and header output captured and then write a different response to the transfer's output and return a different `TransferResult`; unit test.
- [x] With no hook using the new member, every existing `Curl.Extension.Abstractions.UnitTests` test passes unchanged.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the member's shape and order, and `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` describes it.
- [x] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `ITransferHook.ExecuteAsync(context, next) => next(context)`, run per run of the transfer (repeats included) outermost first in request order, curl's handler innermost, inside the body streams and before `TransferFinished`/`RepeatAfter` (ADR-1019). A public `TransferContextOutputs.WithOutputs` extension lets a hook run `next` with its own body, `-i` and `-D` outputs; `HookedTransferContext` now carries both header outputs.
- Tests: `TransferHookingProtocolHandlerTests.Execute.cs` (a partial of the existing class, one test class per production class) and `TransferContextOutputsTests.cs`; 41 tests in Curl.Extension.Abstractions.UnitTests pass, the existing ones unchanged except the class made partial.
- Measure-CodeQuality.ps1: Curl.Extension.Abstractions.UnitLibrary 100% line, 100% branch, 0 failing members. Full build clean, fast tests green (52 test assemblies).
- Changes are platform-neutral (in-memory streams only).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c05685ad ae76495d e36ca50f 24779316 e7c4212f 3d836d5e.
- 2026-10-29: Doing -> Done. An extension transfer hook can answer a transfer itself or capture curl's response and replace it (ITransferHook.ExecuteAsync, ADR-1019)
