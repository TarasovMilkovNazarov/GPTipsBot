using GPTipsBot.Config;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    [Migration("20260819100000_AddFreeAlicePhotoQuotas")]
    public partial class AddFreeAlicePhotoQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeCombinePhotos",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: PaymentConfig.NewbieFreeCombinePhotos);

            migrationBuilder.AddColumn<int>(
                name: "FreeChangePhotos",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: PaymentConfig.NewbieFreeChangePhotos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeCombinePhotos",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FreeChangePhotos",
                table: "Users");
        }
    }
}
