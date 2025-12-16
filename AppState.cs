public class AppState
{
    public bool HasData { get; set; }
    public List<InternetRecord> Records { get; set; } = new();

    // Dynamic raw table storage
    public List<string> RawHeaders { get; set; } = new();
    public List<Dictionary<string, string>> RawRows { get; set; } = new();
}
