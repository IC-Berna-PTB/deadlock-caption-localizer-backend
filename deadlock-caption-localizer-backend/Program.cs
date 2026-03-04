// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using deadlock_caption_localizer_backend;
using SteamDatabase.ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.IO;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.Serialization.KeyValues;
using ValveResourceFormat.TextureDecoders;
using static System.Text.Json.JsonSerializer;
using KVObject = ValveResourceFormat.Serialization.KeyValues.KVObject;

var rootCommand = new RootCommand("Deadlock Caption Localizer Backend");

const int defaultAppid = 1422450;

Option<int> deadlockAppIdOption = new("--deadlock-app-id")
{
    Description = "Use this AppID for Deadlock instead",
    DefaultValueFactory = _ => defaultAppid
};
rootCommand.Options.Add(deadlockAppIdOption);

Option<string> runModeOption = new("--run-mode")
{
    Description = "Run mode",
    DefaultValueFactory = _ => "server"
};
rootCommand.Options.Add(runModeOption);

Option<string> voiceFileOption = new("--voice-file");
rootCommand.Options.Add(voiceFileOption);

Option<string> heroCodeOption = new("--hero-code");
rootCommand.Options.Add(heroCodeOption);

Option<string> deadlockPathOption = new("--deadlock-path");
rootCommand.Options.Add(deadlockPathOption);

var argResults = rootCommand.Parse(args);

var runMode = argResults.GetValue(runModeOption);

var deadlockPath = GetFolder(argResults);

string? GetFolder(ParseResult parseResult)
{
    var manualPath = parseResult.GetValue(deadlockPathOption);
    if (manualPath is not null && RunValidateFolder() == 0)
    {
        return manualPath;
    }

    return GetAutoFolder();
}

return runMode switch
{
    "convo" => RunConvo(),
    "vo" => RunVo(),
    "mugshot" => RunMugshot(),
    "autofolder" => RunAutoFolder(),
    "validatefolder" => RunValidateFolder(),
    "server" => RunServer(),
    _ => 3
};

int RunServer()
{
    var listener = new HttpListener();
    const string url = "http://localhost:51072/";
    listener.Prefixes.Add(url);
    listener.Start();
    var listenTask = HandleIncomingConnections(listener);
    listenTask.GetAwaiter().GetResult();
    
    listener.Close();
    return 0;
}

async Task HandleIncomingConnections(HttpListener listener)
{
    while (true)
    {
        var ctx = await listener.GetContextAsync();

        var req = ctx.Request;
        var resp = ctx.Response;

        switch (req)
        {
            case { HttpMethod: "POST", Url.AbsolutePath: "/set-folder" }:
                using (var reader = new StreamReader(req.InputStream))
                {
                    var path = reader.ReadToEnd();
                    var tuple = GetVpk(path, tryAutoFind: false);
                    resp.ContentType = "text/plain";
                    resp.ContentEncoding = Encoding.UTF8;
                    if (tuple is not null)
                    {
                        deadlockPath = path;
                        var success = $"Path set to {deadlockPath}.";
                        resp.StatusCode = (int)HttpStatusCode.OK;
                        resp.ContentLength64 = success.Length;
                        await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(success).AsMemory(0, success.Length));
                    }
                    else
                    {
                        var failed = $"Path {deadlockPath} not found";
                        resp.StatusCode = (int)HttpStatusCode.NotFound;
                        resp.ContentLength64 = failed.Length;
                        await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(failed).AsMemory(0, failed.Length));
                    }
                }
                resp.Close();
                break;
            case { HttpMethod: "GET", Url: not null } when VoiceFileRegex().IsMatch(req.Url.AbsolutePath):
            {
                var voiceFile = GetVo(req.Url.AbsolutePath.Replace("/", ""));
                if (voiceFile is not null)
                {
                    resp.StatusCode = (int)HttpStatusCode.OK;
                    resp.ContentType = "audio/mpeg";
                    resp.ContentLength64 = voiceFile.LongLength;
                    await resp.OutputStream.WriteAsync(voiceFile);
                }
                else
                {
                    const string responseText = "Could not find requested voice file";
                    resp.StatusCode = (int)HttpStatusCode.NotFound;
                    resp.ContentType = "text/plain";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = responseText.Length;
                
                    await resp.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(responseText).AsMemory(0, responseText.Length));
                }
                resp.Close();
                break;
            }
        }
    }
} 

int RunValidateFolder()
{
    var valueTuple = GetVpk(argResults.GetValue(deadlockPathOption), argResults.GetValue(deadlockAppIdOption), tryAutoFind: false);
    return valueTuple.HasValue ? 0 : 1;
}

