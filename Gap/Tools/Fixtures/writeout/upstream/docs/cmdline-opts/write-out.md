---
c: Copyright (C) Daniel Stenberg, <daniel@haxx.se>, et al.
SPDX-License-Identifier: curl
Long: write-out
Short: w
Arg: <format>
Added: 6.5
Multi: single
---

# `--write-out`

Self-test write-out document.

## `not_a_variable`

This heading comes before the list, so it is not inventoried.

The variables available are:

## `content_type`
The Content-Type of the requested document, if there was any.

## `header{name}`
The value of the response header named name. (Added in 7.84.0)

## `http_code`
The numerical response code that was found in the last retrieved HTTP(S) or FTP(s)
transfer.

## `time_total`
The total time, in seconds, that the full operation lasted. (Added in 7.9.7)

## `url`
The URL that was fetched last. (Added in 7.75.0)

##

## `after_the_list`

This heading comes after the list ends, so it is not inventoried.
