public class AppState
{
    public bool HasData { get; set; }
    public List<InternetRecord> Records { get; set; } = new();
}
