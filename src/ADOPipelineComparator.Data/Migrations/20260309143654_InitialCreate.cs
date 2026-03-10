using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADOPipelineComparator.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdoSites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OrganizationUrl = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Pat = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdoSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdoSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrganizationName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Project = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PipelineId = table.Column<int>(type: "INTEGER", nullable: false),
                    PipelineName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PipelineType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PipelineSubtype = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastRunDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRunBy = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    TaskName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    PipelineUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CachedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineCache_AdoSites_AdoSiteId",
                        column: x => x.AdoSiteId,
                        principalTable: "AdoSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineCache_AdoSiteId_Project_PipelineId_PipelineType",
                table: "PipelineCache",
                columns: new[] { "AdoSiteId", "Project", "PipelineId", "PipelineType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineCache");

            migrationBuilder.DropTable(
                name: "AdoSites");
        }
    }
}
