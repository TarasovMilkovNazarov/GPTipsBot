using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    public partial class AddedBotSettingsUserFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BotSettingsId",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Users_BotSettingsId",
                table: "Users",
                column: "BotSettingsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BotSettings_BotSettingsId",
                table: "Users",
                column: "BotSettingsId",
                principalTable: "BotSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_BotSettings_BotSettingsId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_BotSettingsId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BotSettingsId",
                table: "Users");
        }
    }
}
