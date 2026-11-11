using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PartyEndsAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PartyStartsAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PartyEndsAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PartyStartsAt",
                table: "Bookings");
        }
    }
}
