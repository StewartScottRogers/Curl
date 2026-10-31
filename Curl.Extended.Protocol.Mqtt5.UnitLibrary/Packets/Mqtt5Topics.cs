namespace Curl.Extended.Protocol.Mqtt5.Packets;

/// <summary>
/// The topic rules of MQTT 5: topic names and filters with the wildcards of section 4.7, and shared subscription
/// filters <c>$share/{ShareName}/{filter}</c> of section 4.8.2.
/// </summary>
internal static class Mqtt5Topics
{
    /// <summary>What every shared subscription filter starts with.</summary>
    public const string SharedSubscriptionPrefix = "$share/";

    /// <summary>A topic name is at least one character and holds no wildcard; otherwise Topic Name invalid (0x90).</summary>
    public static void ValidateTopicName(string topicName)
    {
        if (topicName.Length == 0 || topicName.AsSpan().IndexOfAny('+', '#') >= 0)
        {
            throw new Mqtt5MalformedPacketException(Mqtt5ReasonCode.TopicNameInvalid, $"\"{topicName}\" is not a topic name: empty or holds a wildcard.");
        }
    }

    /// <summary>
    /// A topic filter is at least one character; '#' stands alone as the last level and '+' alone in a level; a shared
    /// subscription's ShareName and filter follow <see cref="ParseSharedSubscription"/>. Otherwise Topic Filter invalid (0x8F).
    /// </summary>
    public static void ValidateTopicFilter(string topicFilter)
    {
        string filter = IsSharedSubscription(topicFilter) ? ParseSharedSubscription(topicFilter).TopicFilter : topicFilter;
        if (filter.Length == 0 || !AreLevelsValid(filter.Split('/')))
        {
            throw FilterInvalid($"\"{topicFilter}\" is not a topic filter: empty or a wildcard is not alone in its level.");
        }
    }

    /// <summary>Whether <paramref name="topicFilter"/> is a shared subscription, starting <c>$share/</c>.</summary>
    public static bool IsSharedSubscription(string topicFilter) => topicFilter.StartsWith(SharedSubscriptionPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Splits <c>$share/{ShareName}/{filter}</c>. The ShareName is at least one character holding no '/', '+' or '#',
    /// and a '/' follows it; otherwise Topic Filter invalid (0x8F).
    /// </summary>
    public static (string ShareName, string TopicFilter) ParseSharedSubscription(string topicFilter)
    {
        int slash = topicFilter.IndexOf('/', SharedSubscriptionPrefix.Length);
        if (slash <= SharedSubscriptionPrefix.Length)
        {
            throw FilterInvalid($"\"{topicFilter}\" has no ShareName followed by '/'.");
        }

        string shareName = topicFilter[SharedSubscriptionPrefix.Length..slash];
        return shareName.AsSpan().IndexOfAny('+', '#') < 0
            ? (shareName, topicFilter[(slash + 1)..])
            : throw FilterInvalid($"ShareName \"{shareName}\" holds a wildcard.");
    }

    private static bool AreLevelsValid(string[] levels)
    {
        for (int index = 0; index < levels.Length; index++)
        {
            if (!IsLevelValid(levels[index], index == levels.Length - 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLevelValid(string level, bool isLastLevel) =>
        level.Contains('#', StringComparison.Ordinal)
            ? level == "#" && isLastLevel
            : !level.Contains('+', StringComparison.Ordinal) || level == "+";

    private static Mqtt5MalformedPacketException FilterInvalid(string message) => new(Mqtt5ReasonCode.TopicFilterInvalid, message);
}
