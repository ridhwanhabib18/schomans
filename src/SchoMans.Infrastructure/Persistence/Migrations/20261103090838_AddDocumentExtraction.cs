using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoMans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExtractedAt",
                table: "ApplicantDocuments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedData",
                table: "ApplicantDocuments",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedAt",
                table: "ApplicantDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractedData",
                table: "ApplicantDocuments");
        }
    }
}
