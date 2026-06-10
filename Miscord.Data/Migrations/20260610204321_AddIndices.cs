using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miscord.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServerMembers_ServerId",
                table: "ServerMembers");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMembers_ServerId_UserId",
                table: "ServerMembers",
                columns: new[] { "ServerId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invites_Token",
                table: "Invites",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServerMembers_ServerId_UserId",
                table: "ServerMembers");

            migrationBuilder.DropIndex(
                name: "IX_Invites_Token",
                table: "Invites");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMembers_ServerId",
                table: "ServerMembers",
                column: "ServerId");
        }
    }
}
