using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260808150000_AddPreferredGptModel")]
    public partial class AddPreferredGptModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredGptModel",
                table: "BotSettings",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredGptModel",
                table: "BotSettings");
        }
    }
}
