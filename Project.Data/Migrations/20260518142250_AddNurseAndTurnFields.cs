using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNurseAndTurnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dateOfTurn",
                table: "turns");

            migrationBuilder.DropColumn(
                name: "workHours",
                table: "nurses");

            migrationBuilder.AddColumn<string>(
                name: "date",
                table: "turns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "turns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "time",
                table: "turns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "nurses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "nurses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "nurses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "specialization",
                table: "nurses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date",
                table: "turns");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "turns");

            migrationBuilder.DropColumn(
                name: "time",
                table: "turns");

            migrationBuilder.DropColumn(
                name: "email",
                table: "nurses");

            migrationBuilder.DropColumn(
                name: "name",
                table: "nurses");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "nurses");

            migrationBuilder.DropColumn(
                name: "specialization",
                table: "nurses");

            migrationBuilder.AddColumn<DateTime>(
                name: "dateOfTurn",
                table: "turns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "workHours",
                table: "nurses",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
