# ADR-1391: HttpProtocolHandler keeps one concern per partial file

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5949)

## Context

`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` held the whole handler, 4522 lines:
connecting, authentication, retries, the response exchange, its `-v` lines and three nested types.
Every task that changed any one of them named the file in its `touches`, so the tasks waited on one
another (BL-5945, BL-5946, BL-5948, BL-5951, BL-5952).

## Decision

- `HttpProtocolHandler` is a partial class. `HttpProtocolHandler.cs` keeps the class summary, the
  constructor, the properties and `ExecuteAsync`.
- Each other concern lives in `HttpProtocolHandler.<Concern>.cs`, named for what it does (for
  example `Connecting`, `AuthorizationRetry`, `ResponseBody`, `ConnectionReuse`), with a comment
  above the class saying what that part holds. The nested types `HttpExchange`, `HttpRequestPlan`
  and `HttpAttemptOutcome` each have their own part (`Exchange`, `RequestPlan`, `AttemptOutcome`).
- A new member goes in the part whose concern it is; a new concern gets a new part. A task names
  only the parts it changes in its `touches`.

## Alternatives not taken

- Separate classes with their own interfaces: the members share the handler's constructor services
  and call one another across concerns, so extracting classes would redesign the handler, with a
  risk of changing behaviour, to gain nothing the board needs.

## Consequences

The split moves code only; no member and no behaviour changes.
