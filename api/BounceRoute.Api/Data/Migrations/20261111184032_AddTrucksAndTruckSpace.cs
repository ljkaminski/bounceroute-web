using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BounceRoute.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrucksAndTruckSpace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TruckSpace",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Trucks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trucks", x => x.Id);
                    table.CheckConstraint("CK_Trucks_Capacity", "\"Capacity\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "TruckCrewMember",
                columns: table => new
                {
                    TruckId = table.Column<int>(type: "integer", nullable: false),
                    CrewMemberId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruckCrewMember", x => new { x.TruckId, x.CrewMemberId });
                    table.ForeignKey(
                        name: "FK_TruckCrewMember_CrewMembers_CrewMemberId",
                        column: x => x.CrewMemberId,
                        principalTable: "CrewMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TruckCrewMember_Trucks_TruckId",
                        column: x => x.TruckId,
                        principalTable: "Trucks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TruckCrewMember_CrewMemberId",
                table: "TruckCrewMember",
                column: "CrewMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Trucks_Name",
                table: "Trucks",
                column: "Name",
                unique: true);

            // Trucks used to be typed in on each job. Start the truck list with every name already in use.
            migrationBuilder.Sql("""
                INSERT INTO "Trucks" ("Name", "Capacity", "IsActive")
                SELECT DISTINCT "Truck", 6, TRUE FROM "Jobs" WHERE "Truck" <> ''
                ON CONFLICT DO NOTHING;
                """);

            // Equipment like a generator rides along without taking a unit's spot.
            migrationBuilder.Sql("""UPDATE "InventoryItems" SET "TruckSpace" = 0 WHERE "Category" = 'Equipment';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TruckCrewMember");

            migrationBuilder.DropTable(
                name: "Trucks");

            migrationBuilder.DropColumn(
                name: "TruckSpace",
                table: "InventoryItems");
        }
    }
}
