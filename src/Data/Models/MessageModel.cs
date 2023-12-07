using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace little_heart_bot_3.Data.Models;

public class MessageModel
{
    public int Id { get; set; }
    public long Uid { get; set; }
    public long TargetUid { get; set; }
    public string TargetName { get; set; } = null!;
    public long RoomId { get; set; }
    public string? Content { get; set; } = null!;
    public int Code { get; set; }
    public string? Response { get; set; }
    public bool Completed { get; set; }

    public UserModel UserModel { get; set; } = null!;

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}

public class MessageModelEntityTypeConfiguration : IEntityTypeConfiguration<MessageModel>
{
    public void Configure(EntityTypeBuilder<MessageModel> builder)
    {
        builder.ToTable("message_table");

        builder.HasKey(m => m.Id);

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
            .HasDefaultValueSql("NOW(6)")
            .HasColumnName("create_time");

        builder.Property(m => m.UpdateTime)
            .HasColumnName("update_time")
            .HasDefaultValueSql("NOW(6)");

        #endregion
    }
}