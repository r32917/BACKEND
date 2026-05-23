namespace project
{
    public class Babies
    {
        public int id { get; set; }

        public string name { get; set; } = string.Empty;

        public int age { get; set; }

        public string family { get; set; } = string.Empty;

        public Turns ? turn { get; set; }
    }
}
