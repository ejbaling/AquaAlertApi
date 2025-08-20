using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaAlertApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureRolloutClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureRollouts",
                columns: table => new
                {
                    RolloutId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    RolloutType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Parameter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureRollouts", x => x.RolloutId);
                    table.ForeignKey(
                        name: "FK_FeatureRollouts_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "Features",
                        principalColumn: "FeatureId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRollouts_FeatureId",
                table: "FeatureRollouts",
                column: "FeatureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureRollouts");
        }
    }
}
