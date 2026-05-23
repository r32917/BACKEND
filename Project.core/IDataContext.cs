namespace project
{
    public interface IDataContext
    {
        public List<Babies> babies { get; set; }
        public List<Nurses> nurses { get; set; }
        public List<Turns> turns { get; set; }
    }
}
