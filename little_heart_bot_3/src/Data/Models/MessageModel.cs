using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace little_heart_bot_3.Data.Models;

public class MessageModel
{
    public int Id { get; init; }
    public required long Uid { get; init; }
    public required long TargetUid { get; init; }
    public required string TargetName { get; init; }
    public required long RoomId { get; init; }
    public string? Content { get; set; }
    public int Code { get; set; }
    public string? Response { get; set; }
    public bool Completed { get; set; }

    public UserModel UserModel { get; init; } = null!;

    public DateTime CreateTime { get; init; }

    public DateTime UpdateTime { get; init; }
}

public class MessageModelEntityTypeConfiguration : IEntityTypeConfiguration<MessageModel>
{
    public void Configure(EntityTypeBuilder<MessageModel> builder)
    {
        builder.ToTable("message_table");

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.Uid, m.TargetUid })
            .IsUnique();

        builder.HasOne(m => m.UserModel)
            .WithMany(u => u.Messages)
            .HasPrincipalKey(u => u.Uid)
            .HasForeignKey(m => m.Uid)
            .IsRequired();

        #region Property

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.Uid)
            .HasColumnName("uid")
            .HasComment("用户的uid");

        builder.Property(m => m.TargetUid)
            .HasColumnName("target_uid");

        builder.Property(m => m.TargetName)
            .HasColumnName("target_name")
            .HasColumnType("varchar(30)");

        builder.Property(m => m.RoomId)
            .HasColumnName("room_id");

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasColumnType("varchar(30)")
            .HasComment("弹幕的内容");

        builder.Property(m => m.Code)
            .HasColumnName("code")
            .HasDefaultValue(0);

        builder.Property(m => m.Response)
            .HasColumnName("response")
            .HasColumnType("json");

        builder.Property(m => m.Completed)
            .HasColumnName("completed")
            .HasDefaultValue(false)
            .HasComment("弹幕是否已发送");

        builder.Property(m => m.CreateTime)
            .HasColumnName("create_time")
            .HasDefaultValueSql("datetime('now', 'localtime')");

        builder.Property(m => m.UpdateTime)
            .HasColumnName("update_time")
            .HasDefaultValueSql("datetime('now', 'localtime')");

        #endregion
    }
}