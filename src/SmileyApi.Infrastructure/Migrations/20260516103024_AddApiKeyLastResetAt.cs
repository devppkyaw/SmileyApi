using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmileyApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyLastResetAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastResetAt",
                table: "ApiKeys",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_GeoLat",
                table: "Establishments",
                column: "GeoLat");

            migrationBuilder.CreateIndex(
                name: "IX_Establishments_GeoLng",
                table: "Establishments",
                column: "GeoLng");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Establishments_GeoLng", table: "Establishments");
            migrationBuilder.DropIndex(name: "IX_Establishments_GeoLat", table: "Establishments");
            migrationBuilder.DropIndex(name: "IX_ApiKeys_KeyHash", table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "LastResetAt",
                table: "ApiKeys");
        }
    }
}
