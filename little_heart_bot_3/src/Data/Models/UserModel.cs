using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace little_heart_bot_3.Data.Models;

public class UserModel
{
    public int Id { get; init; }
    public required long Uid { get; init; }
    public required string Cookie { get; set; }
    public required string Csrf { get; set; }
    public bool Completed { get; set; }
    public CookieStatus CookieStatus { get; set; }
    public int ConfigNum { get; set; }
    public long ReadTimestamp { get; set; }
    public long ConfigTimestamp { get; set; }
    public DateTime CreateTime { get; init; }
    public DateTime UpdateTime { get; init; }

    public List<MessageModel> Messages { get; } = [];
    public List<TargetModel> Targets { get; } = [];
}

public class UserModelEntityTypeConfiguration : IEntityTypeConfiguration<UserModel>
{
    public void Configure(EntityTypeBuilder<UserModel> builder)
    {
        builder.ToTable("user_table");

        builder.HasKey(u => u.Id);

        builder.HasAlternateKey(u => u.Uid);

        builder.HasMany(u => u.Messages)
            .WithOne(m => m.UserModel)
            .HasPrincipalKey(u => u.Uid)
            .HasForeignKey(m => m.Uid)
            .IsRequired();

        builder.HasMany(u => u.Targets)
            .WithOne(t => t.UserModel)
            .HasPrincipalKey(u => u.Uid)
            .HasForeignKey(t => t.Uid)
            .IsRequired();

        #region Property

        builder.Property(u => u.Id)
            .HasColumnName("id");

        builder.Property(u => u.Uid)
            .HasColumnName("uid");

        builder.Property(u => u.Cookie)
            .HasColumnName("cookie")
            .HasColumnType("varchar(2000)")
            .HasDefaultValue(string.Empty);

        builder.Property(u => u.Csrf)
            .HasColumnName("csrf")
            .HasColumnType("varchar(32)")
            .HasDefaultValue(string.Empty);

        builder.Property(u => u.Completed)
            .HasColumnName("completed")
            .HasDefaultValue(false)
            .HasComment("今日的任务是否已完成");

        builder.Property(u => u.CookieStatus)
            .HasColumnName("cookie_status")
            .HasDefaultValue(CookieStatus.Unverified)
            .HasComment("0 未验证, 1 正常, -1 异常");

        builder.Property(u => u.ConfigNum)
            .HasColumnName("config_num")
            .HasDefaultValue(0)
            .HasComment("今日查了多少次配置");

        builder.Property(u => u.ReadTimestamp)
            .HasColumnName("read_timestamp")
            .HasDefaultValue(0)
            .HasComment("上一条已读私信的时间戳，用于找出未读私信");

        builder.Property(u => u.ConfigTimestamp)
            .HasColumnName("config_timestamp")
            .HasDefaultValue(0)
            .HasComment("上一次查询配置的时间戳");

        builder.Property(u => u.CreateTime)
            .HasColumnName("create_time")
            .HasDefaultValueSql("datetime('now', 'localtime')");

        builder.Property(u => u.UpdateTime)
            .HasColumnName("update_time")
            .HasDefaultValueSql("datetime('now', 'localtime')");

        #endregion
    }
}

public enum CookieStatus
{
    Unverified = 0,
    Normal = 1,
    Error = -1
}