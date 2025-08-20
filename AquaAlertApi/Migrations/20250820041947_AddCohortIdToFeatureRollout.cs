using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaAlertApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCohortIdToFeatureRollout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CohortId",
                table: "FeatureRollouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRollouts_CohortId",
                table: "FeatureRollouts",
                column: "CohortId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureRollouts_Cohorts_CohortId",
                table: "FeatureRollouts",
                column: "CohortId",
                principalTable: "Cohorts",
                principalColumn: "CohortId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeatureRollouts_Cohorts_CohortId",
                table: "FeatureRollouts");

            migrationBuilder.DropIndex(
                name: "IX_FeatureRollouts_CohortId",
                table: "FeatureRollouts");

            migrationBuilder.DropColumn(
                name: "CohortId",
                table: "FeatureRollouts");
        }
    }
}
