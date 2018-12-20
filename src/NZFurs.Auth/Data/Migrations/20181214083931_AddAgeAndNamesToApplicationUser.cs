using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NZFurs.Auth.Data.Migrations
{
    public partial class AddAgeAndNamesToApplicationUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DateOfBirthPublicFlags",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FursonaName",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FursonaSpecies",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IrlFullName",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IrlShortName",
                table: "AspNetUsers",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DateOfBirthPublicFlags",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FursonaName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FursonaSpecies",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IrlFullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IrlShortName",
                table: "AspNetUsers");
        }
    }
}
