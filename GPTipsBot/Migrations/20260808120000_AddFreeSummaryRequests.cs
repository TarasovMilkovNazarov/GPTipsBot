using GPTipsBot.Config;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260808120000_AddFreeSummaryRequests")]
    public partial class AddFreeSummaryRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeSummaryRequests",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: PaymentConfig.NewbieFreeSummaries);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeSummaryRequests",
                table: "Users");
        }
    }
}
