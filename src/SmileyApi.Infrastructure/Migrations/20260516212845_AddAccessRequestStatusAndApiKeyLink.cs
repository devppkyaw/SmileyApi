using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmileyApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessRequestStatusAndApiKeyLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApiKeyId",
                table: "AccessRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AccessRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AccessRequests_ApiKeyId",
                table: "AccessRequests",
                column: "ApiKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccessRequests_ApiKeys_ApiKeyId",
                table: "AccessRequests",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccessRequests_ApiKeys_ApiKeyId",
                table: "AccessRequests");

            migrationBuilder.DropIndex(
                name: "IX_AccessRequests_ApiKeyId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "ApiKeyId",
                table: "AccessRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AccessRequests");
        }
    }
}
