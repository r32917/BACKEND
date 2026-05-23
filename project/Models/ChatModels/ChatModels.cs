using System.Collections.Generic;

namespace project.Models.ChatModels
{
    public class ChatRequestModel
    {
        public string Message { get; set; }
        public List<ConversationMessage> ConversationHistory { get; set; }
    }

    public class ConversationMessage
    {
        public string Role { get; set; } // "user" or "assistant"
        public string Content { get; set; }
    }

    public class ChatResponseModel
    {
        public string Response { get; set; }
    }
}
