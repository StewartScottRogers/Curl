namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>How a round's option value is passed to <c>curl_easy_setopt</c>, the tester's kind letter.</summary>
public enum LibcurlEasyOptionKind
{
    /// <summary><c>l</c>: a <c>long</c> (or <c>curl_off_t</c> for options from 30000).</summary>
    Number,

    /// <summary><c>s</c>: a string, or <c>NULL</c>.</summary>
    Text,

    /// <summary><c>L</c>: a <c>curl_slist</c> of strings.</summary>
    List,

    /// <summary><c>W</c>: the write callback that collects the round's body.</summary>
    WriteCallback,

    /// <summary><c>H</c>: the header callback that collects the round's headers.</summary>
    HeaderCallback,
}
