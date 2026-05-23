using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Project.core.Servies;

namespace Project.Service
{
    public class ChatService : IChatService
    {
        private readonly IConfiguration _configuration;
        private readonly IBabiesServies _babiesService;
        private readonly INursesServies _nursesService;
        private readonly ITurnsServies _turnsService;
        private readonly ILogger<ChatService> _logger;
        private readonly HttpClient _httpClient;

        public ChatService(
            IConfiguration configuration,
            IBabiesServies babiesService,
            INursesServies nursesService,
            ITurnsServies turnsService,
            ILogger<ChatService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _babiesService = babiesService;
            _nursesService = nursesService;
            _turnsService = turnsService;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<string> ProcessChatMessageAsync(string userMessage, List<(string role, string content)> conversationHistory)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("❌ OpenAI API Key is not configured in appsettings.Development.json");
                    return "❌ OpenAI API Key לא מוגדר בשרת. בדוק את appsettings.Development.json";
                }

                _logger.LogInformation($"✅ ChatService: API Key found, length: {apiKey.Length}");

                var systemPrompt = GetSystemPrompt();
                var tools = GetAvailableTools();

                // בנה את רשימת ההודעות להשלחה ל־OpenAI
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // הוסף היסטוריה
                foreach (var (role, content) in conversationHistory)
                {
                    messages.Add(new { role, content });
                }

                // הוסף הודעה של המשתמש הנוכחית
                messages.Add(new { role = "user", content = userMessage });

                // קרא ל־OpenAI
                var response = await CallOpenAIAsync(apiKey, messages, tools);

                if (string.IsNullOrEmpty(response))
                {
                    return "❌ לא הצלחתי לקבל תגובה מ־OpenAI";
                }

                // בדוק אם יש tool_calls בתגובה
                var finalResponse = response;
                var toolCalls = ExtractToolCalls(response);

                foreach (var toolCall in toolCalls)
                {
                    try
                    {
                        var toolResult = await ExecuteToolAsync(toolCall.tool, toolCall.@params);
                        finalResponse = finalResponse.Replace(
                            $"<tool_call>\n{JsonSerializer.Serialize(toolCall)}\n</tool_call>",
                            $"\n📌 תוצאה: {toolResult}\n"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error executing tool {toolCall.tool}: {ex.Message}");
                    }
                }

                return finalResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ProcessChatMessageAsync: {ex.Message}");
                return $"❌ שגיאה בעיבוד ההודעה: {ex.Message}";
            }
        }

