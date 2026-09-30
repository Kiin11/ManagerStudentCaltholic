namespace ManagerStudentCaltholic.Models.ViewModels
{
    public class LogEntryItem
    {
        public DateTime? Timestamp { get; set; }
        public string Level { get; set; } = "INF"; // INF, WRN, ERR, FTL, DBG
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string RawLine { get; set; } = string.Empty;
    }

    public class SystemLogsViewModel
    {
        public List<string> AvailableLogFiles { get; set; } = new();
        public string SelectedFile { get; set; } = string.Empty;
        public string? LevelFilter { get; set; }
        public string? SearchKeyword { get; set; }
        public int TotalLines { get; set; }
        public List<LogEntryItem> Entries { get; set; } = new();
    }
}
