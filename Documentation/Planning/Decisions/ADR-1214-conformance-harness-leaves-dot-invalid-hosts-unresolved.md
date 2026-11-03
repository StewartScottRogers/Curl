# ADR-1214: The extended conformance harness leaves every host under .invalid unresolved

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4619)

## Context

`ExtendedConformanceHarness` answered 127.0.0.1 for every host name, so no extended conformance case
could reach curl's exit 6 ("Could not resolve host: <name>"). A case needs a way to name a host that
does not resolve, without a network.

## Decision

The harness's resolver answers no address for a host under RFC 6761's reserved `.invalid` name (the
name `invalid` itself, `cache.invalid`, `a.b.invalid.`, in any case), and 127.0.0.1 for every other
host. A case wanting exit 6 names such a host in its URL; there is no new `RunAsync` parameter.

## Why

- RFC 6761 reserves `.invalid` precisely for names guaranteed not to resolve, so a real resolver
  answers the same way: the case reads true against real curl too.
- It needs no new overload or parameter on either `RunAsync`, so every existing case and caller is
  untouched - the simplest thing that makes exit 6 testable.
- Only the last label counts: `invalid.example` still resolves to the reference servers.

## Consequences

- A case can no longer have `.invalid` hosts reach a reference server; none did.
- `Documentation/Conformance/Extended/README.md`'s "A run" section documents the rule.