(Package, IFileLoader)? GetVpk(string? path, int appId = defaultAppid, bool tryAutoFind = true)
{

    if (path is null && tryAutoFind)
    {
        var deadlock = GameFolderLocator.FindSteamGameByAppId(appId);
        if (deadlock.HasValue)
        {
            path = deadlock.Value.GamePath;
        }
    }

    if (string.IsNullOrEmpty(path))
    {
        return null;
        // throw new FileNotFoundException(
        //     "Could not find Deadlock installed in the system. Use --deadlock-path [path] to set the path to the game install folder.");
    }

    var vpkPath = Path.Join(path, "game/citadel/pak01_dir.vpk");

    if (!Path.Exists(vpkPath))
    {
        return null;
        // throw new FileNotFoundException($"Could not find VPK file at {vpkPath}");
    }

    var vpk = new Package();
    vpk.Read(vpkPath);
    var fileLoader = new NullFileLoader();
    return (vpk, fileLoader);
}

int RunAutoFolder()
{
    var path = GetAutoFolder();
    if (path is null)
    {
        return 1;
    }
    Console.Write(path);
    return 0;
}

string? GetAutoFolder()
{
    var deadlock = GameFolderLocator.FindSteamGameByAppId(argResults.GetValue(deadlockAppIdOption));
    return deadlock?.GamePath;
}

int RunMugshot()
{
    var tuple = GetVpk(deadlockPath);
    if (tuple is null)
    {
        return 1;
    }
    var (vpk, _) = tuple.Value;
    var entries = vpk.Entries!["vtex_c"];
    var file = entries.First(e => e.GetFullPath()
        .Equals($"panorama/images/heroes/{argResults.GetRequiredValue(heroCodeOption)}_sm_psd.vtex_c"));
    var stream = vpk.GetMemoryMappedStreamIfPossible(file);
    using var resource = new Resource();
    resource.FileName = file.GetFullPath();
    resource.Read(stream);
    if (resource.DataBlock! is not Texture)
    {
        return 4;
    }

    var contentFile = new TextureExtract(resource)
    {
        DecodeFlags = TextureCodec.Auto
    }.ToContentFile();
    var png = contentFile.SubFiles[0].Extract?.Invoke();
    Console.OpenStandardOutput().Write(png);
    return 0;
}

int RunConvo()
{
    var tuple = GetVpk(deadlockPath);
    if (tuple is null)
    {
        return 1;
    }
    var (vpk, fileLoader) = tuple.Value;
    var entries = vpk.Entries!["vcd_c"];
    var convos = entries.Select(e => ParseConversation(e, vpk, fileLoader)).ToList();
    Console.Write(Serialize(new ConversationsRecord(Conversations: convos),
        typeof(ConversationsRecord),
        SourceGenerationContext.Default));
    return 0;
}

int RunVo()
{
    var result = GetVo(argResults.GetRequiredValue(voiceFileOption));
    if (result is null)
    {
        return 4;
    }
    Console.OpenStandardOutput().Write(result);
    return 0;
}

byte[]? GetVo(string requestedVoiceFile)
{
    var tuple = GetVpk(deadlockPath);
    if (tuple is null)
    {
        return null;
    }
    var (vpk, fileLoader) = tuple.Value;
    var soundFiles = vpk.Entries!["vsnd_c"];
    Console.WriteLine(string.Join(",", soundFiles.Where(s => s.FileName.Contains("alt_01")).Select(s => s.FileName).ToList()));
    var voiceFileName = requestedVoiceFile.Trim();
    PackageEntry? file = null;
    while (file is null && voiceFileName.Length > 0)
    {
        var current = soundFiles.FirstOrDefault(s => s.FileName.Equals(voiceFileName));
        if (current is not null)
        {
            file = current;
        }
        else
        {
            voiceFileName = voiceFileName[..voiceFileName.LastIndexOf('_')];
        }
    }
    if (file is null || voiceFileName.Length == 0)
    {
        return null;
    }
    var stream = vpk.GetMemoryMappedStreamIfPossible(file);
    using var resource = new Resource();
    resource.Read(stream);
    var contentFile = FileExtract.Extract(resource, fileLoader);
    return contentFile.Data;
    // if (contentFile.Data is null)
    // {
    //     return 4;
    // }
    //
    // Console.OpenStandardOutput().Write(contentFile.Data);
}

static Conversation ParseConversation(PackageEntry entry, Package vpk, IFileLoader fileLoader)
{
    using var resource = new Resource();
    var stream = vpk.GetMemoryMappedStreamIfPossible(entry);
    resource.Read(stream);
    var contentFile = FileExtract.Extract(resource, fileLoader);
    var kv3 = KeyValues3.ParseKVFile(new MemoryStream(contentFile.Data!));

    var actors = kv3.Root.GetProperty<KVObject>("actors");
    var dialogs = actors.SelectMany(a => Dialog.FromKvObject((a.Value) as KVObject)).Aggregate(
        new List<Dialog>(),
        (list, dialog) =>
        {
            list.Add(dialog);
            return list;
        });
    return new Conversation(Path.GetFileNameWithoutExtension(entry.FileName), dialogs);
}

partial class Program
{
    [GeneratedRegex("/[A-Za-z0-9_]+")]
    private static partial Regex VoiceFileRegex();
}