using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mems.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateDemoSeedKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DemoSeedKey",
                table: "Candidates",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_DemoSeedKey",
                table: "Candidates",
                column: "DemoSeedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candidates_DemoSeedKey",
                table: "Candidates");

            migrationBuilder.DropColumn(
                name: "DemoSeedKey",
                table: "Candidates");
        }
    }
}
