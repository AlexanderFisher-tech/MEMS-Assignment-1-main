using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mems.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SetCouncillorSeatsToFive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A voter may choose at most 5 councillors. Existing databases were seeded with 6 seats;
            // fresh databases are seeded with 5, so this is a no-op for them.
            migrationBuilder.Sql("UPDATE Positions SET SeatsAvailable = 5 WHERE Name = 'Councillor' AND SeatsAvailable = 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Positions SET SeatsAvailable = 6 WHERE Name = 'Councillor' AND SeatsAvailable = 5;");
        }
    }
}
