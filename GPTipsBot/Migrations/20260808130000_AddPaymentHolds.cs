using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260808130000_AddPaymentHolds")]
    public partial class AddPaymentHolds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentHolds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Feature = table.Column<int>(type: "integer", nullable: false),
                    UsedFreeQuota = table.Column<bool>(type: "boolean", nullable: false),
                    WalletAmount = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentHolds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHolds_Status_CreatedAt",
                table: "PaymentHolds",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentHolds_UserId",
                table: "PaymentHolds",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentHolds");
        }
    }
}
