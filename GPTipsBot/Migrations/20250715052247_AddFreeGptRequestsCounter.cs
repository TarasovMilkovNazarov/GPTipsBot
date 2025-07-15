using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeGptRequestsCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositDate",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "FreeGptRequests",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeGptRequests",
                table: "Users");

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositDate",
                table: "Wallets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "Balance",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
