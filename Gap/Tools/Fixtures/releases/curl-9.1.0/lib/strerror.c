/* self-test fixture */
const char *curl_easy_strerror(CURLcode error)
{
  switch(error) {
  case CURLE_OK:
    return "No error";

  case CURLE_UNSUPPORTED_PROTOCOL:
    return "Unsupported protocol";

  case CURLE_FAILED_INIT:
    return "Failed initialization";

  case CURLE_URL_MALFORMAT:
    return "Quote \"x\" and \\ here";

  case CURLE_NOT_BUILT_IN:
    return "A feature was not built-in"
           " in this libcurl.";

  case CURLE_COULDNT_RESOLVE_PROXY:
    return "Could not resolve proxy name";

  case CURLE_COULDNT_RESOLVE_HOST:
    return "Could not resolve host name";

    /* error codes not used by current libcurl */
  default:
    break;
  }
  return "Unknown error";
}

const char *curl_multi_strerror(CURLMcode error)
{
  switch(error) {
  case CURLM_BAD_HANDLE:
    return "Invalid multi handle";
  default:
    break;
  }
  return "Unknown error";
}
