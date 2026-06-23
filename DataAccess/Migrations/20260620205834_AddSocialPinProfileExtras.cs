using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialPinProfileExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalUrl",
                table: "SocialProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "SocialPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "SocialPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SocialNotifyComments",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialNotifyFollowers",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialNotifyMentions",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialNotifyPostEngagement",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SocialNotifyStoryEngagement",
                table: "Settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalUrl",
                table: "SocialProfiles");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "SocialPosts");

            migrationBuilder.DropColumn(
                name: "SocialNotifyComments",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SocialNotifyFollowers",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SocialNotifyMentions",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SocialNotifyPostEngagement",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SocialNotifyStoryEngagement",
                table: "Settings");
        }
    }
}
