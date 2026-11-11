using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrucksOutPerDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TruckDays",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TruckId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckDays", x => new { x.Date, x.TruckId });
                    table.ForeignKey(
                        name: "FK_TruckDays_Trucks_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Trucks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TruckDays_TruckId",
                table: "TruckDays",
                column: "TruckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TruckDays");
        }
    }
}
