using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkApiKeyToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "ApiKeys",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_BusinessId",
                table: "ApiKeys",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Businesses_BusinessId",
                table: "ApiKeys",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Businesses_BusinessId",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_BusinessId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "ApiKeys");
        }
    }
}
