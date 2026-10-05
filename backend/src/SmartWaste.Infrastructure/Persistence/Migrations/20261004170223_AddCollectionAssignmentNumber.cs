using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionAssignmentNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CollectionAssignmentNumberSequence");

            migrationBuilder.AddColumn<long>(
                name: "AssignmentNumber",
                table: "CollectionAssignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id", row_number() OVER (ORDER BY "CreatedAt", "Id") AS "Number"
                    FROM "CollectionAssignments"
                )
                UPDATE "CollectionAssignments" AS assignment
                SET "AssignmentNumber" = numbered."Number"
                FROM numbered
                WHERE assignment."Id" = numbered."Id";
                """);

            migrationBuilder.AlterColumn<long>(
                name: "AssignmentNumber",
                table: "CollectionAssignments",
                type: "bigint",
                nullable: false,
                defaultValueSql: "nextval('\"CollectionAssignmentNumberSequence\"'::regclass)",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.Sql("""
                SELECT setval(
                    '"CollectionAssignmentNumberSequence"'::regclass,
                    COALESCE((SELECT MAX("AssignmentNumber") FROM "CollectionAssignments"), 1),
                    EXISTS (SELECT 1 FROM "CollectionAssignments")
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionAssignments_AssignmentNumber",
                table: "CollectionAssignments",
                column: "AssignmentNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CollectionAssignments_AssignmentNumber",
                table: "CollectionAssignments",
                sql: "\"AssignmentNumber\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionAssignments_AssignmentNumber",
                table: "CollectionAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CollectionAssignments_AssignmentNumber",
                table: "CollectionAssignments");

            migrationBuilder.DropColumn(
                name: "AssignmentNumber",
                table: "CollectionAssignments");

            migrationBuilder.DropSequence(
                name: "CollectionAssignmentNumberSequence");
        }
    }
}
