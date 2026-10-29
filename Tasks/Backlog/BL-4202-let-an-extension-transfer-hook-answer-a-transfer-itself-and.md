---
id: BL-4202
title: Let an extension transfer hook answer a transfer itself and replace curl's response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4202 — Let an extension transfer hook answer a transfer itself and replace curl's response

## Goal

An extension's `ITransferHook` can answer a transfer without running curl's handler (a fresh cached response) and can replace the response curl's handler received (a stored response in place of a 304), with curl's handler, output and `-i`/`-D` header output unchanged when no hook does so.

## Context

Found by BL-4133 (`--cache-dir`, RFC 9111) in its prerequisite pass. ADR-1007's `ITransferHook` can only add request headers, pass the body through a stream, read the result and repeat the transfer (ADR-1009, BL-4174); `TransferHookingProtocolHandler` always runs curl's handler, and the body stream is flushed before `TransferFinished`, so it never knows the status. RFC 9111 §4.2 (serve a fresh stored response without contacting the origin) and §4.3.4 (on 304 Not Modified, send the stored response, updated) need both. Suggested shape (decide in an ADR): a default interface member such as `ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) => next(context)`, run outermost-first in `TransferHookingProtocolHandler`, so a hook can skip `next`, or call it with a context whose `Output`/`HeaderOutput` it buffers and then write what it chooses. Start at `Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs` and `HookedTransferContext.cs`.

## Acceptance criteria

- [ ] A transfer hook can produce the transfer's `TransferResult`, body and header output without curl's handler running; unit test with a fake handler that fails if called.
- [ ] A transfer hook can run curl's handler with its body and header output captured and then write a different response to the transfer's output and return a different `TransferResult`; unit test.
- [ ] With no hook using the new member, every existing `Curl.Extension.Abstractions.UnitTests` test passes unchanged.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the member's shape and order, and `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` describes it.
- [ ] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
