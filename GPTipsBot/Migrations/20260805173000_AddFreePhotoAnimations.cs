using GPTipsBot.Config;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationContext))]
    [Migration("20260805173000_AddFreePhotoAnimations")]
    public partial class AddFreePhotoAnimations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreePhotoAnimations",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: PaymentConfig.NewbieFreePhotoAnimations);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreePhotoAnimations",
                table: "Users");
        }
    }
}
