using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruit_Finder_AI.Migrations
{
    /// <inheritdoc />
    public partial class AiFeedbackCvImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiFeedback",
                table: "Cvs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiFeedback",
                table: "Cvs");
        }
    }
}
