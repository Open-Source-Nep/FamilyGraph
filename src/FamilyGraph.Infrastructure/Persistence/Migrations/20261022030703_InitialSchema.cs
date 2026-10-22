using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    family = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    maintainer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    middle_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_people", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parent_child_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    parent_approval = table.Column<int>(type: "integer", nullable: false),
                    child_approval = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    birth_order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parent_child_links", x => x.id);
                    table.CheckConstraint("ck_parent_child_links_birth_order", "birth_order IS NULL OR birth_order >= 1");
                    table.CheckConstraint("ck_parent_child_links_child_approval", "child_approval IN (1, 2, 3)");
                    table.CheckConstraint("ck_parent_child_links_not_self", "parent_id <> child_id");
                    table.CheckConstraint("ck_parent_child_links_parent_approval", "parent_approval IN (1, 2, 3)");
                    table.CheckConstraint("ck_parent_child_links_type", "type IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_parent_child_links_people_child_id",
                        column: x => x.child_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parent_child_links_people_parent_id",
                        column: x => x.parent_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor",
                table: "audit_events",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_subject",
                table: "audit_events",
                columns: new[] { "subject_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_parent_child_links_child_active",
                table: "parent_child_links",
                column: "child_id",
                filter: "deleted_at IS NULL AND parent_approval = 2 AND child_approval = 2");

            migrationBuilder.CreateIndex(
                name: "ix_parent_child_links_child_not_deleted",
                table: "parent_child_links",
                column: "child_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_parent_child_links_parent_active",
                table: "parent_child_links",
                column: "parent_id",
                filter: "deleted_at IS NULL AND parent_approval = 2 AND child_approval = 2");

            migrationBuilder.CreateIndex(
                name: "ux_parent_child_links_parent_child",
                table: "parent_child_links",
                columns: new[] { "parent_id", "child_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_people_maintainer_id",
                table: "people",
                column: "maintainer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "parent_child_links");

            migrationBuilder.DropTable(
                name: "people");
        }
    }
}
