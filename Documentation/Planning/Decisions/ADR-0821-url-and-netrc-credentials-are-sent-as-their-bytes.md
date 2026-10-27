# ADR-0821: A URL's percent-decoded credentials and a netrc file's are sent as their bytes

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3581, CGR-052)
- Relates to: [ADR-0022](ADR-0022-basic-and-bearer-credentials-are-sent-as-the-platform-curl-sends-them.md)

## Context

ADR-0022 sends credentials in the platform's encoding: the system ANSI code page on Windows, where the
reference curl receives its arguments in it, UTF-8 elsewhere. `TransferCredentialLookup` percent-decoded
a URL's user name and password as UTF-8 and read the netrc file as UTF-8, so on Windows the
authenticators re-encoded them to the ANSI code page. Measured on 2026-10-26 with `Record-CurlExchange.ps1`
against curl 8.22.0 Schannel on Windows:

| Command | curl 8.22.0 | Curl before |
| --- | --- | --- |
| `curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20581/a"` | `Basic w6k64oKs` (C3 A9 3A E2 82 AC) | `Basic 6TqA` (E9 3A 80) |
| the same URL, raw UTF-8, in a `-K` file | `Basic w6k64oKs` | |
| `curl -sS "http://é@127.0.0.1:20581/a"` | `Basic 6To=` (E9 3A) | |
| `--netrc-file` with UTF-8 `login é password €` | `Basic w6k64oKs` | |

curl decodes `%XX` to bytes and sends bytes; only text that came in as an argument is in the ANSI code page.

## Decision

`TransferCredentialLookup` takes the credential encoding the authenticators use (the platform's by default).
It turns the URL's user information into bytes in that encoding, replaces each `%XX` with its byte, and
reads the bytes back through the same encoding; it reads the netrc file through it too. The authenticator's
encode then gives back exactly the decoded bytes and the file's bytes, while a raw argument character still
goes in the ANSI code page. The control-code check decodes as Latin-1, so it sees bytes. A `%` not followed by
two hex digits is kept, as `Curl_urldecode` keeps it.

## Consequences

Windows sends URL and netrc credentials byte for byte as curl does; Linux and macOS are unchanged for valid
UTF-8. A byte sequence the system code page cannot round-trip (a double-byte code page) may still differ.
