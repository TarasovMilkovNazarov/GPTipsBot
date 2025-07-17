using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    public partial class RenamedBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Wallets");

            migrationBuilder.AddColumn<double>(
                name: "Balance",
                table: "Wallets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Wallets");

            migrationBuilder.AddColumn<long>(
                name: "Amount",
                table: "Wallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
