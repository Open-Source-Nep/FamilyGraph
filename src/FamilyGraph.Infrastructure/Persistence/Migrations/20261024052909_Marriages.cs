using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Marriages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marriages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_spouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    second_spouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_approval = table.Column<int>(type: "integer", nullable: false),
                    second_approval = table.Column<int>(type: "integer", nullable: false),
                    is_former = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marriages", x => x.id);
                    table.CheckConstraint("ck_marriages_first_approval", "first_approval IN (1, 2, 3)");
                    table.CheckConstraint("ck_marriages_second_approval", "second_approval IN (1, 2, 3)");
                    table.CheckConstraint("ck_marriages_spouse_order", "first_spouse_id < second_spouse_id");
                    table.ForeignKey(
                        name: "fk_marriages_people_first_spouse_id",
                        column: x => x.first_spouse_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_marriages_people_second_spouse_id",
                        column: x => x.second_spouse_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_marriages_first_spouse_active",
                table: "marriages",
                column: "first_spouse_id",
                filter: "deleted_at IS NULL AND first_approval = 2 AND second_approval = 2");

            migrationBuilder.CreateIndex(
                name: "ix_marriages_second_spouse_active",
                table: "marriages",
                column: "second_spouse_id",
                filter: "deleted_at IS NULL AND first_approval = 2 AND second_approval = 2");

            migrationBuilder.CreateIndex(
                name: "ux_marriages_spouses",
                table: "marriages",
                columns: new[] { "first_spouse_id", "second_spouse_id" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marriages");
        }
    }
}
