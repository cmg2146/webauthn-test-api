using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAuthnTest.Database.Migrations
{
    public partial class AddUserHandle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "UserHandle",
                table: "User",
                type: "varbinary(64)",
                maxLength: 64,
                nullable: true);

            // currently, the user id is used as the handle, so populate the new handle column accordingly.
            // on a big database it would be more practical to make this column nullable.
            migrationBuilder.Sql("UPDATE [User] SET [UserHandle] = CONVERT(varbinary, Id)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "UserHandle",
                table: "User",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_User_UserHandle",
                table: "User",
                column: "UserHandle",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_UserHandle",
                table: "User");

            migrationBuilder.DropColumn(
                name: "UserHandle",
                table: "User");
        }
    }
}
