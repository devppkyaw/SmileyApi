using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessWebhookSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebhookSubscriptions_ApiKeys_ApiKeyId",
                table: "WebhookSubscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "ApiKeyId",
                table: "WebhookSubscriptions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BusinessId",
                table: "WebhookSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_BusinessId",
                table: "WebhookSubscriptions",
                column: "BusinessId");

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookSubscriptions_ApiKeys_ApiKeyId",
                table: "WebhookSubscriptions",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookSubscriptions_Businesses_BusinessId",
                table: "WebhookSubscriptions",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WebhookSubscriptions_ApiKeys_ApiKeyId",
                table: "WebhookSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_WebhookSubscriptions_Businesses_BusinessId",
                table: "WebhookSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscriptions_BusinessId",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "WebhookSubscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "ApiKeyId",
                table: "WebhookSubscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookSubscriptions_ApiKeys_ApiKeyId",
                table: "WebhookSubscriptions",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
