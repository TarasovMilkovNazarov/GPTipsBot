using System;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260805080000_AddYooKassaPayment")]
    public partial class AddYooKassaPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalPaymentId",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FiatAmountKopecks",
                table: "Invoices",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExternalPaymentId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FiatAmountKopecks",
                table: "Invoices");
        }
    }
}
