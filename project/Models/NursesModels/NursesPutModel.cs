namespace project.Models.NursesModels
{
    public class NursesPutModel
    {
        public int id { get; set; }

        public string name { get; set; } = string.Empty;

        public string phone { get; set; } = string.Empty;

        public string email { get; set; } = string.Empty;

        public string specialization { get; set; } = string.Empty;
    }
}
