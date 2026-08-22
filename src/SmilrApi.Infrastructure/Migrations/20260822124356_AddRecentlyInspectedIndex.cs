using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentlyInspectedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Establishments_City_LatestScoreDate_Name",
                table: "Establishments",
                columns: new[] { "City", "LatestScoreDate", "Name" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Establishments_City_LatestScoreDate_Name",
                table: "Establishments");
        }
    }
}
