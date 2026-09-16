using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmilrApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingClaimCvrToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingClaimCvr",
                table: "Businesses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingClaimCvr",
                table: "Businesses");
        }
    }
}
