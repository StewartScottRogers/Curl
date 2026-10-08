# `--config`

Options and their parameters must be specified on the same line in the file,
separated by whitespace, colon, or the equals sign.

If the first non-blank column of a config line is a '#' character, that line
is treated as a comment.

Within double quotes the following
escape sequences are available: \, \".

1) **"$CURL_HOME/.curlrc"** (Added in 7.10.3)

2) Windows: **"%APPDATA%\.curlrc"**

3) Non-Windows: use getpwuid to find the home directory
