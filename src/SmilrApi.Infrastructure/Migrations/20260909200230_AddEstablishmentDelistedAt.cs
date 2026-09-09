using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstablishmentDelistedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DelistedAt",
                table: "Establishments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelistedAt",
                table: "Establishments");
        }
    }
}
