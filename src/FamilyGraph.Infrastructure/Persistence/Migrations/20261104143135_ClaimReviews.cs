using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClaimReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "administrator_question",
                table: "profile_claims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "appealed",
                table: "profile_claims",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "decided_by_administrator",
                table: "profile_claims",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "maintainer_response",
                table: "profile_claims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "notes_erased_at",
                table: "profile_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rationale",
                table: "profile_claims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "review_requested_at",
                table: "profile_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_profile_claims_notes_to_erase",
                table: "profile_claims",
                column: "closed_at",
                filter: "notes_erased_at IS NULL AND closed_at IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_profile_claims_texts_length",
                table: "profile_claims",
                sql: "char_length(rationale) <= 2000 AND char_length(administrator_question) <= 2000 AND char_length(maintainer_response) <= 2000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_profile_claims_notes_to_erase",
                table: "profile_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_profile_claims_texts_length",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "administrator_question",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "appealed",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "decided_by_administrator",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "maintainer_response",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "notes_erased_at",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "rationale",
                table: "profile_claims");

            migrationBuilder.DropColumn(
                name: "review_requested_at",
                table: "profile_claims");
        }
    }
}
