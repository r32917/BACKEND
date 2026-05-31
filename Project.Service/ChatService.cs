using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                    new Dictionary<string, object?>
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    }
                };

                // הוסף היסטוריה
                foreach (var (role, content) in conversationHistory)
                {
                    messages.Add(new Dictionary<string, object?>
                    {
                        ["role"] = role,
                        ["content"] = content
                    });
                }

                // הוסף הודעה של המשתמש הנוכחית
                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = userMessage
                });

                for (var round = 0; round < 6; round++)
                {
                    var response = await CallOpenAIAsync(apiKey, messages, tools);

                    if (response == null)
                    {
                        return "❌ לא הצלחתי לקבל תגובה מ־OpenAI";
                    }

                    var message = response.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message");

                    var content = GetMessageContent(message);

                    if (!HasToolCalls(message, out var toolCallsElement))
                    {
                        return string.IsNullOrWhiteSpace(content)
                            ? "❌ לא התקבלה תשובה טקסטואלית מ־OpenAI"
                            : content;
                    }

                    messages.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "assistant",
                        ["content"] = content,
                        ["tool_calls"] = JsonSerializer.Deserialize<object>(toolCallsElement.GetRawText())
                    });

                    foreach (var toolCall in toolCallsElement.EnumerateArray())
                    {
                        var toolCallId = toolCall.GetProperty("id").GetString();
                        var function = toolCall.GetProperty("function");
                        var toolName = function.GetProperty("name").GetString();
                        var argumentsJson = function.GetProperty("arguments").GetString();

                        if (string.IsNullOrWhiteSpace(toolName) || string.IsNullOrWhiteSpace(toolCallId))
                        {
                            continue;
                        }

                        var toolParameters = MergeToolParameters(
                            toolName,
                            ParseToolArguments(argumentsJson),
                            conversationHistory,
                            userMessage);

                        var toolResult = await ExecuteToolAsync(toolName, toolParameters);

                        // כלי request_confirmation — חזור מיד עם שאלת האישור
                        if (toolResult.StartsWith("__CONFIRM__:"))
                        {
                            return toolResult["__CONFIRM__:".Length..].Trim();
                        }

                        messages.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = toolCallId,
                            ["content"] = toolResult
                        });
                    }
                }

                return "❌ לא הצלחתי להשלים את בקשת הכלים של OpenAI";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ProcessChatMessageAsync: {ex.Message}");
                return $"❌ שגיאה בעיבוד ההודעה: {ex.Message}";
            }
        }

        private async Task<JsonDocument?> CallOpenAIAsync(string apiKey, List<object> messages, List<object> tools)
        {
            try
            {
                _logger.LogInformation("📡 Calling OpenAI API...");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var request = new
                {
                    model = "gpt-4o-mini",
                    messages = messages,
                    tools = tools,
                    tool_choice = "auto",
                    temperature = 0.2,
                    max_tokens = 300
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(request),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    var response = await _httpClient.PostAsync(
                        "https://api.openai.com/v1/chat/completions",
                        content,
                        cts.Token
                    );
                

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"❌ OpenAI API Error: {response.StatusCode} - {error}");
                        return null;
                    }

                    _logger.LogInformation("✅ OpenAI API Response received");
                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonDocument.Parse(responseString);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error calling OpenAI API: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private static string? GetMessageContent(JsonElement message)
        {
            if (!message.TryGetProperty("content", out var contentElement) || contentElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return contentElement.GetString();
        }

        private static bool HasToolCalls(JsonElement message, out JsonElement toolCallsElement)
        {
            if (message.TryGetProperty("tool_calls", out toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array
                && toolCallsElement.GetArrayLength() > 0)
            {
                toolCallsElement = toolCallsElement.Clone();
                return true;
            }

            return false;
        }

        private static Dictionary<string, string> ParseToolArguments(string? argumentsJson)
        {
            var parameters = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return parameters;
            }

            using var document = JsonDocument.Parse(argumentsJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                parameters[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => property.Value.GetRawText()
                };
            }

            return parameters;
        }

        private async Task<string> ExecuteToolAsync(string toolName, Dictionary<string, string> @params)
        {
            try
            {
                switch (toolName)
                {
                    case "request_confirmation":
                        @params.TryGetValue("message_to_user", out var confirmMsg);
                        return $"__CONFIRM__: {confirmMsg ?? "האם אתה בטוח שברצונך לבצע פעולה זו?"}"; 

                    case "get_all_babies":
                        return await GetAllBabies();

                    case "get_baby_by_id":
                        if (@params.TryGetValue("baby_id", out var babyId) && int.TryParse(babyId, out var id))
                            return await GetBabyById(id);
                        return "❌ מזהה תינוק לא חוקי";

                    case "create_baby":
                        return await CreateBaby(@params);

                    case "update_baby":
                        return await UpdateBaby(@params);

                    case "delete_baby":
                        return await DeleteBaby(@params);

                    case "get_all_nurses":
                        return await GetAllNurses();

                    case "get_nurse_by_id":
                        if (@params.TryGetValue("nurse_id", out var nurseId) && int.TryParse(nurseId, out var nid))
                            return await GetNurseById(nid);
                        return "❌ מזהה אחות לא חוקי";

                    case "create_nurse":
                        return await CreateNurse(@params);

                    case "update_nurse":
                        return await UpdateNurse(@params);

                    case "delete_nurse":
                        return await DeleteNurse(@params);

                    case "get_all_turns":
                        return await GetAllTurns();

                    case "get_turn_by_id":
                        if (@params.TryGetValue("turn_id", out var turnId) && int.TryParse(turnId, out var tid))
                            return await GetTurnById(tid);
                        return "❌ מזהה תור לא חוקי";

                    case "create_turn":
                        return await CreateTurn(@params);

                    case "update_turn":
                        return await UpdateTurn(@params);

                    case "delete_turn":
                        return await DeleteTurn(@params);

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

        private async Task<string> CreateBaby(Dictionary<string, string> parameters)
        {
            var hasExplicitId = TryGetRequiredInt(parameters, out var id, "id", "baby_id");

            if (!TryGetRequiredString(parameters, out var name, "name", "baby_name") ||
                !TryGetRequiredInt(parameters, out var age, "age", "baby_age") ||
                !TryGetRequiredString(parameters, out var family, "family", "family_name", "last_name"))
            {
                return "❌ חסרים נתונים ליצירת תינוק. צריך name, age, family";
            }

            if (hasExplicitId)
            {
                var existingBaby = await _babiesService.GetByIdAsync(id);
                if (existingBaby != null)
                {
                    return $"❌ כבר קיים תינוק עם מזהה {id}. צריך לבחור מזהה אחר";
                }
            }

            await _babiesService.AddAsync(new project.Babies
            {
                id = hasExplicitId ? id : 0,
                name = name,
                age = age,
                family = family
            });

            return $"✅ התינוק {name} {family} נוסף בהצלחה";
        }

        private async Task<string> UpdateBaby(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "baby_id") ||
                !TryGetRequiredString(parameters, out var name, "name", "baby_name") ||
                !TryGetRequiredInt(parameters, out var age, "age", "baby_age") ||
                !TryGetRequiredString(parameters, out var family, "family", "family_name", "last_name"))
            {
                return "❌ חסרים נתונים לעדכון תינוק. צריך id, name, age, family";
            }

            var existingBaby = await _babiesService.GetByIdAsync(id);
            if (existingBaby == null)
            {
                return $"❌ לא קיים תינוק עם מזהה {id}";
            }

            await _babiesService._Put(id, new project.Babies
            {
                id = id,
                name = name,
                age = age,
                family = family
            });

            return $"✅ התינוק עם מזהה {id} עודכן בהצלחה";
        }

        private async Task<string> DeleteBaby(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "baby_id"))
            {
                return "❌ חסר id למחיקת תינוק";
            }

            var existingBaby = await _babiesService.GetByIdAsync(id);
            if (existingBaby == null)
            {
                return $"❌ לא קיים תינוק עם מזהה {id}";
            }

            await _babiesService._Delete(id);
            return $"✅ התינוק עם מזהה {id} נמחק בהצלחה";
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

        private async Task<string> CreateNurse(Dictionary<string, string> parameters)
        {
            var hasExplicitId = TryGetRequiredInt(parameters, out var id, "id", "nurse_id");

            if (!TryGetRequiredString(parameters, out var name, "name", "nurse_name") ||
                !TryGetRequiredString(parameters, out var phone, "phone") ||
                !TryGetRequiredString(parameters, out var email, "email") ||
                !TryGetRequiredString(parameters, out var specialization, "specialization", "profession"))
            {
                return "❌ חסרים נתונים ליצירת אחות. צריך name, phone, email, specialization";
            }

            if (hasExplicitId)
            {
                var existingNurse = await _nursesService.GetByIdAsync(id);
                if (existingNurse != null)
                {
                    return $"❌ כבר קיימת אחות עם מזהה {id}. צריך לבחור מזהה אחר";
                }
            }

            await _nursesService.AddAsync(new project.Nurses
            {
                id = hasExplicitId ? id : 0,
                name = name,
                phone = phone,
                email = email,
                specialization = specialization
            });

            return $"✅ האחות {name} נוספה בהצלחה";
        }

        private async Task<string> UpdateNurse(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "nurse_id") ||
                !TryGetRequiredString(parameters, out var name, "name", "nurse_name") ||
                !TryGetRequiredString(parameters, out var phone, "phone") ||
                !TryGetRequiredString(parameters, out var email, "email") ||
                !TryGetRequiredString(parameters, out var specialization, "specialization", "profession"))
            {
                return "❌ חסרים נתונים לעדכון אחות. צריך id, name, phone, email, specialization";
            }

            var existingNurse = await _nursesService.GetByIdAsync(id);
            if (existingNurse == null)
            {
                return $"❌ לא קיימת אחות עם מזהה {id}";
            }

            await _nursesService._PutAsync(id, new project.Nurses
            {
                id = id,
                name = name,
                phone = phone,
                email = email,
                specialization = specialization
            });

            return $"✅ האחות עם מזהה {id} עודכנה בהצלחה";
        }

        private async Task<string> DeleteNurse(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "nurse_id"))
            {
                return "❌ חסר id למחיקת אחות";
            }

            var existingNurse = await _nursesService.GetByIdAsync(id);
            if (existingNurse == null)
            {
                return $"❌ לא קיימת אחות עם מזהה {id}";
            }

            await _nursesService._DeleteAsync(id);
            return $"✅ האחות עם מזהה {id} נמחקה בהצלחה";
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

        private async Task<string> CreateTurn(Dictionary<string, string> parameters)
        {
            var hasExplicitId = TryGetRequiredInt(parameters, out var id, "id", "turn_id");

            if (!TryResolveTurnSchedule(parameters, out var date, out var time, out var scheduleError))
            {
                return scheduleError;
            }

            var notes = NormalizeNotes(parameters);

            var (babyResolved, babyId, babyError) = await TryResolveBabyId(parameters);
            if (!babyResolved)
            {
                return babyError;
            }

            var (nurseResolved, nurseId, nurseError) = await TryResolveNurseId(parameters);
            if (!nurseResolved)
            {
                return nurseError;
            }

            if (hasExplicitId)
            {
                var existingTurn = await _turnsService.GetByIdAsync(id);
                if (existingTurn != null)
                {
                    return $"❌ כבר קיים תור עם מזהה {id}. צריך לבחור מזהה אחר";
                }
            }

            await _turnsService._PostAsync(new project.Turns
            {
                id = hasExplicitId ? id : 0,
                babyId = babyId,
                nurseId = nurseId,
                date = date,
                time = time,
                notes = notes
            });

            return "✅ התור נוסף בהצלחה";
        }

        private async Task<string> UpdateTurn(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "turn_id") ||
                !TryGetRequiredInt(parameters, out var babyId, "babyId", "baby_id") ||
                !TryGetRequiredInt(parameters, out var nurseId, "nurseId", "nurse_id"))
            {
                return "❌ חסרים נתונים לעדכון תור. צריך id, babyId, nurseId, date, time";
            }

            if (!TryResolveTurnSchedule(parameters, out var date, out var time, out var scheduleError))
            {
                return scheduleError;
            }

            var notes = NormalizeNotes(parameters);

            var existingTurn = await _turnsService.GetByIdAsync(id);
            if (existingTurn == null)
            {
                return $"❌ לא קיים תור עם מזהה {id}";
            }

            await _turnsService._PutAsync(id, new project.Turns
            {
                id = id,
                babyId = babyId,
                nurseId = nurseId,
                date = date,
                time = time,
                notes = notes
            });

            return $"✅ התור עם מזהה {id} עודכן בהצלחה";
        }

        private async Task<string> DeleteTurn(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredInt(parameters, out var id, "id", "turn_id"))
            {
                return "❌ חסר id למחיקת תור";
            }

            var existingTurn = await _turnsService.GetByIdAsync(id);
            if (existingTurn == null)
            {
                return $"❌ לא קיים תור עם מזהה {id}";
            }

            await _turnsService._DeleteAsync(id);
            return $"✅ התור עם מזהה {id} נמחק בהצלחה";
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
    - כשמשתמש מבקש להוסיף תינוק - השתמש ב-tool create_baby
    - כשמשתמש מבקש לעדכן תינוק - השתמש ב-tool update_baby
    - כשמשתמש מבקש למחוק תינוק - השתמש ב-tool delete_baby
