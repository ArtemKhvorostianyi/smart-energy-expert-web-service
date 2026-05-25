using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEnergyExpert.Client.Migrations
{
    /// <inheritdoc />
    public partial class DatasetOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Datasets_Name",
                table: "Datasets");

            migrationBuilder.AddColumn<bool>(
                name: "IsGuestCatalog",
                table: "Datasets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Datasets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_IsGuestCatalog",
                table: "Datasets",
                column: "IsGuestCatalog");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_OwnerUserId_Name",
                table: "Datasets",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Datasets_Users_OwnerUserId",
                table: "Datasets",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Datasets_Users_OwnerUserId",
                table: "Datasets");

            migrationBuilder.DropIndex(
                name: "IX_Datasets_IsGuestCatalog",
                table: "Datasets");

            migrationBuilder.DropIndex(
                name: "IX_Datasets_OwnerUserId_Name",
                table: "Datasets");

            migrationBuilder.DropColumn(
                name: "IsGuestCatalog",
                table: "Datasets");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Datasets");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_Name",
                table: "Datasets",
                column: "Name",
                unique: true);
        }
    }
}
