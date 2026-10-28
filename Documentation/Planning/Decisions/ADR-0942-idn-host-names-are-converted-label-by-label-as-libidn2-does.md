# ADR-0942: IDN host names are converted label by label as libidn2 does

- Status: Accepted
- Date: 2026-10-26
- Decided by Claude under Stewart's delegation (BL-3588, CGR-059)

## Context

Curl converted a transfer URL's non-ASCII host with `IdnMapping.GetAscii` on the whole name.
That refused an empty label (`ü..example`) and bidirectional text that libidn2's transitional
fallback accepts (`aא.example`), accepted a label starting with a hyphen (`-ü.example`), and
read a percent-encoded byte that is not UTF-8 as unconvertible everywhere. The reference curl
8.22.0 on Windows is a libidn2 build that looks names up with `idn2_lookup_ul`, which reads the
name in the ANSI code page, so `a%FFb` is `aÿb`, `xn--ab-6ka`. A failed conversion is exit 3
with `URL using bad/illegal format or missing URL` and no `-v` line, not `URL rejected: Bad hostname`.

Measured with `Record-CurlExchange.ps1` and the reference build (curl 8.22.0, libidn2 2.3.8):
`a%FFb` `xn--ab-6ka`, `a%E9b` `xn--ab-bja`, `a%80b` `xn--ab-8fu`, `%FC..example` `xn--tda..example`,
`%FC-x.example` `xn---x-wka.example`, `xn--tda.%FC` `xn--tda.xn--tda`; refused with exit 3:
`-%FC.example`, `%FC-.example`, `-a.%FC.example`, `ab--c.%FC`, `a%81b`, `invalid-utf8-%e2%90.local`.

## Decision

`CurlUrlHost` maps the name with `CurlUrlHostNameMapping`, splits it at `.` and the ideographic
and full-width dots, and converts each label itself (`CurlUrlPunycode`, RFC 3492): an empty label
stays empty; a label, ASCII or not, that starts or ends with `-` or has `--` at its third and
fourth characters (an `xn--` label aside) is refused; a lone surrogate, a control character, the
replacement character `U+FFFD` (UTS #46 disallows it, so libidn2 refuses it too; it is what a byte
that is not UTF-8 becomes when a `-K` config file is read as UTF-8 off Windows, upstream test 1034) or a
label over 63 characters once converted is refused; bidirectional rules are not checked.

On Windows a byte that is not UTF-8 (a lone `U+DC80` to `U+DCFF`) is first read as Windows-1252,
the reference machine's ANSI code page; a byte Windows-1252 leaves undefined stays unconvertible.
Valid UTF-8 stays UTF-8, because Curl receives its command line as Unicode, so a literal `ü`
reaches it as the character the reference build reads from its ANSI argument. Off Windows, as in
a UTF-8 locale, such a byte makes the name unconvertible.

A refused name is `CurlUrlRejection.IdnConversionFailed`, which a transfer reports with the bare
`CURLE_URL_MALFORMAT` text (`TransferPerformer.UrlRejectionText`) and no info line. The URL API
still keeps such a name as written (ADR-0734).

## Consequences

- An `xn--` ASCII label is passed as written; libidn2 decodes and refuses an invalid one
  (`xn--zz.%FC`), which Curl does not yet.
- A percent-encoded valid UTF-8 sequence on Windows is read as UTF-8, where the reference build
  reads its bytes as Windows-1252 (`%C3%BC` is `xn--tda` here and `xn--14-ria7423a` there).
