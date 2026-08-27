namespace HomeAssistantX.Recorder;

/// <summary>Normalizes Recorder entity globs that can match canonical Home Assistant entity identifiers.</summary>
internal static class HomeAssistantRecorderEntityGlob
{
    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var candidate = value!.Trim();
        if (!string.Equals(candidate, candidate.ToLowerInvariant(), StringComparison.Ordinal)) return false;

        var separator = candidate.IndexOf('.');
        if (separator <= 0 || separator != candidate.LastIndexOf('.') || separator == candidate.Length - 1) return false;
        if (!IsPatternSegment(candidate, 0, separator) || !IsPatternSegment(candidate, separator + 1, candidate.Length)) return false;

        normalized = candidate;
        return true;
    }

    private static bool IsPatternSegment(string value, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            var character = value[index];
            if (IsSlugCharacter(character) || character is '*' or '?') continue;
            if (character != '[') return false;

            var closing = value.IndexOf(']', index + 1);
            if (closing < 0 || closing >= end) return false;
            var content = index + 1;
            if (content < closing && value[content] == '!') content++;
            if (content >= closing) return false;
            for (var classIndex = content; classIndex < closing; classIndex++)
            {
                var classCharacter = value[classIndex];
                if (!IsSlugCharacter(classCharacter) && classCharacter != '-') return false;
            }
            index = closing;
        }

        return true;
    }

    private static bool IsSlugCharacter(char value)
        => value is >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
}
