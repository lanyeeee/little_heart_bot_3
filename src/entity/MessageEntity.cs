namespace little_heart_bot_3.entity;

public class MessageEntity
{
    public int Id { get; set; }
    public string Uid { get; set; }
    public string TargetId { get; set; }
    public string TargetName { get; set; }
    public string RoomId { get; set; }
    public string Content { get; set; }
    public int Status { get; set; }
    public int Completed { get; set; }
}