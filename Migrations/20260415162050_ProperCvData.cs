using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruit_Finder_AI.Migrations
{
    /// <inheritdoc />
    public partial class ProperCvData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Cvs",
                newName: "ProfessionalExperience");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Cvs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Cvs",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateOfBirth",
                table: "Cvs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "Cvs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Cvs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Interests",
                table: "Cvs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Languages",
                table: "Cvs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Cvs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Cvs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Portfolio",
                table: "Cvs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Skills",
                table: "Cvs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Surname",
                table: "Cvs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Education",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Interests",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Languages",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Portfolio",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "Cvs");

            migrationBuilder.DropColumn(
                name: "Surname",
                table: "Cvs");

            migrationBuilder.RenameColumn(
                name: "ProfessionalExperience",
                table: "Cvs",
                newName: "Content");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Cvs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
