using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyDriverProfilesAutomaticProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LicenseNumber",
                table: "DriverProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.Sql("""
                INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "CreatedAt")
                SELECT users."Id", NULL, 'Available', CURRENT_TIMESTAMP
                FROM "AspNetUsers" AS users
                INNER JOIN "AspNetUserRoles" AS user_roles ON user_roles."UserId" = users."Id"
                INNER JOIN "AspNetRoles" AS roles ON roles."Id" = user_roles."RoleId"
                WHERE roles."Name" = 'Driver'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "DriverProfiles" AS profiles
                      WHERE profiles."UserId" = users."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DriverProfiles" WHERE "LicenseNumber" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot restore required DriverProfile licences while automatic profiles exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LicenseNumber",
                table: "DriverProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
