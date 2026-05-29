using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruit_Finder_AI.Migrations
{
    /// <inheritdoc />
    public partial class AiAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisComment",
                table: "JobOffers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisStatus",
                table: "JobOffers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requirements",
                table: "JobOffers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisComment",
                table: "JobOffers");

            migrationBuilder.DropColumn(
                name: "AiAnalysisStatus",
                table: "JobOffers");

            migrationBuilder.DropColumn(
                name: "Requirements",
                table: "JobOffers");
        }
    }
}
