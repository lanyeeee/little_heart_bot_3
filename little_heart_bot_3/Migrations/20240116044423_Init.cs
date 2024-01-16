using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace little_heart_bot_3.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    uid = table.Column<long>(type: "INTEGER", nullable: false),
                    cookie = table.Column<string>(type: "varchar(2000)", nullable: false, defaultValue: ""),
                    csrf = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: ""),
                    completed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "今日的任务是否已完成"),
                    cookie_status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0, comment: "0 未验证, 1 正常, -1 异常"),
                    config_num = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0, comment: "今日查了多少次配置"),
                    read_timestamp = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L, comment: "上一条已读私信的时间戳，用于找出未读私信"),
                    config_timestamp = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L, comment: "上一次查询配置的时间戳"),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')"),
                    update_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_table", x => x.id);
                    table.UniqueConstraint("AK_user_table_uid", x => x.uid);
                });

            migrationBuilder.CreateTable(
                name: "message_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    uid = table.Column<long>(type: "INTEGER", nullable: false, comment: "用户的uid"),
                    target_uid = table.Column<long>(type: "INTEGER", nullable: false),
                    target_name = table.Column<string>(type: "varchar(30)", nullable: false),
                    room_id = table.Column<long>(type: "INTEGER", nullable: false),
                    content = table.Column<string>(type: "varchar(30)", nullable: true, comment: "弹幕的内容"),
                    code = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    response = table.Column<string>(type: "json", nullable: true),
                    completed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "弹幕是否已发送"),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')"),
                    update_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_table", x => x.id);
                    table.ForeignKey(
                        name: "FK_message_table_user_table_uid",
                        column: x => x.uid,
                        principalTable: "user_table",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "target_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    uid = table.Column<long>(type: "INTEGER", nullable: false, comment: "用户的uid"),
                    target_uid = table.Column<long>(type: "INTEGER", nullable: false, comment: "直播间主播的uid"),
                    target_name = table.Column<string>(type: "varchar(30)", nullable: false, comment: "直播间主播的名字"),
                    room_id = table.Column<long>(type: "INTEGER", nullable: false, comment: "直播间的room_id"),
                    exp = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0, comment: "今日已获得的经验"),
                    watched_seconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0, comment: "今日已观看直播的时长"),
                    completed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "今日任务是否已完成"),
                    create_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')"),
                    update_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now', 'localtime')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_table", x => x.id);
                    table.ForeignKey(
                        name: "FK_target_table_user_table_uid",
                        column: x => x.uid,
                        principalTable: "user_table",
                        principalColumn: "uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_message_table_uid_target_uid",
                table: "message_table",
                columns: new[] { "uid", "target_uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_table_uid_target_uid",
                table: "target_table",
                columns: new[] { "uid", "target_uid" },
                unique: true);

            migrationBuilder.Sql(@"
CREATE TRIGGER user_table_update_time_trigger
    AFTER UPDATE
    ON user_table
    FOR EACH ROW
BEGIN
    UPDATE user_table SET update_time = datetime('now', 'localtime') WHERE id = old.id;
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER message_table_update_time_trigger
    AFTER UPDATE
    ON message_table
    FOR EACH ROW
BEGIN
    UPDATE message_table SET update_time = datetime('now', 'localtime') WHERE id = old.id;
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER target_table_update_time_trigger
    AFTER UPDATE
    ON target_table
    FOR EACH ROW
BEGIN
    UPDATE target_table SET update_time = datetime('now', 'localtime') WHERE id = old.id;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message_table");

            migrationBuilder.DropTable(
                name: "target_table");

            migrationBuilder.DropTable(
                name: "user_table");
        }
    }
}
