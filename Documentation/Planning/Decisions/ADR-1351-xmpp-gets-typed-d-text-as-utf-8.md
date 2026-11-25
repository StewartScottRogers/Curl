# ADR-1351: xmpp gets typed -d text as UTF-8

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5915

## Context

On Windows a typed `-d` argument becomes bytes in the ANSI code page (ADR-0838), as curl sends it. An XMPP stream is
UTF-8, and a body that is not valid UTF-8 is refused (ADR-1350), so `-d café` to `xmpp://` exited 43. The handler sees
only `ITransferContext.PostData`, so it cannot tell typed text from a file's bytes.

## Decision

The command line keeps a second spelling of the body, `CommandLineOptions.PostDataWithTypedTextAsUtf8`, in which each
piece typed on the command line (`-d`, `--data-ascii`, `--data-raw`, `--data-binary`, `--json`, and the name of a
`--data-urlencode` value) is UTF-8. The console gives that body, instead of `PostData`, to a transfer whose URL scheme
is `xmpp` or `xmpps`. Every other scheme gets `PostData`, unchanged.

These keep the bytes they have in `PostData`, so the XMPP handler still refuses them when they are not UTF-8:

- a file's or standard input's bytes (`-d @file`, `--data-binary @file`, `--json @file`);
- a config file line, whose bytes are the file's (`-K`, `.curlrc`);
- an `--expand-` value in which a variable was replaced, which mixes typed text with a variable's bytes;
- the escaped content of `--data-urlencode`, which is ASCII spelling the code page bytes.

## Why

The console already knows the URL when it builds the transfer's settings, so no shared contract changes: an optional
member on `ITransferContext` would have had to be forwarded by every context that wraps another, and one that forgot
would have brought the refusal back. A second body is held only when some piece's two spellings differ, which is only
on Windows. Choosing by who typed the bytes, not by whether they happen to be valid UTF-8, never changes bytes the user
supplied from a file.

## Consequence

A redirect cannot reach `xmpp` under the default redirect protocols, and the choice is made from the transfer's first
URL. A `Libcurl` or `Curl.Api` caller passes bytes, which are sent as given. Another extended protocol that carries
text can be added to the console's choice.
