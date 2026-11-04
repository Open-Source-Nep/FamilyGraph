using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfileClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "claimed_by_id",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_claims", x => x.id);
                    table.CheckConstraint("ck_profile_claims_evidence_length", "char_length(evidence) <= 2000");
                    table.CheckConstraint("ck_profile_claims_state", "state IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "fk_profile_claims_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_people_claimed_by_id",
                table: "people",
                column: "claimed_by_id",
                unique: true,
                filter: "claimed_by_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_profile_claims_person_id",
                table: "profile_claims",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ux_profile_claims_claimant_open",
                table: "profile_claims",
                column: "claimant_id",
                unique: true,
                filter: "state = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_claims");

            migrationBuilder.DropIndex(
                name: "ux_people_claimed_by_id",
                table: "people");

            migrationBuilder.DropColumn(
                name: "claimed_by_id",
                table: "people");
        }
    }
}
