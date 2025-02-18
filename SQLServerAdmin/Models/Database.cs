namespace SQLServerAdmin.Models
{
    public class Database
    {
        public string Name { get; set; }
        public DateTime CreateDate { get; set; }
        public int CompatibilityLevel { get; set; }
        public string CollationName { get; set; }
        public decimal SizeInMB { get; set; }

        public Database(string name, DateTime createDate, int compatibilityLevel, string collationName, decimal sizeInMB)
        {
            Name = name;
            CreateDate = createDate;
            CompatibilityLevel = compatibilityLevel;
            CollationName = collationName;
            SizeInMB = sizeInMB;
        }
    }
}
