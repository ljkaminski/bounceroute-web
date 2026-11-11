using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SetupMinutes",
                table: "InventoryItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategorySetupTimes",
                columns: table => new
                {
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorySetupTimes", x => x.Category);
                });

            migrationBuilder.InsertData(
                table: "CategorySetupTimes",
                columns: new[] { "Category", "Minutes" },
                values: new object[,]
                {
                    { "BounceHouse", 20 },
                    { "Combo", 30 },
                    { "Equipment", 5 },
                    { "Game", 15 },
                    { "ObstacleCourse", 30 },
                    { "Other", 10 },
                    { "SlipNSlide", 20 },
                    { "WaterSlide", 45 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategorySetupTimes");

            migrationBuilder.DropColumn(
                name: "SetupMinutes",
                table: "InventoryItems");
        }
    }
}
