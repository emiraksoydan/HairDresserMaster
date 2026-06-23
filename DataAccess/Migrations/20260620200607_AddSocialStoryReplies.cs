using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialStoryReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialStoryReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2200)", maxLength: 2200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialStoryReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialStoryReplies_SocialProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "SocialProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SocialStoryReplies_SocialStories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "SocialStories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialStoryReplies_ProfileId",
                table: "SocialStoryReplies",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialStoryReplies_StoryId_ProfileId_CreatedAt",
                table: "SocialStoryReplies",
                columns: new[] { "StoryId", "ProfileId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialStoryReplies");
        }
    }
}
