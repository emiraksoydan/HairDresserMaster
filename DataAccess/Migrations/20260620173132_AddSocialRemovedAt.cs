using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialRemovedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "SocialStoryHighlights",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "SocialStoryHighlightItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "SocialStories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "SocialPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "SocialPosts" SET "RemovedAt" = "UpdatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
                UPDATE "SocialStories" SET "RemovedAt" = "CreatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
                UPDATE "SocialStoryHighlights" SET "RemovedAt" = "UpdatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
                UPDATE "SocialStoryHighlightItems" SET "RemovedAt" = "CreatedAt" WHERE "Status" = 2 AND "RemovedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "SocialStoryHighlights");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "SocialStoryHighlightItems");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "SocialStories");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "SocialPosts");
        }
    }
}
