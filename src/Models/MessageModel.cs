namespace little_heart_bot_3.Models;

public class MessageModel
{
    public int Id { get; set; }
    public string? Uid { get; set; }
    public string? TargetUid { get; set; }
    public string? TargetName { get; set; }
    public string? RoomId { get; set; }
    public string? Content { get; set; }
    public int? Code { get; set; }
    public string? Response { get; set; }
    public int Completed { get; set; }
}