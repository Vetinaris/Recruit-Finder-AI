using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruit_Finder_AI.Migrations
{
    /// <inheritdoc />
    public partial class FixApplicationRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_JobOffers_JobOfferId",
                table: "Applications");

            migrationBuilder.AddColumn<int>(
                name: "JobApplicationId",
                table: "Applications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobApplicationId",
                table: "Applications",
                column: "JobApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Applications_JobApplicationId",
                table: "Applications",
                column: "JobApplicationId",
                principalTable: "Applications",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_JobOffers_JobOfferId",
                table: "Applications",
                column: "JobOfferId",
                principalTable: "JobOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Applications_JobApplicationId",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Applications_JobOffers_JobOfferId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobApplicationId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "JobApplicationId",
                table: "Applications");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_JobOffers_JobOfferId",
                table: "Applications",
                column: "JobOfferId",
                principalTable: "JobOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
