using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace little_heart_bot_3.Data.Models;

public class BotModel
{
    public int Id { get; set; }
    public long Uid { get; set; }
    public string Cookie { get; set; } = string.Empty;
    public string Csrf { get; set; } = string.Empty;
    public string DevId { get; set; } = string.Empty;
    public AppStatus AppStatus { get; set; }
    public ReceiveStatus ReceiveStatus { get; set; }
    public SendStatus SendStatus { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }
}

public class BotModelEntityTypeConfiguration : IEntityTypeConfiguration<BotModel>
{
    public void Configure(EntityTypeBuilder<BotModel> builder)
    {
        builder.ToTable("bot_table");

        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.Uid).IsUnique();

        #region Property

        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.Uid)
            .HasColumnName("uid")
            .HasComment("成为小心心bot的用户的uid");

        builder.Property(b => b.Cookie)
            .HasColumnName("cookie")
            .HasColumnType("varchar(2000)")
            .HasDefaultValue(string.Empty)
            .HasComment("成为小心心bot的用户的cookie");

        builder.Property(b => b.Csrf)
            .HasColumnName("csrf")
            .HasColumnType("varchar(32)")
            .HasDefaultValue(string.Empty)
            .HasComment("成为小心心bot的用户的csrf");

        builder.Property(b => b.DevId)
            .HasColumnName("dev_id")
            .HasColumnType("varchar(36)")
            .HasDefaultValue(string.Empty)
            .HasComment("成为小心心bot的用户的dev_id");

        builder.Property(b => b.AppStatus)
            .HasColumnName("app_status")
            .HasDefaultValue(AppStatus.Normal)
            .HasComment("0 正常, -1 冷却中");

        builder.Property(b => b.ReceiveStatus)
            .HasColumnName("receive_status")
            .HasDefaultValue(ReceiveStatus.Normal)
            .HasComment("0 正常, -1 冷却中");

        builder.Property(b => b.SendStatus)
            .HasColumnName("send_status")
            .HasDefaultValue(SendStatus.Normal)
            .HasComment("0 正常, -1 冷却中, -2 禁言");


        builder.Property(b => b.CreateTime)
            .HasColumnName("create_time")
            .HasDefaultValueSql("NOW(6)");

        builder.Property(b => b.UpdateTime)
            .HasColumnName("update_time")
            .HasDefaultValueSql("NOW(6)");

        #endregion
    }
}

public enum AppStatus
{
    Normal = 0,
    Cooling = -1
}

public enum ReceiveStatus
{
    Normal = 0,
    Cooling = -1
}

public enum SendStatus
{
    Normal = 0,
    Cooling = -1,
    Forbidden = -2
}