using System.Text.Json.Serialization;

namespace deadlock_caption_localizer_backend;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ConversationsRecord))]
[JsonSerializable(typeof(Conversation))]
[JsonSerializable(typeof(Dialog))]
public partial class SourceGenerationContext : JsonSerializerContext
{
    
}