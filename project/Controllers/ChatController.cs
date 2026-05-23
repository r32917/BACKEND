using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using project.Models.ChatModels;
using Project.core.Servies;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace project.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ChatResponseModel>> Post([FromBody] ChatRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return BadRequest("הודעה לא יכולה להיות ריקה");
            }

            try
            {
                // המר את ה־ConversationMessage ל־tuple
                var conversationHistory = request.ConversationHistory?
                    .Select(m => (m.Role, m.Content))
                    .ToList() ?? new List<(string, string)>();

                // עבד את ההודעה דרך ChatService
                var response = await _chatService.ProcessChatMessageAsync(request.Message, conversationHistory);

                return Ok(new ChatResponseModel { Response = response });
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Error in ChatController.Post: {ex.Message}");
                return StatusCode(500, new { error = "שגיאה בעיבוד ההודעה" });
            }
        }
    }
}
