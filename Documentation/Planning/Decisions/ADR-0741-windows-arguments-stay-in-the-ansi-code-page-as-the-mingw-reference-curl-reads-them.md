# ADR-0741 — Windows arguments stay in the ANSI code page, as the mingw reference curl reads them

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2975
- Amends: ADR-0022 (confirms its argument model against curl 8.22.0)
- Decided by Claude under Stewart's delegation.

## Context

BL-2968 found that upstream test472 (`--aws-sigv4` with a URL path holding `あ`, features `Debug`,
`Unicode`, `aws`) sends `GET /472/a=? HTTP/1.1` from Curl on Windows, because Curl models a curl
that receives its arguments in the system ANSI code page (ADR-0022). A `Unicode` build of curl
(`wmain`, `curlx_convert_tchar_to_UTF8`) reads wide arguments as UTF-8 instead. BL-2975 asked
which model the reference curl follows.

Measured on 2026-10-21 with `Record-CurlExchange.ps1` on an English Windows 11 system (ANSI code
page 1252), arguments `http://127.0.0.1:<port>/472/a=あ`, `-u "äあ:pä"`, `-H "X-A: ä"`:

| curl | `curl -V` lists `Unicode` | Request line | `Authorization` | `X-A` value bytes |
| --- | --- | --- | --- | --- |
| Git for Windows' mingw 8.22.0, the Windows reference (ADR-0018) | no | `GET /472/a=? HTTP/1.1` | `Basic 5D86cOQ=` (`E4 3F 3A 70 E4`, Windows-1252, `あ` best-fitted to `?`) | `E4` |
| `C:\Windows\System32\curl.exe` 8.21.0 | yes | `GET /472/a=%E3%81%82 HTTP/1.1` | `Basic w6TjgYI6cMOk` (UTF-8) | `C3 A4` |

Curl's Windows output for all three already equals the reference's bytes.

The SigV4 half of test472 is not a difference: `AwsSigV4Signer` signs path `/472/a=%E3%81%82` for
service `es` with the case's `Signature=b8783c83...` (canonical URI `/472/a%3D%25E3%2581%2582`, the
percent-encoded path escaped once more), pinned by
`AwsSigV4SignerTests.Sign_Upstream472NonAsciiPathForEs_EscapesThePercentEncodedPathAgainAndGivesTheCasesSignature`.

## Decision

1. **Curl keeps reading arguments in the system ANSI code page on Windows**, best-fitting what the
   code page cannot hold, as ADR-0022 decided. The mingw build is the Windows reference (ADR-0018),
   and it is not a `Unicode` build; switching to UTF-8 would make Curl differ from it in every
   non-ASCII URL, user name and header.
2. **Curl's `curl -V` does not list `Unicode` on Windows**, because the reference does not. The
   conformance harness reports only what Curl's `-V` names, so ADR-0737's decision 2 (`Unicode`
   stays off) stands, and test472, which needs `Unicode`, stays off the ratchet on every platform
   (no curl on Linux or macOS reports `Unicode` either).
3. Moving Curl to the `Unicode` model is a change of the Windows reference build, which needs its
   own ADR superseding ADR-0018; it is not made here.

## Consequences

- No product change: the non-ASCII argument bytes stay as ADR-0022 and ADR-0067 pin them.
- BL-2968's goal of reporting `Unicode` on Windows conflicts with decision 2. Its harness fix for
  test3203 and test1703 (case words read as UTF-8) stands on its own; reporting `Unicode` and
  listing test472 do not.
