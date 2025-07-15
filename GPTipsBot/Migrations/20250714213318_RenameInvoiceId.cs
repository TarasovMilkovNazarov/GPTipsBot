using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    public partial class RenameInvoiceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TelegramPaymentId",
                table: "Invoices",
                newName: "TelegramInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TelegramInvoiceId",
                table: "Invoices",
                newName: "TelegramPaymentId");
        }
    }
}
