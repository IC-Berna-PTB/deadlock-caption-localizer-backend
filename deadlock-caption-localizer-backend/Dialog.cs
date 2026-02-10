using ValveResourceFormat.Serialization.KeyValues;

namespace deadlock_caption_localizer_backend;

public record Dialog(string Actor, double Start, double End, string FileName)
{
    public static List<Dialog> FromKvObject(KVObject? obj)
    {
        var actorName = obj.GetProperty<string>("name");
        var events = (obj.GetProperty<KVObject>("channels")[0].Value as KVObject).GetProperty<KVObject>("events");
        return events.Select(e => e.Value)
            .Select(e => e as KVObject)
            .Where(e => e != null)
            .Select(e => e!)
            .Select(e => new Dialog(actorName,
                e.GetProperty<double>("start_time"),
                e.GetProperty<double>("end_time"),
                e.GetProperty<string>("param")))
            .ToList();
    }    
}

