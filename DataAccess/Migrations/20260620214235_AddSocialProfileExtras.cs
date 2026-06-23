using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialProfileExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageId",
                table: "SocialProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DmPolicy",
                table: "SocialProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SocialProfileMutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MutedByProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MutedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialProfileMutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialProfileMutes_SocialProfiles_MutedByProfileId",
                        column: x => x.MutedByProfileId,
                        principalTable: "SocialProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SocialProfileMutes_SocialProfiles_MutedProfileId",
                        column: x => x.MutedProfileId,
                        principalTable: "SocialProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialProfileMutes_MutedByProfileId_MutedProfileId",
                table: "SocialProfileMutes",
                columns: new[] { "MutedByProfileId", "MutedProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialProfileMutes_MutedProfileId",
                table: "SocialProfileMutes",
                column: "MutedProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialProfileMutes");

            migrationBuilder.DropColumn(
                name: "CoverImageId",
                table: "SocialProfiles");

            migrationBuilder.DropColumn(
                name: "DmPolicy",
                table: "SocialProfiles");
        }
    }
}
