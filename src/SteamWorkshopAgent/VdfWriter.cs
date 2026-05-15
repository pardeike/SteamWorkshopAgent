using System.Text;

namespace SteamWorkshopAgent;

public static class VdfWriter
{
    public static string WriteWorkshopItem(IReadOnlyDictionary<string, string> fields)
    {
        var builder = new StringBuilder();
        builder.AppendLine("\"workshopitem\"");
        builder.AppendLine("{");
        foreach (var field in fields)
            builder.AppendLine($"  \"{Escape(field.Key)}\" \"{Escape(field.Value)}\"");
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
}
