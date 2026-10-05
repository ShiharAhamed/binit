using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWaste.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteBinWeekdaysCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_WasteBins_CollectionWeekdays_Range",
                table: "WasteBins",
                sql: "\"CollectionWeekdays\" <@ ARRAY[1, 2, 3, 4, 5, 6, 7]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WasteBins_CollectionWeekdays_Range",
                table: "WasteBins");
        }
    }
}
