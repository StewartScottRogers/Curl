# ADR-0120 — Protocol libraries may reference the hand-built libraries, and nothing else horizontal

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-667.

Amended by ADR-0185 (2026-09-29, BL-785), as "Adding a hand-built library" below
provides: the table gains `Curl.Zstandard.UnitLibrary`, and `Curl.Tls.UnitLibrary` may
reference it.

## Context

Curl is a complete reimplementation of curl (Stewart, 2026-09-28). Each piece the base
class library lacks is hand-built in its own `Curl.<Area>.UnitLibrary` with its own
`.UnitTests`, held to the same quality gates. Several of those pieces are shared by more
than one protocol - SSH and TLS both need X25519 (ADR-0118), SMB and HTTP both need NTLM,
FTP `--krb`, SASL `GSSAPI` and HTTP Negotiate all need Kerberos, HTTP needs HTTP/2 and
HTTP/3 - so they cannot live inside any one protocol library, because protocol libraries
never reference each other.

Today `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`
(`ProtocolLibrary_References_OnlyAbstractions`) fails any `ProjectReference` of a
`Curl.Protocol.*.UnitLibrary` other than `Curl.Protocol.Abstractions.UnitLibrary`, and
each protocol library's `CLAUDE.md` says it may reference Abstractions "and nothing else
horizontal". A protocol library that needs a hand-built piece therefore cannot reference
it yet. The planned libraries are `Curl.Cryptography` (exists, ADR-0118), `Curl.Ntlm`
(BL-682), `Curl.Kerberos` (BL-685), `Curl.Tls` (BL-696), `Curl.Http2` (BL-715),
`Curl.Quic` (BL-719) and `Curl.Http3` (BL-720), each a `.UnitLibrary`.

`Curl.Protocol.Abstractions.UnitLibrary` references nothing and holds only contracts
(`IConnection`, `IDatagramConnector`, `IDatagramChannel`, the transfer types); it opens
nothing itself.

## Decision

### The allowed list

A `Curl.Protocol.*.UnitLibrary` may reference exactly these projects:

1. `Curl.Protocol.Abstractions.UnitLibrary`, as today.
2. The hand-built libraries:

| Hand-built library | Holds | May reference |
| --- | --- | --- |
| `Curl.Cryptography.UnitLibrary` | Primitives the BCL lacks on a CI platform (ADR-0118) | nothing |
| `Curl.Ntlm.UnitLibrary` | NTLM messages and responses (MS-NLMP) | `Curl.Cryptography.UnitLibrary` |
| `Curl.Kerberos.UnitLibrary` | Kerberos V5 client and the GSS-API Kerberos mechanism | `Curl.Cryptography.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Tls.UnitLibrary` | The hand-built TLS client | `Curl.Cryptography.UnitLibrary`, `Curl.Zstandard.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Http2.UnitLibrary` | HPACK and HTTP/2 framing | `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Quic.UnitLibrary` | The QUIC v1 client transport | `Curl.Tls.UnitLibrary`, `Curl.Cryptography.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Http3.UnitLibrary` | QPACK and HTTP/3 framing | `Curl.Http2.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Zstandard.UnitLibrary` | The Zstandard (RFC 8878) decoder and XXH64 (added by ADR-0185) | nothing |

A protocol library may reference any library on the list that it uses; which protocol
uses which is that protocol's own design, not this ADR's. "May reference" in the table
is an upper bound: a library adds a reference only when its first task that needs it
lands (as BL-682, BL-685, BL-696, BL-719 and BL-720 already say), and the BCL is always
allowed.

### What the hand-built libraries may reference

- Only the BCL and the projects in their own row of the table. The rows form a layered,
  acyclic graph: `Curl.Cryptography` and `Curl.Zstandard` at the bottom; `Curl.Ntlm`, `Curl.Kerberos`,
  `Curl.Tls` and `Curl.Http2` above it; `Curl.Quic` and `Curl.Http3` above those.
- `Curl.Http3` does not reference `Curl.Quic`: QUIC streams reach it as byte streams
  through Abstractions contracts, so HTTP/3 framing is tested without a QUIC stack.
