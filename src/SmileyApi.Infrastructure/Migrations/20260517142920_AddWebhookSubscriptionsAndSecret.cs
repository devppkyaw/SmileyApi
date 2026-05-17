using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmileyApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSubscriptionsAndSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecretKey",
                table: "WebhookSubscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretKey",
                table: "WebhookSubscriptions");
        }
    }
}