        private async Task<string> CallOpenAIAsync(string apiKey, List<object> messages, List<object> tools)
        {
            try
            {
                _logger.LogInformation("📡 Calling OpenAI API...");
                
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    var request = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = messages,
                        tools = tools,
                        tool_choice = "auto",
                        temperature = 0.7,
                        max_tokens = 500
                    };

                    var content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(request),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    var response = await client.PostAsync(
                        "https://api.openai.com/v1/chat/completions",
                        content
                    );

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"❌ OpenAI API Error: {response.StatusCode} - {error}");
                        return null;
                    }

                    _logger.LogInformation("✅ OpenAI API Response received");
                    var responseString = await response.Content.ReadAsStringAsync();
                    var jsonDocument = JsonDocument.Parse(responseString);
                    var messageContent = jsonDocument
                        .RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return messageContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error calling OpenAI API: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, string> @params)
        {
            try
            {
                switch (toolName)
                {
                    case "get_all_babies":
                        return await GetAllBabies();

                    case "get_baby_by_id":
                        if (@params.TryGetValue("baby_id", out var babyId) && int.TryParse(babyId, out var id))
                            return await GetBabyById(id);
                        return "❌ מזהה תינוק לא חוקי";

                    case "get_all_nurses":
                        return await GetAllNurses();

                    case "get_nurse_by_id":
                        if (@params.TryGetValue("nurse_id", out var nurseId) && int.TryParse(nurseId, out var nid))
                            return await GetNurseById(nid);
                        return "❌ מזהה אחות לא חוקי";

                    case "get_all_turns":
                        return await GetAllTurns();

                    case "get_turn_by_id":
                        if (@params.TryGetValue("turn_id", out var turnId) && int.TryParse(turnId, out var tid))
                            return await GetTurnById(tid);
                        return "❌ מזהה תור לא חוקי";

                    default:
                        return $"❌ Tool לא קיים: {toolName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing tool {toolName}: {ex.Message}");
                return $"❌ שגיאה בביצוע {toolName}: {ex.Message}";
            }
        }

        private async Task<string> GetAllBabies()
        {
            try
            {
                var babies = await _babiesService.GetAllAsync();
                if (babies == null || !babies.Any())
                    return "📋 אין תינוקות במערכת כרגע";

                var babyList = string.Join("\n", babies.Select(b => $"• {b.name} (מזהה: {b.id})"));
                return $"👶 רשימת כל התינוקות:\n{babyList}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת תינוקות: {ex.Message}";
            }
        }

        private async Task<string> GetBabyById(int babyId)
        {
            try
            {
                var baby = await _babiesService.GetByIdAsync(babyId);
                if (baby == null)
                    return $"❌ לא הצלחתי למצוא תינוק עם מזהה {babyId}";

                return $"👶 פרטי התינוק:\nשם: {baby.name}\nמזהה: {baby.id}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת תינוק: {ex.Message}";
            }
        }

        private async Task<string> GetAllNurses()
        {
            try
            {
                var nurses = await _nursesService.GetAllAsync();
                if (nurses == null || !nurses.Any())
                    return "📋 אין אחיות במערכת כרגע";

                var nurseList = string.Join("\n", nurses.Select(n => $"• {n.name} (מזהה: {n.id})"));
                return $"👩‍⚕️ רשימת כל האחיות:\n{nurseList}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת אחיות: {ex.Message}";
            }
        }

        private async Task<string> GetNurseById(int nurseId)
        {
            try
            {
                var nurse = await _nursesService.GetByIdAsync(nurseId);
                if (nurse == null)
                    return $"❌ לא הצלחתי למצוא אחות עם מזהה {nurseId}";

                return $"👩‍⚕️ פרטי האחות:\nשם: {nurse.name}\nמזהה: {nurse.id}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת אחות: {ex.Message}";
            }
        }

        private async Task<string> GetAllTurns()
        {
            try
            {
                var turns = await _turnsService.GetAllTurnsAsync();
                if (turns == null || !turns.Any())
                    return "📋 אין תורנויות במערכת כרגע";

                var turnList = string.Join("\n", turns.Select(t => $"• תור מזהה: {t.id}"));
                return $"📅 רשימת כל התורנויות:\n{turnList}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת תורנויות: {ex.Message}";
            }
        }

        private async Task<string> GetTurnById(int turnId)
        {
            try
            {
                var turn = await _turnsService.GetByIdAsync(turnId);
                if (turn == null)
                    return $"❌ לא הצלחתי למצוא תור עם מזהה {turnId}";

                return $"📅 פרטי התור:\nמזהה: {turn.id}";
            }
            catch (Exception ex)
            {
                return $"❌ שגיאה בשליפת תור: {ex.Message}";
            }
        }

        private string GetSystemPrompt()
        {
            return @"אתה עוזר דיגיטלי חכם וידידותי לאתר ניהול תינוקות, אחיות ותורנויות.

**מערכת המידע:**
- **תינוקות (Babies)**: רשומות של תינוקות שבהשגחה
- **אחיות (Nurses)**: רשומות של אחיות המטפלות בתינוקות
- **תורנויות (Turns)**: לוח תורנויות המקשר בין אחיות לתינוקות

**הנחיות השימוש בכלים:**
- כשמשתמש שואל ""הצג כל התינוקות"" - השתמש ב-tool get_all_babies
- כשמשתמש שואל ""מי הוא התינוק מספר 5?"" או ""פרטי תינוק"" - השתמש ב-tool get_baby_by_id
- כשמשתמש שואל ""הצג כל האחיות"" - השתמש ב-tool get_all_nurses
- כשמשתמש שואל ""מי היא אחות מספר 3?"" או ""פרטי אחות"" - השתמש ב-tool get_nurse_by_id
- כשמשתמש שואל ""הצג את התורנויות"" - השתמש ב-tool get_all_turns
- כשמשתמש שואל על תור מסוים - השתמש ב-tool get_turn_by_id

**התנהגות:**
- עונה בעברית בלבד
- אם משתמש מבקש מידע - חפש בכלים המתאימים בהחלט
- אם המידע לא נמצא - הסבר בנימוס שאין מידע
- תמיד עוזר בנימוס וחיוך
- קצר, ברור ותכליתי";
        }

        private List<object> GetAvailableTools()
        {
            return new List<object>
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_all_babies",
                        description = "קבל רשימה של כל התינוקות במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_baby_by_id",
                        description = "קבל פרטי תינוק לפי מספר מזהה",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                baby_id = new { type = "string", description = "מספר המזהה של התינוק" }
                            },
                            required = new[] { "baby_id" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_all_nurses",
                        description = "קבל רשימה של כל האחיות במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_nurse_by_id",
                        description = "קבל פרטי אחות לפי מספר מזהה",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                nurse_id = new { type = "string", description = "מספר המזהה של האחות" }
                            },
                            required = new[] { "nurse_id" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_all_turns",
                        description = "קבל רשימה של כל התורנויות במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_turn_by_id",
                        description = "קבל פרטי תור לפי מספר מזהה",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                turn_id = new { type = "string", description = "מספר המזהה של התור" }
                            },
                            required = new[] { "turn_id" }
                        }
                    }
                }
            };
        }

        private List<(string tool, Dictionary<string, string> @params)> ExtractToolCalls(string response)
        {
            var toolCalls = new List<(string tool, Dictionary<string, string> @params)>();
            var regex = new System.Text.RegularExpressions.Regex(@"<tool_call>\s*(\{[\s\S]*?\})\s*</tool_call>");
            var matches = regex.Matches(response);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                try
                {
                    var json = match.Groups[1].Value;
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var toolName = root.GetProperty("tool").GetString();
                    if (toolName == null) continue;
                    
                    var @params = new Dictionary<string, string>();

                    if (root.TryGetProperty("params", out var paramsElement))
                    {
                        foreach (var prop in paramsElement.EnumerateObject())
                        {
                            var value = prop.Value.GetString();
                            if (value != null)
                                @params[prop.Name] = value;
                        }
                    }

                    toolCalls.Add((toolName, @params));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error parsing tool call: {ex.Message}");
                }
            }

            return toolCalls;
        }
    }
}
