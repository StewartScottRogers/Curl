# ADR-1012: An extension resolver hook replaces curl's resolver, and refuses `--doh-url` beside it

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4096)

## Context

DNS-over-TLS, DNS-over-QUIC, Oblivious DoH and DNSSEC/DANE extensions (ADR-0999) each need to supply the
`IDnsResolver` a transfer looks host names up with. curl's own `--doh-url` already replaces the system
resolver, so an extension resolver given beside it leaves two answers to one question.

## Decision

- `Curl.Extension.Abstractions.Resolution.IResolverHook` names its activating option and gives its
  resolver over curl's (`ResolverOver(curlResolver)`), so it can resolve its own server's name through
  curl's, as `IConnectionHook` does for connectors (ADR-1010).
- `ResolverHooks.ActivatedBy(values, activators, dohUrlGiven)` keeps the one activated hook. Two or more
  claimants - extension options, or one beside `--doh-url` - throw an `ArgumentException` whose message
  names each (`the options --example-dot and --doh-url each supply the resolver a transfer looks host
  names up with; give only one of them`), which a host turns into curl's option error (exit 2), as for
  two connection hooks. Refusing is simpler and safer than choosing one silently: no curl behaviour
  changes, since `--doh-url` alone keeps working exactly as before.
- `EngineComposition.CreateProtocolHandlers` takes `extensionResolver` (`Func<IDnsResolver, IDnsResolver>`),
  applied before any handler is built; `null` keeps curl's resolver.

## Consequences

A host still has to build `ResolverHooks` from its option group and put `ResolverOver` under the
connectors' look-ups; that wiring is its own task, as BL-4192 is for connection hooks.
