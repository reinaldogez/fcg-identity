using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fcg.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjusteOutboxMassTransit8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "BusName",
                table: "OutboxState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusName",
                table: "OutboxState",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState",
                columns: new[] { "BusName", "Created" });
        }
    }
}
