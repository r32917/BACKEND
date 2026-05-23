using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.core.Servies
{
    public interface IChatService
    {
        Task<string> ProcessChatMessageAsync(string userMessage, List<(string role, string content)> conversationHistory);
    }
}
