using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gender",
                table: "people",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gender",
                table: "people");
        }
    }
}
