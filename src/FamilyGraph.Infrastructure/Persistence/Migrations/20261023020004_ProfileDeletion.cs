using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "people");
        }
    }
}
