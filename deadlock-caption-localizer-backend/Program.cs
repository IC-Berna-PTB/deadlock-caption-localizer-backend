// See https://aka.ms/new-console-template for more information

using System.CommandLine;
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

Option<int> deadlockAppIdOption = new("--deadlock-app-id")
{
    Description = "Use this AppID for Deadlock instead",
    DefaultValueFactory = _ => 1422450
};
rootCommand.Options.Add(deadlockAppIdOption);

Option<string> runModeOption = new("--run-mode")
{
    Description = "Run mode",
};
rootCommand.Options.Add(runModeOption);

Option<string> voiceFileOption = new("--voice-file");
rootCommand.Options.Add(voiceFileOption);

Option<string> heroCodeOption = new("--hero-code");
rootCommand.Options.Add(heroCodeOption);

Option<string> deadlockPathOption = new("--deadlock-path");
rootCommand.Options.Add(deadlockPathOption);

var argResults = rootCommand.Parse(args);

var deadlock = GameFolderLocator.FindSteamGameByAppId(argResults.GetValue(deadlockAppIdOption));
var deadlockPath = "";

var runMode = argResults.GetRequiredValue(runModeOption);


if (!deadlock.HasValue)
{
    if (argResults.GetValue(deadlockPathOption) is null || runMode.Equals("autofolder"))
    {
        Console.Error.WriteLine("Could not find Deadlock installed in the system. Use --deadlock-path [path] to set the path to the game install folder.");
        return 1;
    }
    deadlockPath = argResults.GetValue(deadlockPathOption);
}
else
{
    deadlockPath = deadlock.Value.GamePath;
    if (runMode.Equals("autofolder"))
    {
        Console.Write(deadlockPath);
        return 0;
    }
}



var vpkPath = Path.Join(deadlockPath, "game/citadel/pak01_dir.vpk");

if (!Path.Exists(vpkPath))
{
   Console.Error.WriteLine($"There's no file in {vpkPath}!");
   return 2;
}

var vpk = new Package();
vpk.Read(vpkPath);
var fileLoader = new NullFileLoader();

return runMode switch
{
    "convo" => RunConvo(),
    "vo" => RunVo(),
    "mugshot" => RunMugshot(),
    "autofolder" => RunAutoFolder(),
    _ => 3
};

int RunAutoFolder()
{
    if (deadlock == null) return 1;
    Console.Write(deadlock.Value.GamePath);
    return 0;

}

int RunMugshot()
{
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
    var entries = vpk.Entries!["vcd_c"];
    var convos = entries.Select(e => ParseConversation(e, vpk, fileLoader)).ToList();
    Console.Write(Serialize(new ConversationsRecord(Conversations: convos),
        typeof(ConversationsRecord),
        SourceGenerationContext.Default));
    return 0;
}

int RunVo()
{
    var requestedVoiceFile = argResults.GetRequiredValue(voiceFileOption);
    var soundFiles = vpk.Entries!["vsnd_c"];
    var file = soundFiles.First(s => requestedVoiceFile.StartsWith(Path.GetFileNameWithoutExtension(s.FileName)));
    var stream = vpk.GetMemoryMappedStreamIfPossible(file);
    using var resource = new Resource();
    resource.Read(stream);
    var contentFile = FileExtract.Extract(resource, fileLoader);
    if (contentFile.Data is null)
    {
        return 4;
    }
    Console.OpenStandardOutput().Write(contentFile.Data);
    return 0;
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
            list.Add( dialog);
            return list;
        });
    return new Conversation(Path.GetFileNameWithoutExtension(entry.FileName), dialogs);
}