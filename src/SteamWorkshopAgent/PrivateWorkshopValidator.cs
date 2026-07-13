using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace SteamWorkshopAgent;

public sealed class PrivateWorkshopValidator(
    SteamEnvironment steamEnvironment,
    ProcessRunner processRunner,
    WorkshopPublishRequestStore requestStore,
    SteamworksPublisher publisher)
{
    private const ulong PublicZombielandItemId = 928376710;
    private static readonly string MetadataPath = Path.Combine(AgentPaths.AppSupportDirectory, "private-validation-item.json");
    private static readonly string FixtureRoot = Path.Combine(AgentPaths.AppSupportDirectory, "private-validation-content");

    public async Task<object> ValidateAsync(bool confirm)
    {
        if (!confirm)
        {
            return new
            {
                confirmed = false,
                visibility = "private",
                title = "SteamWorkshopAgent Private Validation",
                metadataPath = MetadataPath,
                message = "Pass confirm=true to create or update only the dedicated private validation item."
            };
        }

        var contentFolder = Path.Combine(FixtureRoot, "Content");
        var previewFile = Path.Combine(FixtureRoot, "Preview.png");
        CreateFixture(contentFolder, previewFile);

        var probe = await publisher.ProbeAsync();
        if (!probe.Ready || probe.SteamId is not { } steamId)
            throw new InvalidOperationException($"The detached Steamworks session is not ready: {probe.Message}");

        var metadata = ReadMetadata();
        var reused = metadata != null;
        PrivateWorkshopItemCreationResult? creation = null;
        ulong publishedFileId;
        if (metadata != null)
        {
            if (metadata.PublishedFileId == PublicZombielandItemId)
                throw new InvalidOperationException("Refusing to use the public Zombieland Workshop item for validation.");
            if (metadata.SteamId != steamId)
                throw new InvalidOperationException("The stored private validation item belongs to a different Steam account.");
            publishedFileId = metadata.PublishedFileId;
        }
        else
        {
            creation = await CreatePrivateItemAsync();
            if (!creation.Success || creation.PublishedFileId is not { } createdId || createdId == 0)
                throw new InvalidOperationException(creation.Message);
            if (createdId == PublicZombielandItemId)
                throw new InvalidOperationException("Steam returned the public Zombieland id for a new validation item; refusing to continue.");
            publishedFileId = createdId;
            WriteMetadata(new PrivateItemMetadata(createdId, steamId, DateTimeOffset.UtcNow));
        }

        var preparation = await requestStore.CreatePrivateValidationAsync(
            publishedFileId,
            steamId,
            contentFolder,
            previewFile);
        var publish = await publisher.PublishPreparedAsync(preparation.RequestPath);
        return new PrivateWorkshopValidationResult(
            publish.Success,
            reused,
            MetadataPath,
            publishedFileId,
            preparation.RequestPath,
            creation,
            publish,
            publish.Success
                ? "The dedicated Workshop validation item was updated and kept private."
                : "The private validation update did not complete successfully; inspect the structured publish result before retrying.");
    }

    public async Task<object> PrepareExistingAsync(bool confirm)
    {
        if (!confirm)
        {
            return new
            {
                confirmed = false,
                metadataPath = MetadataPath,
                message = "Pass confirm=true to prepare a fresh request for the existing private validation item without submitting it."
            };
        }

        var metadata = ReadMetadata()
            ?? throw new InvalidOperationException("No dedicated private validation item exists. Run private-validation --confirm first.");
        if (metadata.PublishedFileId == PublicZombielandItemId)
            throw new InvalidOperationException("Refusing to use the public Zombieland Workshop item for validation.");

        var probe = await publisher.ProbeAsync();
        if (!probe.Ready || probe.SteamId is not { } steamId || steamId != metadata.SteamId)
            throw new InvalidOperationException("The current Steam session does not match the stored private validation item owner.");

        var contentFolder = Path.Combine(FixtureRoot, "Content");
        var previewFile = Path.Combine(FixtureRoot, "Preview.png");
        CreateFixture(contentFolder, previewFile);
        return await requestStore.CreatePrivateValidationAsync(
            metadata.PublishedFileId,
            steamId,
            contentFolder,
            previewFile);
    }

    public PrivateWorkshopItemCreationResult CreatePrivateItemInCurrentProcess(string nativeLibraryPath)
    {
        if (!ProcessIsolation.TryDetachFromControllingTerminal(out var isolationMessage))
            return Failure($"Refusing to initialize Steamworks without a detached process session. {isolationMessage}");

        ConfigureSteamEnvironment();
        using var steam = new SteamworksNativeClient(nativeLibraryPath);
        if (!steam.Init())
            return Failure("SteamAPI_Init failed while creating the private validation item.");

        try
        {
            var loggedOn = steam.UserLoggedOn();
            var steamId = steam.GetSteamId();
            var appId = steam.GetAppId();
            if (!loggedOn || steamId == 0 || appId != AgentPaths.RimWorldAppId)
                return Failure("The Steamworks session is not ready to create a private validation item.", true, loggedOn, steamId, appId);

            var call = steam.CreateItem(AgentPaths.RimWorldAppId);
            if (call == 0)
                return Failure("SteamUGC.CreateItem returned an invalid API call handle.", true, true, steamId, appId);

            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                steam.RunCallbacks();
                if (steam.TryGetCreateItemResult(call, out var result, out var ioFailure))
                {
                    var steamResult = SteamworksNativeClient.FormatResult(result.Result);
                    var success = !ioFailure && result.Result == 1 && result.PublishedFileId != 0;
                    return new PrivateWorkshopItemCreationResult(
                        success,
                        SteamInitialized: true,
                        SteamUserLoggedOn: true,
                        steamId,
                        appId,
                        result.PublishedFileId == 0 ? null : result.PublishedFileId,
                        steamResult,
                        result.UserNeedsToAcceptWorkshopLegalAgreement,
                        TimedOut: false,
                        success
                            ? "Created a new empty Workshop item. The following update will explicitly keep it private."
                            : $"SteamUGC.CreateItem returned {steamResult}; IOFailure={ioFailure}.");
                }

                Thread.Sleep(100);
            }

            return new PrivateWorkshopItemCreationResult(
                false, true, true, steamId, appId, null, null, false, true,
                "Timed out waiting for CreateItemResult_t. Do not create another item until account state is inspected.");
        }
        finally
        {
            steam.Shutdown();
        }
    }

    private async Task<PrivateWorkshopItemCreationResult> CreatePrivateItemAsync()
    {
        var nativeLibraryPath = steamEnvironment.FindSteamworksNativeLibrary()
            ?? throw new InvalidOperationException("RimWorld's native Steamworks library was not found.");
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot locate the SteamWorkshopAgent executable.");
        var arguments = new List<string>();
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add(Path.Combine(AppContext.BaseDirectory, "SteamWorkshopAgent.dll"));
        }
        arguments.Add("steamworks-create-private-validation-item-internal");
        arguments.Add(nativeLibraryPath);

        var process = await processRunner.RunAsync(processPath, arguments, timeout: TimeSpan.FromMinutes(3));
        foreach (var line in process.Stdout.Split('\n').Reverse())
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("{\"status\":", StringComparison.Ordinal))
                continue;
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.TryGetProperty("data", out var data))
            {
                var result = data.Deserialize<PrivateWorkshopItemCreationResult>(ToolJson.Options);
                if (result != null)
                    return result;
            }
        }

        return Failure($"Private item helper failed with exit code {process.ExitCode}: {process.Stderr}");
    }

    private static void CreateFixture(string contentFolder, string previewFile)
    {
        Directory.CreateDirectory(contentFolder);
        File.WriteAllText(
            Path.Combine(contentFolder, "validation.txt"),
            $"SteamWorkshopAgent private validation\nUpdated: {DateTimeOffset.UtcNow:O}\n");
        WritePreviewPng(previewFile, 256, 256);
    }

    private static void WritePreviewPng(string path, int width, int height)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                raw.WriteByte(36);
                raw.WriteByte(48);
                raw.WriteByte(57);
                raw.WriteByte(255);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw.ToArray());

        using var png = File.Create(path);
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, Crc32(typeBytes, data));
        output.Write(crcBytes);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xffffffffu;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }

    private static PrivateItemMetadata? ReadMetadata()
    {
        if (!File.Exists(MetadataPath))
            return null;
        return JsonSerializer.Deserialize<PrivateItemMetadata>(File.ReadAllText(MetadataPath), ToolJson.Options);
    }

    private static void WriteMetadata(PrivateItemMetadata metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MetadataPath)!);
        File.WriteAllText(MetadataPath, JsonSerializer.Serialize(metadata, ToolJson.Options));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(MetadataPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void ConfigureSteamEnvironment()
    {
        var appId = AgentPaths.RimWorldAppId.ToString();
        Environment.SetEnvironmentVariable("SteamAppId", appId);
        Environment.SetEnvironmentVariable("SteamGameId", appId);
        Environment.SetEnvironmentVariable("SteamOverlayGameId", appId);
        Directory.CreateDirectory(AgentPaths.SteamworksDirectory);
        File.WriteAllText(Path.Combine(AgentPaths.SteamworksDirectory, "steam_appid.txt"), appId);
        Environment.CurrentDirectory = AgentPaths.SteamworksDirectory;
    }

    private static PrivateWorkshopItemCreationResult Failure(
        string message,
        bool initialized = false,
        bool loggedOn = false,
        ulong? steamId = null,
        uint? appId = null)
    {
        return new PrivateWorkshopItemCreationResult(
            false, initialized, loggedOn, steamId, appId, null, null, false, false, message);
    }

    private sealed record PrivateItemMetadata(ulong PublishedFileId, ulong SteamId, DateTimeOffset CreatedAtUtc);
}
