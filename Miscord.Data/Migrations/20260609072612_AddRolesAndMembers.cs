using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miscord.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServerId = table.Column<int>(type: "int", nullable: false),
                    Nickname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ServerMembers_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Permissions = table.Column<long>(type: "bigint", nullable: false),
                    ServerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerRoles_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerMemberRoles",
                columns: table => new
                {
                    ServerMemberId = table.Column<int>(type: "int", nullable: false),
                    ServerRoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMemberRoles", x => new { x.ServerMemberId, x.ServerRoleId });
                    table.ForeignKey(
                        name: "FK_ServerMemberRoles_ServerMembers_ServerMemberId",
                        column: x => x.ServerMemberId,
                        principalTable: "ServerMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServerMemberRoles_ServerRoles_ServerRoleId",
                        column: x => x.ServerRoleId,
                        principalTable: "ServerRoles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerMemberRoles_ServerRoleId",
                table: "ServerMemberRoles",
                column: "ServerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMembers_ServerId",
                table: "ServerMembers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerMembers_UserId",
                table: "ServerMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServerRoles_ServerId",
                table: "ServerRoles",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerMemberRoles");

            migrationBuilder.DropTable(
                name: "ServerMembers");

            migrationBuilder.DropTable(
                name: "ServerRoles");
        }
    }
}
