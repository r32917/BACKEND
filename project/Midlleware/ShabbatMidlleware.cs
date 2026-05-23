namespace project.Midlleware
{
    public class ShabbatMidlleware
    {
        private readonly RequestDelegate _next;
        public ShabbatMidlleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task InvokeAsync(HttpContext context)
        {
            var shabbat = true;
            if (DateTime.Now.DayOfWeek==DayOfWeek.Saturday)
            {
                var message = "השבת היא ה-❤️ של העם היהודי ";
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(message);
            }
            else
            {
                await _next(context);
            }
        }
    }
}
