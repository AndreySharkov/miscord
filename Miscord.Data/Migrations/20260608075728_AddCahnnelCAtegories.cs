using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miscord.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCahnnelCAtegories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChannelCategoryId",
                table: "Channels",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "Channels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChannelCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelCategories_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_ChannelCategoryId",
                table: "Channels",
                column: "ChannelCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelCategories_ServerId",
                table: "ChannelCategories",
                column: "ServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_ChannelCategories_ChannelCategoryId",
                table: "Channels",
                column: "ChannelCategoryId",
                principalTable: "ChannelCategories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_ChannelCategories_ChannelCategoryId",
                table: "Channels");

            migrationBuilder.DropTable(
                name: "ChannelCategories");

            migrationBuilder.DropIndex(
                name: "IX_Channels_ChannelCategoryId",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "ChannelCategoryId",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Channels");
        }
    }
}
