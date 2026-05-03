using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentRequestLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "RequestLatitude",
                table: "Appointments",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RequestLongitude",
                table: "Appointments",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestLatitude",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RequestLongitude",
                table: "Appointments");
        }
    }
}
