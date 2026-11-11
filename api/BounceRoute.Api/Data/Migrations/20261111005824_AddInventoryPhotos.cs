using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhotoVersion",
                table: "InventoryItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryPhotos",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPhotos", x => x.InventoryItemId);
                    table.ForeignKey(
                        name: "FK_InventoryPhotos_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryPhotos");

            migrationBuilder.DropColumn(
                name: "PhotoVersion",
                table: "InventoryItems");
        }
    }
}
