using System.Text.Json.Nodes;
using little_heart_bot_3.Data.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace little_heart_bot_3.Data;

public class LittleHeartDbContext : DbContext
{
    public DbSet<BotModel> Bots { get; set; } = null!;
    public DbSet<UserModel> Users { get; set; } = null!;
    public DbSet<MessageModel> Messages { get; set; } = null!;
    public DbSet<TargetModel> Targets { get; set; } = null!;

    public LittleHeartDbContext()
    {
    }

    public LittleHeartDbContext(DbContextOptions<LittleHeartDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        base.OnModelCreating(modelBuilder);
    }
}