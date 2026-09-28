# ADR-0067 — Command-line header text becomes bytes in the platform curl's encoding, inside the HTTP formatter

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"; task BL-252).

## Context

curl copies some command-line text into the request head as it is, most visibly each
`-H`/`--header` value (and `--proxy-header`, `-A`, `-e`). curl works on the bytes of its
narrow `argv`, so which bytes a non-ASCII character becomes depends on how the platform's
curl receives its arguments. Curl receives every argument as a UTF-16 .NET `string`
(`CommandLineOptions.Headers` is `IReadOnlyList<string>`), so it must choose the bytes itself.

Measured in BL-172 with the Windows reference build (ADR-0018: curl 8.21.0, mingw), on a
system whose ANSI code page is Windows-1252, sending `-H` values to a loopback server:

| Character | Byte sent |
| --- | --- |
| `é` (U+00E9) | `0xE9` |
| `Ā` (U+0100) | `A` (`0x41`), Windows' best-fit mapping |
| `€` (U+20AC) | `0x80`, Windows-1252's own byte for it |
| `中` (U+4E2D) | `?` (`0x3F`), no best fit exists |

The mingw build receives its arguments in the system ANSI code page, with Windows' best fit
for characters the code page lacks, and sends those bytes unchanged. curl on Linux and macOS
receives the argument's bytes as the shell passed them, which in a UTF-8 locale are UTF-8.

`HttpRequestHeadFormatter` (`Curl.Protocol.Http.UnitLibrary`) builds the head as a `string` and
turns it into bytes with `Encoding.Latin1`, one byte per character. That matches `é`, U+0100
(Latin-1's encoder best-fits it to `A`) and `中`, but sends `?` for `€`, because Windows-1252's
`0x80`-`0x9F` characters are not Latin-1's. The head also holds text that is already one
character per byte: the request target (`HttpUrlText.RequestTarget`, and `--request-target`
as its UTF-8 bytes) and the Bearer token (ADR-0022). So the whole head cannot simply be
encoded in another encoding; only the command-line text in it can.

The encoding question was already settled for credentials by ADR-0022:
`CredentialEncoding.ForPlatform(isWindows)` in `Curl.Authentication.UnitLibrary` gives the system
ANSI code page on Windows (`CodePagesEncodingProvider.Instance.GetEncoding(0)`) and UTF-8
elsewhere, and `Curl.Console` passes it to the authenticator.

### Base class library only: the CodePages provider and best fit

`System.Text.CodePagesEncodingProvider` ships in the .NET shared framework, so reaching the
ANSI code pages needs no package (root `CLAUDE.md`, "Base class library only"). The concern
raised when this task was filed was that .NET's code-page encoders do not perform Windows'
best-fit mapping, so an exact match for `Ā` -> `A` would need a hand-rolled best-fit table or a
P/Invoke to `WideCharToMultiByte`. The repository already shows otherwise for Windows-1252:
`BasicAndBearerAuthenticatorTests` pins `-u Ω中Ā:p` to `Basic Tz9BOnA=`, the bytes
`4F 3F 41 3A 70` (`O?A:p`) the reference sent, through the provider's Windows-1252 encoder with
its default replacement fallback. The provider's encoders carry the best-fit data themselves,
so neither a table nor a P/Invoke is needed, and nothing here touches native-AOT compatibility
beyond what ADR-0022 already ships.

## Decision

- **Windows:** command-line text in the request head is encoded in the system ANSI code page,
  with the provider's best fit and `?` for characters with none: the encoding
  `CredentialEncoding.ForPlatform(isWindows: true)` gives. On a Windows-1252 system this sends
  exactly the four measured bytes above.
- **Linux and macOS:** UTF-8, the bytes curl receives in a UTF-8 locale:
  `CredentialEncoding.ForPlatform(isWindows: false)`.
- **Which layer owns it:** the HTTP formatter in `Curl.Protocol.Http.UnitLibrary`, not
  `Curl.Cli.UnitLibrary`. `Curl.Cli` keeps every option value as a .NET `string` (as ADR-0064
  records), and a `string` is what crosses the boundary in `HttpRequestOptions.Headers` and
  `ProxyHeaders`. `Curl.Console`, the composition root that already knows the platform, puts the
  `System.Text.Encoding` it gives the authenticator onto `HttpRequestOptions` as the encoding for
  command-line text. The formatter encodes each command-line text in that encoding and writes the
  resulting bytes into the head one character per byte, which its closing `Encoding.Latin1` step
  then puts on the wire unchanged. Text that is already bytes (the request target, the Bearer
  token) is left as it is. When no encoding is set the formatter keeps `Encoding.Latin1`, today's
  behaviour, so a caller that does not set it is not changed.
- **One encoding for credentials and headers.** Headers use the same platform encoding as
  Basic and Bearer credentials, so `-u` and `-H` agree on the bytes of the same text.

This decision changes code; the implementation is follow-up task BL-373 (see BL-252's Notes).

## Consequences

- Once implemented, `-H` bytes match the reference for every character on a Windows-1252
  system, including the `0x80`-`0x9F` characters such as `€`, and match curl on Linux and
  macOS in a UTF-8 locale, where Latin-1 today sends `?` for anything above U+00FF.
- A Windows system with another ANSI code page gets that code page, as the reference does.
- `Curl.Protocol.Http.UnitLibrary` gains no reference: `System.Text.Encoding` is in the base
  class library, and the formatter still never learns which platform it runs on.
- Scope: this ADR decides request-head text. Request bodies (`-d`, `-F` literal values) and URLs
  are not decided here; they are UTF-8 today, and a change for them needs its own measurement.
- **Consistency with earlier text-to-bytes decisions.** ADR-0022 (credentials) is the same rule,
  and this ADR reuses its encoding. ADR-0004 (the `-T` file name appended to a URL, UTF-8
  everywhere) and ADR-0064 (`--variable` content, UTF-8) are not consistent with it on Windows:
  both chose UTF-8 where the mingw reference uses the ANSI code page. ADR-0004 was written against
  the System32 build before ADR-0018 made mingw the reference, and ADR-0064 records the difference
  as a known consequence. Neither is superseded here: they concern the URL and option expansion,
  which are outside this ADR's scope.

## Alternatives considered

- **Keep `Encoding.Latin1` on every platform.** Simplest, and already right for `é`, U+0100 and
  `中` on Windows, but wrong for `€` and the rest of Windows-1252's `0x80`-`0x9F` block, and wrong
  on Linux and macOS for every character above U+007F, where curl sends UTF-8.
- **Convert in `Curl.Cli.UnitLibrary`, handing the formatter bytes.** Puts the platform choice in
  the argument parser, which would have to know the platform, and changes the type of the header
  options and every option that reaches the head, the same cost ADR-0064 declined for variables.
- **A hand-rolled best-fit table, or a P/Invoke to `WideCharToMultiByte`.** Both would reproduce
  Windows' best fit, but `CodePagesEncodingProvider` already does, as the pinned `Tz9BOnA=` test
  shows; a P/Invoke would also add a Windows-only native path to an AOT-published binary.
- **UTF-8 everywhere.** Matches Linux, macOS and ADR-0004, but sends two bytes for `é` where the
  Windows reference sends `0xE9`: a different header for any non-ASCII `-H` on Windows.
