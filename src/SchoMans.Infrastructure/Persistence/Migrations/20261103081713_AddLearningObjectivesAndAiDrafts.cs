using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoMans.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningObjectivesAndAiDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LearningObjectiveId",
                table: "Grades",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBulk",
                table: "AiUsageLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LearningObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningObjectives_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Grades_LearningObjectiveId",
                table: "Grades",
                column: "LearningObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningObjectives_SubjectId_Code",
                table: "LearningObjectives",
                columns: new[] { "SubjectId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Grades_LearningObjectives_LearningObjectiveId",
                table: "Grades",
                column: "LearningObjectiveId",
                principalTable: "LearningObjectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Grades_LearningObjectives_LearningObjectiveId",
                table: "Grades");

            migrationBuilder.DropTable(
                name: "LearningObjectives");

            migrationBuilder.DropIndex(
                name: "IX_Grades_LearningObjectiveId",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "LearningObjectiveId",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "IsBulk",
                table: "AiUsageLogs");
        }
    }
}
