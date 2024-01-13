using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace little_heart_bot_3.Migrations
{
    public partial class Removebot_table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_table");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bot_table",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    app_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中"),
                    cookie = table.Column<string>(type: "varchar(2000)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的cookie")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    create_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)"),
                    csrf = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的csrf")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    dev_id = table.Column<string>(type: "varchar(36)", nullable: false, defaultValue: "", comment: "成为小心心bot的用户的dev_id")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receive_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中"),
                    send_status = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "0 正常, -1 冷却中, -2 禁言"),
                    uid = table.Column<long>(type: "bigint", nullable: false, comment: "成为小心心bot的用户的uid"),
                    update_time = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_table", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bot_table_uid",
                table: "bot_table",
                column: "uid",
                unique: true);
        }
    }
}
