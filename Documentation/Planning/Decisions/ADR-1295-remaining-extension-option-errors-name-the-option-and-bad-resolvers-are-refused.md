# ADR-1295: The remaining extension option errors name the option and carry no .NET text, and bad DoT and DoQ resolvers are refused (BL-5406)

Decided by Claude under Stewart's delegation.

## Status

Accepted

## Context

After ADR-1289 and ADR-1070, five option errors still differed: `--wireguard` and `--require-sct` printed the `IOException` text with a full local path, `--sigstore-identity` and `--oauth2-device` without their companion failed at transfer time as `curl: (2) ...` with no try-help line, `--http-file` and `--openapi` printed `http-file: cannot read x` with exit 26, and `--dns-over-tls "::bad::"` was accepted because any value with a colon was taken as a bare IPv6 address.

## Decision

- `--wireguard` and `--require-sct` say `--<option> cannot read ...'<file as given>'`; neither carries exception text or a directory. A log list that is not JSON says `--require-sct: '<file>' is not a valid log list`.
- `--sigstore-*` companions without `--sigstore-bundle`, and an OAuth2 flow option without its companion, are refused when the hooks are activated (an `ArgumentException`, which the host prints as `curl: <message>`, try-help line, exit 2). The hooks keep their own transfer-time checks for hooks built directly.
- `--http-file` and `--openapi` say `--<option> cannot read '<file>'` and exit 2 (not 26), as the other refusals do.
- A resolver value must be a host name (letters, digits, `.`, `-`, `_`, not starting with `-` or `.`), an IPv4 address, a bracketed IPv6 address or a bare IPv6 address that parses.

## Consequences

The try-help line after an expander refusal (`--http-file`, `--openapi`) needs a change in `Curl.Console/CurlCommandRunner.cs`, which another task held when this was done; it is filed separately.
