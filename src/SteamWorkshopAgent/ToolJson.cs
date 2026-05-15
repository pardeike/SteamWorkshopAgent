using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamWorkshopAgent;

public static class ToolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Ok(object data) => JsonSerializer.Serialize(new
    {
        status = "ok",
        data
    }, Options);

    public static string Error(Exception exception) => JsonSerializer.Serialize(new
    {
        status = "error",
        message = exception.Message,
        details = exception.GetType().Name
    }, Options);

    public static async Task<string> TryAsync(Func<Task<object>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (Exception exception)
        {
            return Error(exception);
        }
    }
}
