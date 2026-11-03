# ADR-1200: In-process curl and the extended conformance harness take a stand-in process starter

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4577
- Decided by Claude under Stewart's delegation

## Context

ADR-1184 (BL-4463) gave `EngineComposition.CreateProtocolHandlers` an `extensionProcessStarter` that reaches
`ProtocolHandlerServices.ProcessStarter`, but nothing above the engine passed one, so the extended conformance
cases for `mcp+stdio://` (BL-4483) and the `curl-proto-<scheme>` Plugin (BL-4489) could not run curl without
starting a real process.

## Decision

- `Curl.Console`'s `InProcessCurl` gains a public `RunAsync` overload that takes an `IExtensionProcessStarter`
  after the clock. It reaches the dialing runner through an optional `extensionProcessStarter` on
  `CurlComposition.CreateRunner`, and from there `CreateProtocolHandlers`. The production composition
  (`CreateTransferDispatch`) passes none, so the shipped `curl` keeps each extension's real starter.
- The harness gets a new delegate type, `InProcessCurlProcessRunner` (the existing `InProcessCurlRunner` plus the
  starter), and an `ExtendedConformanceHarness.RunAsync` overload that takes it with a required starter fourth.
  The starter is optional to a case because it chooses the overload. Widening the existing delegate would have
  changed every case file that names `InProcessCurlRunner`. Adding an optional starter to the existing
  `RunAsync` would not work either, because `InProcessCurl.RunAsync` without a starter has no slot for one. A
  separate overload keeps every existing case file compiling unchanged. Both overloads share one private body.
- When a stand-in is given, the extension handlers find their programs in the environment the stand-in's
  `ReadEnvironmentVariable` reads, as ADR-1184 says. The `environment` that curl's own options read is unchanged.

## Consequences

BL-4483 and BL-4489 can run a real command line in process against a stand-in program and add nothing to
`Harness/`.
