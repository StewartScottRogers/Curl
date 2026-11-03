# ADR-1202: The signalr:// client answers a hub call that expects a client result with a Completion error

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4589)

## Context

HubProtocol.md ("Invocation", "Completion") says the receiver of an Invocation that carries an `invocationId` (a
client result) answers it with a Completion for that id. `SignalRHubSession` wrote the hub call as a JSON line and
sent nothing back, so a hub waiting on the result waited for ever (`SignalR:HUB-Invocation-3`). Curl implements no
client methods, so it has no result to give. upstream curl has no SignalR, so there is no curl behaviour to match: the
extension follows its specification (ADR-0999).

## Decision

`SignalRHubSession` still writes the call as its `["target", args...]` line, then sends
`{"type":3,"invocationId":"<id>","error":"Client didn't provide a result."}`. The error text is the one ASP.NET Core's
own .NET client (`HubConnection`) sends when it has no handler that returns a result, so a hub sees from curl exactly
what it sees from the reference client and its `InvokeAsync` fails with the familiar message. An Invocation without
an `invocationId` is answered with nothing, as before. The transfer's exit code is unchanged.

## Consequences

A hub that calls a client for a result gets an immediate failure instead of hanging. The text is pinned in
`SignalRHubSession.NoClientResultError` and by `SignalRProtocolHandlerTests`.
