# ADR-0867 — `-r` appends a dash after any leading number not before a dash, and SFTP reads the `-r` text

- **Status:** Accepted
- **Date:** 2026-10-28

Decided by Claude under Stewart's delegation (task BL-3660, CGR-131).

## Context

CGR-131 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found two differences for
`sftp://` with `-r`. curl 8.22.0 gives exit 33, `Requested range was not delivered by the server`,
with nothing written, for `0-1,5-6`, `0-9x`, `0-1,3`, `1-2-3` and `0-0,0-0`, and the whole file for
`0 - 9` and `0x10-0x20`. Curl read the first range of the first five and 10 bytes for `0 - 9`, and
gave exit 33 for `0x10-0x20`.

Measured on 2026-10-26 against curl 8.22.0 (Schannel) over `file://` on a 292-byte file: `0 - 9`,
`0x10-0x20` and `0 -9` print `A specified range MUST include at least one dash (-). Appending one
for you` and read the whole file; `5 ` reads from byte 5; `0-9x` and `-5 x` print the
invalid-character warning; `1-2-3` and `0-1,5-6` print nothing; `99999999999999999999 - 3` prints the
invalid-character warning and keeps the text.

## Decision

1. `CommandLineOptionTable.SetRange` appends a dash, with the warning, whenever the value starts
   with a number that fits a `long` and the character after it is not `-`, as curl 8.22.0's tool
   does with `curlx_str_number`. Otherwise it keeps the text, with the invalid-character warning
   when due. Before, it appended only when the value held no dash anywhere.
2. `SftpDownloadPart.Choose` reads the `-r` text with its port of `Curl_ssh_range` whenever the text
   is given, and uses the parsed `ByteRange` only when there is no text. `ByteRangeParser` keeps
   ignoring trailing text, which is right for `file://` and FTP.

## Consequences

`-r "0 - 9"` now reads from byte 0 on every protocol, as the curl tool does. An SFTP range with text
after the first range is exit 33 and reads nothing. `scp://` still ignores `-r`, as curl does.
