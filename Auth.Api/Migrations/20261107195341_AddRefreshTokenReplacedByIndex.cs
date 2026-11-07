using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenReplacedByIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenHash",
                table: "RefreshTokens",
                column: "ReplacedByTokenHash",
                unique: true,
                filter: "\"ReplacedByTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ReplacedByTokenHash",
                table: "RefreshTokens");
        }
    }
}
