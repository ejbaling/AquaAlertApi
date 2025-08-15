using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaAlertApi.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToWaterLevelLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "water_level_logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "water_level_logs");
        }
    }
}
