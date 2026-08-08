using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260808140000_AddMessageThreadId")]
    public partial class AddMessageThreadId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MessageThreadId",
                table: "Messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId_MessageThreadId_CreatedAt",
                table: "Messages",
                columns: new[] { "ChatId", "MessageThreadId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatId_MessageThreadId_CreatedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageThreadId",
                table: "Messages");
        }
    }
}
