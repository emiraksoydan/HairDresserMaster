using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialStoryViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialStoryViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialStoryViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialStoryViews_SocialProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "SocialProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialStoryViews_SocialStories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "SocialStories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialStoryViews_ProfileId",
                table: "SocialStoryViews",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialStoryViews_StoryId_ProfileId",
                table: "SocialStoryViews",
                columns: new[] { "StoryId", "ProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialStoryViews");
        }
    }
}
