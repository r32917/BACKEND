namespace project.Models.TurnesModel
{
    public class TurnesPutModel
    {
        public int id { get; set; }

        public string date { get; set; } = string.Empty;

        public string time { get; set; } = string.Empty;

        public string notes { get; set; } = string.Empty;

        public int babyId { get; set; }

        public int nurseId { get; set; }
    }
}
