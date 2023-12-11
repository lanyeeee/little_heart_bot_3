using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace little_heart_bot_3.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bot_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    uid = table.Column<long>(type: "bigint", nullable: false, comment: "成为小心心bot的用户的uid"),
                    cookie = table.Column<string>(type: "varchar(2000)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的cookie")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    csrf = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的csrf")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dev_id = table.Column<string>(type: "varchar(36)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的dev_id")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    app_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中"),
                    receive_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中"),
                    send_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中, -2 禁言"),
                    create_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)"),
                    update_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_table", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    uid = table.Column<long>(type: "bigint", nullable: false),
                    cookie = table.Column<string>(type: "varchar(2000)", nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    csrf = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "今日的任务是否已完成"),
                    cookie_status = table.Column<int>(type: "int", nullable: false, defaultValue: 1, comment: "0 未验证, 1 正常, -1 异常"),
                    config_num = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "今日查了多少次配置"),
                    read_timestamp = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L, comment: "上一条已读私信的时间戳，用于找出未读私信"),
                    config_timestamp = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L, comment: "上一次查询配置的时间戳"),
                    create_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)"),
                    update_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_table", x => x.id);
                    table.UniqueConstraint("AK_user_table_uid", x => x.uid);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "message_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    uid = table.Column<long>(type: "bigint", nullable: false, comment: "用户的uid"),
                    target_uid = table.Column<long>(type: "bigint", nullable: false),
                    target_name = table.Column<string>(type: "varchar(30)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    room_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "varchar(30)", nullable: true, comment: "弹幕的内容")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    response = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "弹幕是否已发送"),
                    create_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)"),
                    update_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "target_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    uid = table.Column<long>(type: "bigint", nullable: false, comment: "用户的uid"),
                    target_uid = table.Column<long>(type: "bigint", nullable: false, comment: "直播间主播的uid"),
                    target_name = table.Column<string>(type: "varchar(30)", nullable: false, comment: "直播间主播的名字")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    room_id = table.Column<long>(type: "bigint", nullable: false, comment: "直播间的room_id"),
                    exp = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "今日已获得的经验"),
                    watched_seconds = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "今日已观看直播的时长"),
                    completed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false, comment: "今日任务是否已完成"),
                    create_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)"),
                    update_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bot_table_uid",
                table: "bot_table",
                column: "uid",
                unique: true);

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
CREATE TRIGGER bot_table_update_time_trigger
BEFORE UPDATE
ON bot_table FOR EACH ROW
BEGIN
SET NEW.update_time = NOW(6);
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER user_table_update_time_trigger
BEFORE UPDATE
ON user_table FOR EACH ROW
BEGIN
SET NEW.update_time = NOW(6);
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER message_table_update_time_trigger
BEFORE UPDATE
ON message_table FOR EACH ROW
BEGIN
SET NEW.update_time = NOW(6);
END;");
            migrationBuilder.Sql(@"
CREATE TRIGGER target_table_update_time_trigger
BEFORE UPDATE
ON target_table FOR EACH ROW
BEGIN
SET NEW.update_time = NOW(6);
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_table");

            migrationBuilder.DropTable(
                name: "message_table");

            migrationBuilder.DropTable(
                name: "target_table");

            migrationBuilder.DropTable(
                name: "user_table");
        }
    }
}
