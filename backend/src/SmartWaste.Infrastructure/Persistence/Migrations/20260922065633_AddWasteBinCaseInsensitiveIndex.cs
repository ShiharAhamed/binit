using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteBinCaseInsensitiveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_WasteBins_BinCode_CaseInsensitive\" ON \"WasteBins\" (lower(\"BinCode\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_WasteBins_BinCode_CaseInsensitive\";");
        }
    }
}
