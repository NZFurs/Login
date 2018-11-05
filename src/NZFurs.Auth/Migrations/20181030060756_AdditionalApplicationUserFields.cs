using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NZFurs.Auth.Migrations
{
    public partial class AdditionalApplicationUserFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DateOfBirthPublicFlags",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FursonaFullName",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FursonaShortName",
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
                name: "FursonaFullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FursonaShortName",
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