- כשמשתמש שואל ""הצג כל האחיות"" - השתמש ב-tool get_all_nurses
- כשמשתמש שואל ""מי היא אחות מספר 3?"" או ""פרטי אחות"" - השתמש ב-tool get_nurse_by_id
    - כשמשתמש מבקש להוסיף אחות - השתמש ב-tool create_nurse
    - כשמשתמש מבקש לעדכן אחות - השתמש ב-tool update_nurse
    - כשמשתמש מבקש למחוק אחות - השתמש ב-tool delete_nurse
- כשמשתמש שואל ""הצג את התורנויות"" - השתמש ב-tool get_all_turns
- כשמשתמש שואל על תור מסוים - השתמש ב-tool get_turn_by_id
    - כשמשתמש מבקש להוסיף תור - השתמש ב-tool create_turn
    - כשמשתמש מבקש לעדכן תור - השתמש ב-tool update_turn
    - כשמשתמש מבקש למחוק תור - השתמש ב-tool delete_turn

    **כללי עבודה חשובים:**
    - אתה עוסק רק במערכת התינוקות, האחיות והתורנויות
    - אם המשתמש שואל על נושא אחר, השב בנימוס שזה לא קשור למערכת ושאתה יכול לעזור רק בפעולות CRUD של Babies, Nurses, Turns
    - לפני יצירה, עדכון או מחיקה בדוק שיש לך את כל הנתונים הדרושים
            - חוק מחייב:
        - אסור לבקש יותר מפרט חסר אחד בכל הודעה
        - בכל תגובה מותר לשאול שאלה אחת בלבד
        - אם חסרים כמה שדות, שאל רק על השדה הראשון שחסר
        - לאחר קבלת תשובת המשתמש, עבור שוב על כל השיחה וחפש איזה שדה חסר עכשיו
        - לעולם אל תציג רשימת פרטים חסרים
        - לעולם אל תמספר נתונים חסרים (1,2,3...)
        - אל תבקש כמה נתונים באותה הודעה גם אם חסרים הרבה
        - רק לאחר שכל הנתונים קיימים, הפעל tool
            - אם המשתמש נותן מידע בכמה הודעות:
        - שמור את כל הנתונים מההיסטוריה
        - שאל בכל פעם רק על פרט חסר אחד
        - אל תחזור על שאלה שנשאלה אם המידע כבר הופיע בשיחה
    - אל תבקש מהמשתמש לבדוק לבד אם מזהה קיים או אם פעולה הצליחה; אתה צריך להשתמש בכלים של המערכת כדי לבדוק ולבצע
    - אם כלי מחזיר הודעת שגיאה, הסבר אותה למשתמש בקצרה ואל תמציא סיבה אחרת
    - אם המשתמש כתב שם פרטי ושם משפחה באותו משפט, נסה לזהות אותם מתוך ההקשר ולשאול רק על השדות שבאמת חסרים
    - המשתמש לא אמור לדעת מזהים של תינוקות או תורים בזמן יצירה
    - ביצירת תינוק אל תבקש id
       -גיל תינוק לא יעלה על גיל 6, אם משתמש מבקש להוסיך תינוק מעל גיל 6 הסבר בנימוס שאין אפשרות
    -אל תחזור על אותה שאלה פעמיים!!אם המשתמש לא הביא עדיין נתונים חשובים תעבור שוב על כל השיחה ותנסה לחפש את הפרטים המתאימים,אם לא נמצא רק אז תחזור על השאלה -עם התנצלות.
    - ביצירת תור אל תבקש turn id, ועדיף לזהות תינוק לפי שם ושם משפחה ואחות לפי שם אם לא נמסרו מזהים
    - אם המשתמש כבר מסר פרט קודם בשיחה, זכור אותו ואל תבקש אותו שוב
    - אם המשתמש נותן תאריך או שעה בפורמט חופשי, המר אותם לפורמט המתאים בעצמך לפני הפעלת tool
    - אם המשתמש אומר שאין הערות, השתמש בערך ריק ואל תשאל שוב על הערות

