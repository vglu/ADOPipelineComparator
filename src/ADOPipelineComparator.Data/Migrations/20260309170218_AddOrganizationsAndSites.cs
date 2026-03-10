using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADOPipelineComparator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsAndSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear stale data: old AdoSites rows are org-level and incompatible
            // with the new project-level schema (OrganizationId FK would fail).
            migrationBuilder.Sql("DELETE FROM \"PipelineCache\";");
            migrationBuilder.Sql("DELETE FROM \"AdoSites\";");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AdoSites");

            migrationBuilder.DropColumn(
                name: "OrganizationUrl",
                table: "AdoSites");

            migrationBuilder.DropColumn(
                name: "Pat",
                table: "AdoSites");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AdoSites",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "AdoSites",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "AdoSites",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OrganizationUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Pat = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdoSites_OrganizationId",
                table: "AdoSites",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdoSites_Organizations_OrganizationId",
                table: "AdoSites",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdoSites_Organizations_OrganizationId",
                table: "AdoSites");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_AdoSites_OrganizationId",
                table: "AdoSites");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AdoSites");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AdoSites");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "AdoSites");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AdoSites",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationUrl",
                table: "AdoSites",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pat",
                table: "AdoSites",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
