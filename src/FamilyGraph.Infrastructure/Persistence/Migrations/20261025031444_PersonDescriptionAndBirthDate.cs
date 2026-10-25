using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyGraph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonDescriptionAndBirthDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "birth_calendar",
                table: "people",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "birth_date_shown",
                table: "people",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "birth_day",
                table: "people",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "birth_month",
                table: "people",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_sort_date",
                table: "people",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "birth_year",
                table: "people",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "people",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_people_description_length",
                table: "people",
                sql: "char_length(description) <= 200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_people_description_length",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_calendar",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_date_shown",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_day",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_month",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_sort_date",
                table: "people");

            migrationBuilder.DropColumn(
                name: "birth_year",
                table: "people");

            migrationBuilder.DropColumn(
                name: "description",
                table: "people");
        }
    }
}
