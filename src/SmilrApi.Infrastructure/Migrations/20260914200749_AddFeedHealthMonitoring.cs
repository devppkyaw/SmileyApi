using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedHealthMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedHealthAnomalies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FirstDetectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAlertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastObservedSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedHealthAnomalies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeedHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastEtag = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedHeader = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastHeaderChangeDetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastTotalRowsSeen = table.Column<int>(type: "int", nullable: false),
                    LastParsedRowCount = table.Column<int>(type: "int", nullable: false),
                    FieldNullCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedHealthAnomalies_Kind_FieldName",
                table: "FeedHealthAnomalies",
                columns: new[] { "Kind", "FieldName" },
                unique: true,
                filter: "[FieldName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedHealthAnomalies");

            migrationBuilder.DropTable(
                name: "FeedHealthSnapshots");
        }
    }
}
