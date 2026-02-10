// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using deadlock_caption_localizer_backend;
using SteamDatabase.ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.IO;
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

var results = rootCommand.Parse(args);

var deadlock = GameFolderLocator.FindSteamGameByAppId(results.GetValue(deadlockAppIdOption));
if (!deadlock.HasValue)
{
    Console.Error.WriteLine("Could not find Deadlock installed in the system.");
    return 1;
}

var deadlockPath = deadlock.Value.GamePath;
var vpkPath = Path.Join(deadlockPath, "game/citadel/pak01_dir.vpk");
var vpk = new Package();
vpk.Read(vpkPath);
using var fileLoader = new GameFileLoader(vpk, vpk.FileName);

if (results.GetRequiredValue<string>(runModeOption).Equals("convo"))
{
    
}
var entries = vpk.Entries!["vcd_c"];

static Conversation ParseConversation(PackageEntry entry, Package vpk, IFileLoader fileLoader)
{
    using var resource = new Resource();
    var stream = vpk.GetMemoryMappedStreamIfPossible(entry);
    resource.Read(stream);
    var contentFile = FileExtract.Extract(resource, fileLoader);
    var kv3 = ValveResourceFormat.Serialization.KeyValues.KeyValues3.ParseKVFile(new MemoryStream(contentFile.Data!));

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

var convos = entries.Select(e => ParseConversation(e, vpk, fileLoader)).ToList();

convos.ForEach(c => File.WriteAllText(Path.ChangeExtension(c.Name, "toml"), Tomlyn.Toml.FromModel(c)));
return 0;