namespace deadlock_caption_localizer_backend;

public class RunMode {

    private RunMode(string value)
    {
        Value = value;
    }
    
    public string Value { get; private set; }
    
    public static RunMode Conversations => new("convo");
    public static RunMode VoiceFile => new("vo");
}