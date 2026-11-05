using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoMans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizzes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnswersJson",
                table: "Submissions",
                type: "nvarchar(max)",
                maxLength: 40000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortPointsJson",
                table: "Submissions",
                type: "nvarchar(max)",
                maxLength: 40000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssignmentQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentQuestions_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentQuestions_AssignmentId_Order",
                table: "AssignmentQuestions",
                columns: new[] { "AssignmentId", "Order" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentQuestions_AssignmentId_QuestionId",
                table: "AssignmentQuestions",
                columns: new[] { "AssignmentId", "QuestionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentQuestions_QuestionId",
                table: "AssignmentQuestions",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentQuestions");

            migrationBuilder.DropColumn(
                name: "AnswersJson",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ShortPointsJson",
                table: "Submissions");
        }
    }
}
