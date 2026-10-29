# ADR-0952: A config file URL's host is sent as the file's own bytes on Windows

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3956 (CGR-059)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's Windows build takes its arguments in the ANSI code page but uses a `-K` file's
bytes raw. A host written unescaped as `ü` in a UTF-8 file reaches libidn2 as `C3 BC`, which it
reads as Windows-1252 (`Ã¼`), the way it reads `%C3%BC`. Measured on 2026-10-28 with
Record-CurlExchange.ps1, `-sS --connect-to ::127.0.0.1:20961 -K k.txt` and
`url = "http://ü.example:20200/a"`: exit 0, empty stdout and stderr, `Host: xn--14-ria7423a.example:20200`.
Curl read the URL as the text `ü`, which `CurlUrlHost` cannot tell from a typed `ü` (ANSI `FC`,
`xn--tda`).

## Decision

`CommandLineOptionTable` re-spells a config file URL's whole authority (user information, host
and port), not only its user information (ADR-0910), with `CommandLineOptions.AsWireText`, so
that encoded in the ANSI code page it gives back the file's bytes. The path, query and fragment
are left as read. A URL typed in an argument, and every URL off Windows, is unchanged.

## Consequences

Curl's exchange for the measured command is byte for byte curl's. A host that is ASCII is
unaffected, since re-spelling ASCII is the identity.
