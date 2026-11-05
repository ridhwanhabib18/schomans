using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoMans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneralLetters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneralLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LetterNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PdfKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneralLetters_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralLetters_IssuedAt",
                table: "GeneralLetters",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralLetters_IssuedByUserId",
                table: "GeneralLetters",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralLetters_LetterNumber",
                table: "GeneralLetters",
                column: "LetterNumber",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneralLetters");
        }
    }
}
