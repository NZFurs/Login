using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace NZFurs.Auth.Migrations
{
    public partial class AspNetCoreIdentityUpdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FursonaShortName",
                table: "AspNetUsers",
                newName: "FursonaSpecies");

            migrationBuilder.RenameColumn(
                name: "FursonaFullName",
                table: "AspNetUsers",
                newName: "FursonaName");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                nullable: true,
                oldClrType: typeof(DateTime));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FursonaSpecies",
                table: "AspNetUsers",
                newName: "FursonaShortName");

            migrationBuilder.RenameColumn(
                name: "FursonaName",
                table: "AspNetUsers",
                newName: "FursonaFullName");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateOfBirth",
                table: "AspNetUsers",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldNullable: true);
        }
    }
}
