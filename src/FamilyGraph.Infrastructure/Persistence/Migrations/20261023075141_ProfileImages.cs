using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "image_id",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_images", x => x.id);
                    table.CheckConstraint("ck_profile_images_format", "format IN (1, 2, 3)");
                    table.CheckConstraint("ck_profile_images_size", "octet_length(content) <= 1048576");
                    table.ForeignKey(
                        name: "fk_profile_images_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_profile_images_person_id",
                table: "profile_images",
                column: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_images");

            migrationBuilder.DropColumn(
                name: "image_id",
                table: "people");
        }
    }
}
