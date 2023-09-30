using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GPTipsBot.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "BotSettings",
            //    columns: table => new
            //    {
            //        Id = table.Column<long>(type: "bigint", nullable: false)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        Language = table.Column<string>(type: "text", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_BotSettings", x => x.Id);
            //    });

            migrationBuilder.CreateTable(
                name: "OpenaiAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Password = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FreezedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Balance = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenaiAccounts", x => x.Id);
                });

            //migrationBuilder.CreateTable(
            //    name: "Users",
            //    columns: table => new
            //    {
            //        Id = table.Column<long>(type: "bigint", nullable: false)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        FirstName = table.Column<string>(type: "text", nullable: false),
            //        LastName = table.Column<string>(type: "text", nullable: true),
            //        Source = table.Column<string>(type: "text", nullable: true),
            //        CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            //        IsActive = table.Column<bool>(type: "boolean", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Users", x => x.Id);
            //    });

            //migrationBuilder.CreateTable(
            //    name: "Messages",
            //    columns: table => new
            //    {
            //        Id = table.Column<long>(type: "bigint", nullable: false)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        TelegramMessageId = table.Column<long>(type: "bigint", nullable: true),
            //        ContextId = table.Column<long>(type: "bigint", nullable: true)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        ReplyToId = table.Column<long>(type: "bigint", nullable: true),
            //        Text = table.Column<string>(type: "text", nullable: false),
            //        UserId = table.Column<long>(type: "bigint", nullable: false),
            //        ChatId = table.Column<long>(type: "bigint", nullable: false),
            //        CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            //        Role = table.Column<int>(type: "integer", nullable: false),
            //        ContextBound = table.Column<bool>(type: "boolean", nullable: false)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_Messages", x => x.Id);
            //        table.ForeignKey(
            //            name: "FK_Messages_Messages_ReplyToId",
            //            column: x => x.ReplyToId,
            //            principalTable: "Messages",
            //            principalColumn: "Id");
            //        table.ForeignKey(
            //            name: "FK_Messages_Users_UserId",
            //            column: x => x.UserId,
            //            principalTable: "Users",
            //            principalColumn: "Id",
            //            onDelete: ReferentialAction.Cascade);
            //    });

            //migrationBuilder.CreateIndex(
            //    name: "IX_Messages_ReplyToId",
            //    table: "Messages",
            //    column: "ReplyToId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_Messages_UserId",
            //    table: "Messages",
            //    column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "BotSettings");

            //migrationBuilder.DropTable(
            //    name: "Messages");

            migrationBuilder.DropTable(
                name: "OpenaiAccounts");

            //migrationBuilder.DropTable(
            //    name: "Users");
        }
    }
}
