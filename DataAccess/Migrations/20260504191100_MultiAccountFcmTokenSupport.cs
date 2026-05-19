using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class MultiAccountFcmTokenSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserFcmTokens_FcmToken",
                table: "UserFcmTokens");

            migrationBuilder.CreateIndex(
                name: "IX_UserFcmTokens_FcmToken",
                table: "UserFcmTokens",
                column: "FcmToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserFcmTokens_UserId_FcmTokenHash",
                table: "UserFcmTokens",
                columns: new[] { "UserId", "FcmTokenHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserFcmTokens_FcmToken",
                table: "UserFcmTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserFcmTokens_UserId_FcmTokenHash",
                table: "UserFcmTokens");

            migrationBuilder.CreateIndex(
                name: "IX_UserFcmTokens_FcmToken",
                table: "UserFcmTokens",
                column: "FcmToken",
                unique: true);
        }
    }
}
