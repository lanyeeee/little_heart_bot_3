namespace little_heart_bot_3.Models;

public class TargetModel
{
    public int Id { get; set; }
    public string? Uid { get; set; }
    public string? TargetUid { get; set; }
    public string? TargetName { get; set; }
    public string? RoomId { get; set; }
    public int Exp { get; set; }
    public int WatchedSeconds { get; set; }
    public int Completed { get; set; }
}