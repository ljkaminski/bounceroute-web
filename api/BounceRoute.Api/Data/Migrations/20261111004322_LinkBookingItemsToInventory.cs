using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkBookingItemsToInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryItemId",
                table: "BookingItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWet",
                table: "BookingItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingItems_InventoryItemId",
                table: "BookingItems",
                column: "InventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingItems_InventoryItems_InventoryItemId",
                table: "BookingItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingItems_InventoryItems_InventoryItemId",
                table: "BookingItems");

            migrationBuilder.DropIndex(
                name: "IX_BookingItems_InventoryItemId",
                table: "BookingItems");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "BookingItems");

            migrationBuilder.DropColumn(
                name: "IsWet",
                table: "BookingItems");
        }
    }
}
