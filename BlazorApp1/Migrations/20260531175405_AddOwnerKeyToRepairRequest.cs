using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBlazorSite.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerKeyToRepairRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "RepairRequests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerKey",
                table: "RepairRequests");
        }
    }
}