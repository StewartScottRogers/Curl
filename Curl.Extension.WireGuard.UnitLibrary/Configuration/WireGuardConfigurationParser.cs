namespace Curl.Extension.WireGuard.Configuration;

/// <summary>
/// Parses a wg-quick configuration file (wg(8) "CONFIGURATION FILE FORMAT", wg-quick(8)): one <c>[Interface]</c>
/// section and one or more <c>[Peer]</c> sections of <c>key = value</c> lines, where <c>#</c> starts a comment and
/// section names and keys are case-insensitive.
/// </summary>
public static class WireGuardConfigurationParser
{
    /// <summary>Parses the text of a configuration file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The interface and its peers.</returns>
    /// <exception cref="WireGuardConfigurationException">The file is not a valid configuration; the message names the line.</exception>
    public static WireGuardConfiguration Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        var reader = new SectionReader();
        for (var index = 0; index < lines.Length; index++)
        {
            reader.ReadLine(lines[index], index + 1);
        }

        return reader.Finish(lines.Length);
    }

    private sealed class SectionReader
    {
        private readonly List<WireGuardPeerSectionBuilder> peers = [];
        private WireGuardInterfaceSectionBuilder? interfaceSection;
        private IWireGuardSectionBuilder? currentSection;

        public void ReadLine(string rawLine, int lineNumber)
        {
            var comment = rawLine.IndexOf('#');
            var line = (comment < 0 ? rawLine : rawLine[..comment]).Trim();
            if (line.Length == 0)
            {
                return;
            }

            if (line[0] == '[')
            {
                currentSection = StartSection(line, lineNumber);
                return;
            }

            AddSetting(line, lineNumber);
        }

        public WireGuardConfiguration Finish(int lastLineNumber)
        {
            if (interfaceSection is null)
            {
                throw new WireGuardConfigurationException(lastLineNumber, "the file has no [Interface] section");
            }

            if (peers.Count == 0)
            {
                throw new WireGuardConfigurationException(lastLineNumber, "the file has no [Peer] section");
            }

            var configuredInterface = interfaceSection.Build();
            return new WireGuardConfiguration(configuredInterface, peers.Select(peer => peer.Build()).ToList());
        }

        private IWireGuardSectionBuilder StartSection(string line, int lineNumber)
        {
            var name = line[^1] == ']' ? line[1..^1].Trim() : line;
            if (name.Equals("Peer", StringComparison.OrdinalIgnoreCase))
            {
                var peer = new WireGuardPeerSectionBuilder(lineNumber);
                peers.Add(peer);
                return peer;
            }

            if (!name.Equals("Interface", StringComparison.OrdinalIgnoreCase))
            {
                throw new WireGuardConfigurationException(lineNumber, $"unknown section '{line}'");
            }

            if (interfaceSection is not null)
            {
                throw new WireGuardConfigurationException(lineNumber, "a second [Interface] section");
            }

            interfaceSection = new WireGuardInterfaceSectionBuilder(lineNumber);
            return interfaceSection;
        }

        private void AddSetting(string line, int lineNumber)
        {
            var equals = line.IndexOf('=');
            if (currentSection is null || equals <= 0)
            {
                var problem = currentSection is null ? "is outside a section" : "is not 'key = value'";
                throw new WireGuardConfigurationException(lineNumber, $"'{line}' {problem}");
            }

            currentSection.AddSetting(line[..equals].Trim(), line[(equals + 1)..].Trim(), lineNumber);
        }
    }
}
