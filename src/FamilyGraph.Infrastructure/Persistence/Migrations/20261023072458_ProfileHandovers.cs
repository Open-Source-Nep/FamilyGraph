using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_handovers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    accepted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_handovers", x => x.id);
                    table.CheckConstraint("ck_profile_handovers_state", "state IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_profile_handovers_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_profile_handovers_person_open",
                table: "profile_handovers",
                column: "person_id",
                unique: true,
                filter: "state = 1");

            migrationBuilder.CreateIndex(
                name: "ux_profile_handovers_token_hash",
                table: "profile_handovers",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_handovers");
        }
    }
}
