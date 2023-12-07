using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace little_heart_bot_3.Data.Models;

public class TargetModel
{
    public int Id { get; set; }
    public long Uid { get; set; }
    public long TargetUid { get; set; }
    public string TargetName { get; set; } = null!;
    public long RoomId { get; set; }
    public int Exp { get; set; }
    public int WatchedSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    public UserModel UserModel { get; set; } = null!;
}

public class TargetModelEntityTypeConfiguration : IEntityTypeConfiguration<TargetModel>
{
    public void Configure(EntityTypeBuilder<TargetModel> builder)
    {
        builder.ToTable("target_table");

        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.UserModel)
            .WithMany(u => u.Targets)
            .HasPrincipalKey(u => u.Uid)
            .HasForeignKey(t => t.Uid)
            .IsRequired();

        #region Property

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.Uid)
            .HasColumnName("uid")
            .HasComment("用户的uid");

        builder.Property(t => t.TargetUid)
            .HasColumnName("target_uid")
            .HasComment("直播间主播的uid");

        builder.Property(t => t.TargetName)
            .HasColumnName("target_name")
            .HasColumnType("varchar(30)")
            .HasComment("直播间主播的名字");

        builder.Property(t => t.RoomId)
            .HasColumnName("room_id")
            .HasComment("直播间的room_id");

        builder.Property(t => t.Exp)
            .HasColumnName("exp")
            .HasDefaultValue(0)
            .HasComment("今日已获得的经验");

        builder.Property(t => t.WatchedSeconds)
            .HasColumnName("watched_seconds")
            .HasDefaultValue(0)
            .HasComment("今日已观看直播的时长");

        builder.Property(t => t.Completed)
            .HasColumnName("completed")
            .HasDefaultValue(false)
            .HasComment("今日任务是否已完成");

        builder.Property(t => t.CreateTime)
            .HasColumnName("create_time")
            .HasDefaultValueSql("NOW(6)");

        builder.Property(t => t.UpdateTime)
            .HasColumnName("update_time")
            .HasDefaultValueSql("NOW(6)");

        #endregion
    }
}