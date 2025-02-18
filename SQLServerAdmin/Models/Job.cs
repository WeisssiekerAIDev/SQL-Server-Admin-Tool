namespace SQLServerAdmin.Models
{
    public class Job
    {
        public string JobId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime LastRun { get; set; }
        public string LastRunOutcome { get; set; }
        public DateTime NextRun { get; set; }
        
        public Job(string jobId, string name, string description, string status, 
                  DateTime lastRun, string lastRunOutcome, DateTime nextRun)
        {
            JobId = jobId;
            Name = name;
            Description = description;
            Status = status;
            LastRun = lastRun;
            LastRunOutcome = lastRunOutcome;
            NextRun = nextRun;
        }
    }
}
