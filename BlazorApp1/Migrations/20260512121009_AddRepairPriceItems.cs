using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyBlazorSite.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairPriceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaintCost",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "PartsCost",
                table: "RepairRequests");

            migrationBuilder.RenameColumn(
                name: "WorkCost",
                table: "RepairRequests",
                newName: "CalculatedCost");

            migrationBuilder.RenameColumn(
                name: "DamageDescription",
                table: "RepairRequests",
                newName: "RepairType");

            migrationBuilder.AddColumn<string>(
                name: "DamageLevel",
                table: "RepairRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DamagedPart",
                table: "RepairRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RepairPriceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    RepairType = table.Column<string>(type: "text", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairPriceItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepairPriceItems");

            migrationBuilder.DropColumn(
                name: "DamageLevel",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "DamagedPart",
                table: "RepairRequests");

            migrationBuilder.RenameColumn(
                name: "RepairType",
                table: "RepairRequests",
                newName: "DamageDescription");

            migrationBuilder.RenameColumn(
                name: "CalculatedCost",
                table: "RepairRequests",
                newName: "WorkCost");

            migrationBuilder.AddColumn<decimal>(
                name: "PaintCost",
                table: "RepairRequests",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartsCost",
                table: "RepairRequests",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