**חוק אישור לפני פעולות הרסניות:**
    - לפני מחיקה או עדכון של תינוק, אחות או תור — תמיד הפעל קודם את כלי request_confirmation עם הודעה ברורה בעברית-בהודעה תפתח במילים -רק מוודא- או משפט דומה לשמירה על סגנון קליל
    - רק אם הודעת המשתמש האחרונה היא אישור מפורש ('כן', 'אשר', 'המשך', 'בסדר') — הפעל את הכלי המבצע
    - יצירה (create) אינה מצריכה אישור — בצע מיד

**התנהגות:**
- עונה בעברית בלבד
    - אם משתמש מבקש מידע או פעולה - השתמש בכלי המתאים
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
                        name = "request_confirmation",
                        description = "בקש אישור מהמשתמש לפני פעולה הרסנית כמו מחיקה או עדכון. הפעל כלי זה לפני delete_baby, delete_nurse, delete_turn, update_baby, update_nurse, update_turn — אלא אם המשתמש כבר אישר מפורשות.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                message_to_user = new { type = "string", description = "שאלת האישור בעברית. לדוגמה: 'אני עומד למחוק את התינוק יוסף לוי. להמשיך?'" }
                            },
                            required = new[] { "message_to_user" }
                        }
                    }
                },
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
                        name = "create_baby",
                        description = "צור תינוק חדש במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                name = new { type = "string", description = "שם התינוק" },
                                age = new { type = "string", description = "גיל התינוק" },
                                family = new { type = "string", description = "שם המשפחה" }
                            },
                        required = new string[] { }                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "update_baby",
                        description = "עדכן תינוק קיים במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה התינוק" },
                                name = new { type = "string", description = "שם התינוק" },
                                age = new { type = "string", description = "גיל התינוק" },
                                family = new { type = "string", description = "שם המשפחה" }
                            },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "delete_baby",
                        description = "מחק תינוק מהמערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה התינוק" }
                            },
                            required = new[] { "id" }
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
                        name = "create_nurse",
                        description = "צור אחות חדשה במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                name = new { type = "string", description = "שם האחות" },
                                phone = new { type = "string", description = "טלפון האחות" },
                                email = new { type = "string", description = "אימייל האחות" },
                                specialization = new { type = "string", description = "התמחות האחות" }
                            },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "update_nurse",
                        description = "עדכן אחות קיימת במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה האחות" },
                                name = new { type = "string", description = "שם האחות" },
                                phone = new { type = "string", description = "טלפון האחות" },
                                email = new { type = "string", description = "אימייל האחות" },
                                specialization = new { type = "string", description = "התמחות האחות" }
                            },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "delete_nurse",
                        description = "מחק אחות מהמערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה האחות" }
                            },
                            required = new[] { "id" }
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
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "create_turn",
                        description = "צור תור חדש במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                babyId = new { type = "string", description = "מזהה התינוק" },
                                babyName = new { type = "string", description = "שם התינוק" },
                                babyFamily = new { type = "string", description = "שם המשפחה של התינוק" },
                                nurseId = new { type = "string", description = "מזהה האחות" },
                                nurseName = new { type = "string", description = "שם האחות" },
                                date = new { type = "string", description = "תאריך התור. אפשר גם בפורמט חופשי כמו 29-10-2026 או כג אייר" },
                                time = new { type = "string", description = "שעת התור. אפשר גם כמו 14:00, 2 בצהריים, 12" },
                                notes = new { type = "string", description = "הערות לתור. אם אין, אפשר להשאיר ריק או לציין אין" }
                            },
                            required =new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "update_turn",
                        description = "עדכן תור קיים במערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה התור" },
                                babyId = new { type = "string", description = "מזהה התינוק" },
                                nurseId = new { type = "string", description = "מזהה האחות" },
                                date = new { type = "string", description = "תאריך התור. אפשר גם בפורמט חופשי" },
                                time = new { type = "string", description = "שעת התור. אפשר גם בפורמט חופשי" },
                                notes = new { type = "string", description = "הערות לתור. אם אין, אפשר להשאיר ריק" }
                            },
                            required = new string[] { }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "delete_turn",
                        description = "מחק תור מהמערכת",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "מזהה התור" }
                            },
                            required = new[] { "id" }
                        }
                    }
                }
            };
        }

        private static bool TryGetRequiredInt(Dictionary<string, string> parameters, out int value, params string[] keys)
        {
            value = 0;
            foreach (var key in keys)
            {
                if (parameters.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRequiredString(Dictionary<string, string> parameters, out string value, params string[] keys)
        {
            value = string.Empty;
            foreach (var key in keys)
            {
                if (parameters.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
                {
                    value = rawValue;
                    return true;
                }
            }

            return false;
        }

        private async Task<(bool Success, int BabyId, string ErrorMessage)> TryResolveBabyId(Dictionary<string, string> parameters)
        {
            if (TryGetRequiredInt(parameters, out var babyId, "babyId", "baby_id"))
            {
                var babyById = await _babiesService.GetByIdAsync(babyId);
                if (babyById != null)
                {
                    return (true, babyId, string.Empty);
                }

                return (false, 0, $"❌ לא קיים תינוק עם מזהה {babyId}");
            }

            if (!TryGetRequiredString(parameters, out var babyName, "babyName", "baby_name", "name") ||
                !TryGetRequiredString(parameters, out var babyFamily, "babyFamily", "baby_family", "family", "family_name", "last_name"))
            {
                return (false, 0, "❌ כדי ליצור תור צריך לזהות תינוק לפי babyId או לפי babyName ו-babyFamily");
            }

            var babies = await _babiesService.GetAllAsync();
            var match = babies.FirstOrDefault(b =>
                string.Equals(b.name, babyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(b.family, babyFamily, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return (false, 0, $"❌ לא מצאתי תינוק בשם {babyName} {babyFamily}");
            }

            return (true, match.id, string.Empty);
        }

        private async Task<(bool Success, int NurseId, string ErrorMessage)> TryResolveNurseId(Dictionary<string, string> parameters)
        {
            if (TryGetRequiredInt(parameters, out var nurseId, "nurseId", "nurse_id"))
            {
                var nurseById = await _nursesService.GetByIdAsync(nurseId);
                if (nurseById != null)
                {
                    return (true, nurseId, string.Empty);
                }

                return (false, 0, $"❌ לא קיימת אחות עם מזהה {nurseId}");
            }

            if (!TryGetRequiredString(parameters, out var nurseName, "nurseName", "nurse_name", "name"))
            {
                return (false, 0, "❌ כדי ליצור תור צריך לזהות אחות לפי nurseId או nurseName");
            }

            var nurses = await _nursesService.GetAllAsync();
            var matches = nurses
                .Where(n => string.Equals(n.name, nurseName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                return (false, 0, $"❌ לא מצאתי אחות בשם {nurseName}");
            }

            if (matches.Count > 1)
            {
                return (false, 0, $"❌ יש יותר מאחות אחת בשם {nurseName}. צריך לציין nurseId");
            }

            return (true, matches[0].id, string.Empty);
        }

        private Dictionary<string, string> MergeToolParameters(
            string toolName,
            Dictionary<string, string> parameters,
            List<(string role, string content)> conversationHistory,
            string userMessage)
        {
            var merged = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);

            if (toolName is not "create_turn" and not "update_turn")
            {
                return merged;
            }

            var allUserText = string.Join(
                "\n",
                conversationHistory
                    .Where(item => item.role == "user")
                    .Select(item => item.content)
                    .Append(userMessage));

            SetIfMissing(merged, "baby_id", FindLastMatch(allUserText, @"מזהה\s*תינוק\s*(\d+)"));
            SetIfMissing(merged, "nurse_id", FindLastMatch(allUserText, @"מזהה\s*אחות\s*(\d+)"));
            SetIfMissing(merged, "nurseName", FindLastMatch(allUserText, @"אחות\s+([א-תA-Za-z'\-]+)"));

            var fullBabyMatch = Regex.Matches(allUserText, @"(?:תינוק\s+בשם\s+)?([א-תA-Za-z'\-]+)\s+([א-תA-Za-z'\-]+)")
                .Cast<Match>()
                .LastOrDefault();

            if (fullBabyMatch is not null)
            {
                SetIfMissing(merged, "babyName", fullBabyMatch.Groups[1].Value);
                SetIfMissing(merged, "babyFamily", fullBabyMatch.Groups[2].Value);
            }

            var isoDate = FindLastMatch(allUserText, @"\b(\d{4}-\d{2}-\d{2}|\d{2}[\/\-.]\d{2}[\/\-.]\d{4})\b");
            SetIfMissing(merged, "date", isoDate);

            var timeMatch = FindLastMatch(allUserText, @"\b(\d{1,2}:\d{2}|\d{1,2}\s*(?:בבוקר|בצהריים|אחר הצהריים|בערב|בלילה)|\d{1,2}\s*וחצי)\b");
            SetIfMissing(merged, "time", timeMatch);

            if (!merged.ContainsKey("notes"))
            {
                var lastUserTrimmed = userMessage.Trim();
                if (lastUserTrimmed is "לא" or "אין" or "בלי הערות")
                {
                    merged["notes"] = string.Empty;
                }
            }

            return merged;
        }

        private static void SetIfMissing(Dictionary<string, string> parameters, string key, string? value)
        {
            if (!parameters.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
            {
                parameters[key] = value;
            }
        }

        private static string? FindLastMatch(string input, string pattern)
        {
            var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);
            if (matches.Count == 0)
            {
                return null;
            }

            var match = matches[^1];
            return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
        }

        private static string NormalizeNotes(Dictionary<string, string> parameters)
        {
            if (!TryGetRequiredString(parameters, out var notes, "notes", "note"))
            {
                return string.Empty;
            }

            return notes.Trim() switch
            {
                "אין" => string.Empty,
                "לא" => string.Empty,
                "בלי הערות" => string.Empty,
                _ => notes.Trim()
            };
        }

        private static bool TryResolveTurnSchedule(Dictionary<string, string> parameters, out string date, out string time, out string errorMessage)
        {
            date = string.Empty;
            time = string.Empty;
            errorMessage = string.Empty;

            if (!TryGetRequiredString(parameters, out var rawDate, "date", "dateText", "scheduleDate"))
            {
                errorMessage = "❌ חסר תאריך ליצירת תור";
                return false;
            }

            if (!TryGetRequiredString(parameters, out var rawTime, "time", "timeText", "scheduleTime"))
            {
                errorMessage = "❌ חסרה שעה ליצירת תור";
                return false;
            }

            if (!TryNormalizeDate(rawDate, out date))
            {
                errorMessage = "❌ לא הצלחתי להבין את התאריך. אפשר לכתוב למשל 2026-10-29 או 29-10-2026";
                return false;
            }

            if (!TryNormalizeTime(rawTime, out time))
            {
                errorMessage = "❌ לא הצלחתי להבין את השעה. אפשר לכתוב למשל 14:00 או 2 בצהריים";
                return false;
            }

            return true;
        }

        private static bool TryNormalizeDate(string input, out string normalizedDate)
        {
            normalizedDate = string.Empty;
            var cleaned = input.Trim();

            // תאריכים יחסיים
            var today = DateTime.Today;
            if (Regex.IsMatch(cleaned, @"^(היום|כיום)$"))
            {
                normalizedDate = today.ToString("yyyy-MM-dd");
                return true;
            }

            if (Regex.IsMatch(cleaned, @"^(מחר|מחרתיים)$"))
            {
                normalizedDate = today.AddDays(cleaned == "מחרתיים" ? 2 : 1).ToString("yyyy-MM-dd");
                return true;
            }

            if (Regex.IsMatch(cleaned, @"(הכי קרוב|מוקדם ככל|כמה שיותר קרוב)", RegexOptions.IgnoreCase))
            {
                normalizedDate = today.ToString("yyyy-MM-dd");
                return true;
            }

            // yyyy-MM-dd
            var isoMatch = Regex.Match(cleaned, @"\b(\d{4}-\d{2}-\d{2})\b");
            if (isoMatch.Success && DateTime.TryParseExact(isoMatch.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            {
                normalizedDate = isoDate.ToString("yyyy-MM-dd");
                return true;
            }

            // dd/MM/yyyy, dd-MM-yyyy, dd.MM.yyyy (4-digit year)
            var dmyFullMatch = Regex.Match(cleaned, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\b");
            if (dmyFullMatch.Success)
            {
                var attempt = $"{dmyFullMatch.Groups[1].Value.PadLeft(2, '0')}-{dmyFullMatch.Groups[2].Value.PadLeft(2, '0')}-{dmyFullMatch.Groups[3].Value}";
                if (DateTime.TryParseExact(attempt, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dmyDate))
                {
                    normalizedDate = dmyDate.ToString("yyyy-MM-dd");
                    return true;
                }
            }

            // dd/MM/yy (שנה דו-ספרתית)
            var dmyShortMatch = Regex.Match(cleaned, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{2})\b");
            if (dmyShortMatch.Success)
            {
                var shortYear = int.Parse(dmyShortMatch.Groups[3].Value);
                var fullYear = shortYear < 50 ? 2000 + shortYear : 1900 + shortYear;
                var attempt = $"{dmyShortMatch.Groups[1].Value.PadLeft(2, '0')}-{dmyShortMatch.Groups[2].Value.PadLeft(2, '0')}-{fullYear}";
                if (DateTime.TryParseExact(attempt, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortDate))
                {
                    normalizedDate = shortDate.ToString("yyyy-MM-dd");
                    return true;
                }
            }

            var hebrewGregorianMonthMatch = Regex.Match(cleaned, @"(\d{1,2})\s+ב?([א-ת]+)\s+(\d{4})");
            if (hebrewGregorianMonthMatch.Success)
            {
                var day = int.Parse(hebrewGregorianMonthMatch.Groups[1].Value);
                var monthName = hebrewGregorianMonthMatch.Groups[2].Value;
                var year = int.Parse(hebrewGregorianMonthMatch.Groups[3].Value);
                if (TryGetGregorianMonth(monthName, out var month))
                {
                    normalizedDate = new DateTime(year, month, day).ToString("yyyy-MM-dd");
                    return true;
                }
            }

            var hebrewDateMatch = Regex.Match(cleaned, @"([א-ת""׳׳']+)\s+([א-ת]+)(?:\s+(\d{4}))?");
            if (hebrewDateMatch.Success)
            {
                var hebrewDayText = NormalizeHebrewNumeral(hebrewDateMatch.Groups[1].Value);
                var hebrewMonthName = hebrewDateMatch.Groups[2].Value;
                var hebrewCalendar = new HebrewCalendar();
                var hebrewYear = hebrewDateMatch.Groups[3].Success
                    ? int.Parse(hebrewDateMatch.Groups[3].Value)
                    : hebrewCalendar.GetYear(today);

                if (TryParseHebrewNumber(hebrewDayText, out var hebrewDay) &&
                    TryGetHebrewMonth(hebrewMonthName, hebrewYear, out var hebrewMonth))
                {
                    try
                    {
                        var gregorianDate = hebrewCalendar.ToDateTime(hebrewYear, hebrewMonth, hebrewDay, 0, 0, 0, 0);
                        normalizedDate = gregorianDate.ToString("yyyy-MM-dd");
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        private static bool TryNormalizeTime(string input, out string normalizedTime)
        {
            normalizedTime = string.Empty;
            var cleaned = input.Trim();

            var timeMatch = Regex.Match(cleaned, @"\b(\d{1,2}):(\d{2})\b");
            if (timeMatch.Success)
            {
                normalizedTime = $"{int.Parse(timeMatch.Groups[1].Value):00}:{int.Parse(timeMatch.Groups[2].Value):00}";
                return true;
            }

            var halfMatch = Regex.Match(cleaned, @"\b(\d{1,2})\s*וחצי\b");
            if (halfMatch.Success)
            {
                normalizedTime = $"{int.Parse(halfMatch.Groups[1].Value):00}:30";
                return true;
            }

            var periodMatch = Regex.Match(cleaned, @"\b(\d{1,2})\b\s*(בבוקר|בצהריים|אחר הצהריים|בערב|בלילה)");
            if (periodMatch.Success)
            {
                var hour = int.Parse(periodMatch.Groups[1].Value);
                var period = periodMatch.Groups[2].Value;

                if ((period == "בצהריים" || period == "אחר הצהריים" || period == "בערב") && hour < 12)
                {
                    hour += 12;
                }

                if (period == "בלילה" && hour == 12)
                {
                    hour = 0;
                }

                normalizedTime = $"{hour:00}:00";
                return true;
            }

            var bareHourMatch = Regex.Match(cleaned, @"^\d{1,2}$");
            if (bareHourMatch.Success)
            {
                normalizedTime = $"{int.Parse(bareHourMatch.Value):00}:00";
                return true;
            }

            return false;
        }

        private static bool TryGetGregorianMonth(string monthName, out int month)
        {
            var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ינואר"] = 1,
                ["פברואר"] = 2,
                ["מרץ"] = 3,
                ["אפריל"] = 4,
                ["מאי"] = 5,
                ["יוני"] = 6,
                ["יולי"] = 7,
                ["אוגוסט"] = 8,
                ["ספטמבר"] = 9,
                ["אוקטובר"] = 10,
                ["נובמבר"] = 11,
                ["דצמבר"] = 12
            };

            return months.TryGetValue(monthName.Trim(), out month);
        }

        private static string NormalizeHebrewNumeral(string value)
        {
            return value.Replace("\"", string.Empty)
                .Replace("׳", string.Empty)
                .Replace("״", string.Empty)
                .Replace("'", string.Empty)
                .Trim();
        }

        private static bool TryParseHebrewNumber(string value, out int number)
        {
            number = 0;
            var map = new Dictionary<char, int>
            {
                ['א'] = 1, ['ב'] = 2, ['ג'] = 3, ['ד'] = 4, ['ה'] = 5, ['ו'] = 6, ['ז'] = 7, ['ח'] = 8, ['ט'] = 9,
                ['י'] = 10, ['כ'] = 20, ['ך'] = 20, ['ל'] = 30
            };

            foreach (var character in value)
            {
                if (!map.TryGetValue(character, out var current))
                {
                    return false;
                }

                number += current;
            }

            return number > 0;
        }

        private static bool TryGetHebrewMonth(string monthName, int hebrewYear, out int month)
        {
            var calendar = new HebrewCalendar();
            var isLeapYear = calendar.IsLeapYear(hebrewYear);
            var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["תשרי"] = 1,
                ["חשוון"] = 2,
                ["מרחשוון"] = 2,
                ["כסלו"] = 3,
                ["טבת"] = 4,
                ["שבט"] = 5,
                ["אדר"] = isLeapYear ? 7 : 6,
                ["אדר א"] = 6,
                ["אדר ב"] = 7,
                ["ניסן"] = isLeapYear ? 8 : 7,
                ["אייר"] = isLeapYear ? 9 : 8,
                ["סיוון"] = isLeapYear ? 10 : 9,
                ["סיון"] = isLeapYear ? 10 : 9,
                ["תמוז"] = isLeapYear ? 11 : 10,
                ["אב"] = isLeapYear ? 12 : 11,
                ["אלול"] = isLeapYear ? 13 : 12
            };

            return months.TryGetValue(monthName.Trim(), out month);
        }

    }
}
