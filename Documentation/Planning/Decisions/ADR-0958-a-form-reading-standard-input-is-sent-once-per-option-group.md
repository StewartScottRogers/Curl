# ADR-0958: A `-F` form reading standard input is sent once per option group

- Status: Accepted (amends ADR-0062: a second transfer no longer gets a consistent body with an empty part)
- Date: 2026-10-28
- Task: BL-3704 (CGR-175)
- Decided by Claude under Stewart's delegation

## Context

curl 8.22.0's tool builds one `curl_mime` per option group and sets it on every transfer's handle with
`CURLOPT_MIMEPOST`. A `-F f=@-` part reads standard input whole once, while the tool parses the form, and its
length stays the length declared. Measured on curl 8.22.0 Schannel (BL-3704 Notes), with `STDIN-DATA-123` on
standard input:

- Serially, `-F f=@- URL/a URL/b`: `/a` gets the 176-byte form; `/b` sends nothing and fails with exit 26,
  `client mime read EOF fail, only 162/176 of needed bytes read`, `%{size_upload}` 162 and `%{http_code}` 000.
- Under `-Z`, `CURLOPT_MIMEPOST` moves the mime to the newest handle set up, so only the group's last transfer
  sends the form; every earlier one is a `POST` with `Content-Length: 0` and no `Content-Type`. This holds for any
  form (`-F f=value` too) and any `--parallel-max`.

Curl built the form afresh for every transfer, so the second serial transfer sent an empty `f` part and exited 0,
and under `-Z` every transfer sent the form.

## Decision

`CurlCommandRunner` keeps, by option group, the length of the first form it built that holds an `@-` or `<-`
part. A later transfer of the group whose form comes out shorter (standard input drained) fails before anything is
sent with exit 26 and curl's `client mime read EOF fail, only <read>/<needed> of needed bytes read`, its
`%{size_upload}` the bytes read. Under `-Z` a transfer whose URL is not the group's last sends an empty body with
no content type (a mail message's `-F` body is left as it was).

## Consequences

- The serial and parallel repros of CGR-175 match curl 8.22.0.
- Unmodelled: curl's look-ahead window (it sets up about twice `--parallel-max` handles ahead, so with more URLs
  than that an earlier transfer can still send the form), and the glob matches of the last URL, each of which
  is its own handle in curl but all count as the last URL here.
