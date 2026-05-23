namespace project
{
    public class Turns
    {
        public int id { get; set; }

        public string date { get; set; } = string.Empty;

        public string time { get; set; } = string.Empty;

        public string notes { get; set; } = string.Empty;

        public int babyId {  get; set;}

        public int nurseId {  get; set;}
        public Babies? baby { get; set; }
        public Nurses? nurse { get; set;}
    }
}
