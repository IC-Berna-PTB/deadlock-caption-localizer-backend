// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.Text.Json;
using deadlock_caption_localizer_backend;
using Duper;
using SteamDatabase.ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.IO;
using ValveResourceFormat.Serialization.KeyValues;
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
    // Required = true,
};
rootCommand.Options.Add(runModeOption);

Option<string> voiceFileOption = new("--voice-file");
rootCommand.Options.Add(voiceFileOption);

Option<String> deadlockPathOption = new("--deadlock-path");
rootCommand.Options.Add(deadlockPathOption);

var results = rootCommand.Parse(args);

var deadlock = GameFolderLocator.FindSteamGameByAppId(results.GetValue(deadlockAppIdOption));
var deadlockPath = "";

if (!deadlock.HasValue)
{
    if (results.GetValue(deadlockPathOption) is null)
    {
        Console.Error.WriteLine("Could not find Deadlock installed in the system. Use --deadlock-path [path] to set the path to the game install folder.");
        return 1;
    }
    deadlockPath = results.GetValue(deadlockPathOption);
}
else
{
    deadlockPath = deadlock.Value.GamePath;
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

return results.GetRequiredValue(runModeOption) switch
{
    "convo" => RunConvo(),
    "vo" => RunVo(),
    _ => 3
};

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
    var requestedVoiceFile = results.GetRequiredValue(voiceFileOption);
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