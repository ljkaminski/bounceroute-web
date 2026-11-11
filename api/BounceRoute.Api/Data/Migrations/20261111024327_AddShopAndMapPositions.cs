using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShopAndMapPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShopAddress",
                table: "CompanySettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "ShopLatitude",
                table: "CompanySettings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ShopLongitude",
                table: "CompanySettings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodedAddress",
                table: "Bookings",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Bookings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Bookings",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShopAddress",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ShopLatitude",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "ShopLongitude",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "GeocodedAddress",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Bookings");
        }
    }
}
