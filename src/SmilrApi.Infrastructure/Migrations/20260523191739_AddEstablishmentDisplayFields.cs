using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstablishmentDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LatestScoreDate",
                table: "Establishments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PNumber",
                table: "Establishments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pixibranche",
                table: "Establishments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VirksomhedsType",
                table: "Establishments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestScoreDate",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "PNumber",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "Pixibranche",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "VirksomhedsType",
                table: "Establishments");
        }
    }
}
