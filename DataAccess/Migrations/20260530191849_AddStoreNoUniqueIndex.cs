using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreNoUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BarberStores_StoreNo",
                table: "BarberStores",
                column: "StoreNo",
                unique: true,
                filter: "\"StoreNo\" IS NOT NULL AND \"StoreNo\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BarberStores_StoreNo",
                table: "BarberStores");
        }
    }
}
