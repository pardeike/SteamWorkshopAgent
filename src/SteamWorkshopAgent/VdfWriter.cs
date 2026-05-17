using System.Text;
using System.Text.RegularExpressions;

namespace SteamWorkshopAgent;

public static class VdfWriter
{
    private static readonly Regex FieldRegex = new(
        "^\\s*\"(?<key>(?:\\\\.|[^\"])*)\"\\s*\"(?<value>(?:\\\\.|[^\"])*)\"\\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static string WriteWorkshopItem(IReadOnlyDictionary<string, string> fields, IReadOnlyList<string>? tags = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("\"workshopitem\"");
        builder.AppendLine("{");
        foreach (var field in fields)
            builder.AppendLine($"  \"{Escape(field.Key)}\" \"{Escape(field.Value)}\"");

        if (tags is { Count: > 0 })
        {
            builder.AppendLine("  \"tags\"");
            builder.AppendLine("  {");
            for (var i = 0; i < tags.Count; i++)
                builder.AppendLine($"    \"{i}\" \"{Escape(tags[i])}\"");
            builder.AppendLine("  }");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    public static string Escape(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> ReadWorkshopItemFields(string vdf)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FieldRegex.Matches(vdf))
        {
            var key = Unescape(match.Groups["key"].Value);
            if (key.Equals("workshopitem", StringComparison.OrdinalIgnoreCase))
                continue;

            fields[key] = Unescape(match.Groups["value"].Value);
        }

        return fields;
    }

    public static string? ReadWorkshopItemField(string vdf, string field)
    {
        var fields = ReadWorkshopItemFields(vdf);
        return fields.TryGetValue(field, out var value) ? value : null;
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
                builder.Append(value[i]);
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }
}
