# ADR-1152 — --structured-fields shows known RFC 9651 fields in -v and -w

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4140.

## Context

BL-4140 adds an extension (ADR-0999) in `Curl.Extension.StructuredFields.UnitLibrary` that parses the known
Structured Fields of a response and shows them readably in verbose output and `-w`. curl has no such option, so the
extension is tested against RFC 9651 (September 2024, which obsoletes RFC 8941 and adds the Date and Display String
types), the version followed, and never changes what curl does without the option.

## Decision

- **Option.** One flag, `--structured-fields`, with no argument; curl has no option of that name. It is a transfer
  hook (ADR-1007) registered in the option, description and transfer hook catalogues; it has no URL scheme, so it
  adds no line to `ExtensionCatalogue`.
- **Known fields.** The response fields whose specifications define them as Structured Fields, with the top-level
  type each gives: `Accept-CH`, `Cache-Groups`, `Cache-Status`, `Critical-CH`, `Proxy-Status` (Lists);
  `Accept-Signature`, `CDN-Cache-Control`, `Content-Digest`, `Permissions-Policy`, `Priority`,
  `Reporting-Endpoints`, `Repr-Digest`, `Signature`, `Signature-Input`, `Use-As-Dictionary`, `Want-Content-Digest`,
  `Want-Repr-Digest` (Dictionaries); `Cross-Origin-Embedder-Policy`, `Cross-Origin-Opener-Policy` (and their
  `-Report-Only` forms) and `Origin-Agent-Cluster` (Items). Names match in any case; every line of one name is
  joined with `, ` (§4.2) after its surrounding spaces and tabs are trimmed.
- **Parsing (§4.2).** Every algorithm of §4.2.1 to §4.2.10, as written: non-ASCII input fails, a repeated
  Dictionary key or parameter keeps its first place and takes its last value, unpadded base64 is accepted
  (§4.2.7), a Date must be an Integer, a Display String must be lowercase-percent-encoded UTF-8.
- **Readable form.** One line per field that names every type: `List of 2: Token ExampleCache [hit=Boolean true];
  Token CDN`, `Dictionary of 1: u=Integer 3`, `Inner List (Token a, Token b)`, `String "a\"b"`,
  `Byte Sequence 0x616263 (3 bytes)` (hex reads better than base64 for digests), `Date 2022-08-04T01:57:13Z`
  (`Date @<seconds>` past what an ISO 8601 date can show), `Decimal 0.5`, `Display String "füü"`. A value that does
  not parse reads `not a valid <type> (<why>), ignored`, since §4.2 has a recipient ignore it.
- **Verbose.** After the transfer each known field present is reported as the info line
  `* Structured field <name>: <readable>`, in the order the response gave the fields, with the name as the server
  wrote it. Like every info line it shows only under `-v` and `--trace`.
- **Write-out (ADR-1115).** `%{structured_fields}` holds every known field's `<name>: <readable>` line joined with
  newlines (empty when none was present), and `%{structured_field.<lowercase name>}` one field's readable form. curl
  has neither variable, so none of curl's is shadowed; a field not present stays an unknown variable.

## Left out

- **Request fields** (`Sec-CH-UA`, `Client-Cert`, `Available-Dictionary`, `Idempotency-Key`): curl's `-v` already
  shows what Curl sent, and they are rarely in a response.
- **Draft-only fields** (`RateLimit`, `Upload-Offset`): their type may still change; they join the table when
  published as an RFC.
- **Serialising** (§4.1): the extension only reads fields; nothing it sends is a Structured Field.

## Consequences

Without `--structured-fields` the hook is never activated, so every transfer is unchanged; with
`CURL_EXTENSIONS=0` the option is not registered and is refused as an unknown option (exit 2).
