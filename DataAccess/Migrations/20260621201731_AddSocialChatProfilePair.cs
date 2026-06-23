using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialChatProfilePair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SocialProfileHighId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SocialProfileLowId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_SocialProfileLowId_SocialProfileHighId",
                table: "ChatThreads",
                columns: new[] { "SocialProfileLowId", "SocialProfileHighId" },
                unique: true,
                filter: "\"IsSocialThread\" = true AND \"SocialProfileLowId\" IS NOT NULL AND \"SocialProfileHighId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_SocialProfileLowId_SocialProfileHighId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "SocialProfileHighId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "SocialProfileLowId",
                table: "ChatThreads");
        }
    }
}