- `Curl.Protocol.Abstractions.UnitLibrary` is allowed where a row names it because it is
  contracts only and references nothing, so it adds no path to a socket. A library uses
  it to take an `IConnection`, a datagram channel or another seam instead of opening one.

### What stays forbidden

- A protocol library referencing another protocol library, `Curl.Networking.UnitLibrary`,
  `Curl.Core.UnitLibrary`, `Curl.Console`, or any other project not on the list above
  (`Curl.Authentication`, `Curl.Cli`, `Curl.Output`, `Curl.Cookies`, `Curl.Conformance`).
- A hand-built library referencing any `Curl.Protocol.*` project other than
  `Curl.Protocol.Abstractions.UnitLibrary`, or `Curl.Networking.UnitLibrary`,
  `Curl.Core.UnitLibrary`, `Curl.Console`, or any project not in its row.
- A hand-built library constructing a `Socket`, `SslStream` or `HttpClient`, or opening
  a file itself. It takes bytes, spans, byte streams or injected seams (a KDC transport,
  a datagram channel, a file reader), with time through `TimeProvider` and randomness
  injected, so every protocol test that uses it stays off the network. It stays
  BCL-only and AOT-compatible like every production project.

Non-protocol libraries (`Curl.Authentication.UnitLibrary` for NTLM and Negotiate,
`Curl.Networking.UnitLibrary` for a hand-built TLS or QUIC connection, `Curl.Console` to
compose them) may reference the hand-built libraries too; this ADR does not restrict
them beyond their own rules, and none of the hand-built libraries may reference them back.

### Enforcement

`ProtocolIsolationTests` in `Curl.Protocol.Abstractions.UnitTests` enforces this ADR.
BL-668 changes it: its protocol check accepts Abstractions and the hand-built libraries
named here and still fails any other reference, from a pure method over reference names
covered by data-row tests; a second check fails a hand-built library whose `csproj`
references anything outside its row (skipped for a library whose `csproj` does not exist
yet). Until BL-668 lands, no protocol library adds a hand-built reference.

### Documentation

Each protocol library's `CLAUDE.md` keeps "nothing else horizontal" until it references
a hand-built library; the task that adds the reference amends that `CLAUDE.md` to name
the hand-built libraries it references. Each hand-built library's `CLAUDE.md` names
what its row allows it to reference.

### Adding a hand-built library

A new hand-built library (for example a hand-built SSH transport or DNS client shared by
more than one protocol) joins the list only by amending this ADR - a new row stating
what it holds and what it may reference, keeping the graph acyclic - and updating
`ProtocolIsolationTests` in the same change. A library not in the table is forbidden
to protocol libraries.

## Consequences

- SSH can use `Curl.Cryptography`, SMB `Curl.Ntlm`, FTP `Curl.Kerberos`, and HTTP
  `Curl.Http2` and `Curl.Http3`, while protocols still never reference each other.
- The dependency graph stays small and acyclic, and one test holds it, so a stray
  reference breaks the fast tests rather than creeping in.
- Allowing Abstractions in hand-built libraries couples them to its contracts: a change
  to `IConnection` or the datagram contracts reaches TLS, QUIC, Kerberos and the HTTP
  framers as well as the protocols. That is already the cost of every contract change.
- Each new hand-built library costs an ADR amendment and a test change; that is the
  point, since each one widens what every protocol may depend on.

## Alternatives considered

- **Let protocol libraries reference any non-protocol library.** `Curl.Networking` and
  `Curl.Core` open sockets and dispatch transfers; referencing them would put the network
  back into protocol tests. Rejected.
- **Move the shared pieces into `Curl.Protocol.Abstractions.UnitLibrary`.** Abstractions
  would stop being contracts only, and every protocol would compile against every
  cryptographic primitive and framer. Rejected.
- **Duplicate each shared piece inside every protocol that needs it.** Two X25519s and
  two NTLMs to keep correct and constant-time. Rejected.
- **Hand-built libraries reference the BCL only, with no Abstractions.** TLS, QUIC and
  Kerberos would each define their own connection and datagram seams duplicating
  Abstractions', and composing them with the protocols would need adapters. Rejected.
- **`Curl.Http3` references `Curl.Quic`.** HTTP/3 framing tests would need a QUIC
  stack; taking streams through Abstractions keeps the two independent. Rejected.
