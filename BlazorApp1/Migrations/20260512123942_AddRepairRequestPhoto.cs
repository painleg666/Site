using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBlazorSite.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairRequestPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "RepairRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "RepairRequests");
        }
    }
}
