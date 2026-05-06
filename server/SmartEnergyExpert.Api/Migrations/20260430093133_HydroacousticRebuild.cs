using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEnergyExpert.Api.Migrations
{
    /// <inheritdoc />
    public partial class HydroacousticRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    SourceSystem = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    TimeRangeStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeRangeEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcousticSamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FrequencyBand = table.Column<decimal>(type: "numeric", nullable: false),
                    AmplitudeDb = table.Column<decimal>(type: "numeric", nullable: false),
                    DepthMeters = table.Column<decimal>(type: "numeric", nullable: false),
                    RangeMeters = table.Column<decimal>(type: "numeric", nullable: false),
                    SoundSpeed = table.Column<decimal>(type: "numeric", nullable: true),
                    NoiseLevelDb = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcousticSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcousticSamples_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComparisonRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationDatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Mae = table.Column<decimal>(type: "numeric", nullable: false),
                    Rmse = table.Column<decimal>(type: "numeric", nullable: false),
                    MeanRelativeErrorPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    P95AbsoluteError = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalComparedPoints = table.Column<int>(type: "integer", nullable: false),
                    SignificantDifferenceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparisonRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparisonRuns_Datasets_FieldDatasetId",
                        column: x => x.FieldDatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComparisonRuns_Datasets_SimulationDatasetId",
                        column: x => x.SimulationDatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DifferencePoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComparisonRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FrequencyBand = table.Column<decimal>(type: "numeric", nullable: false),
                    SimulationValue = table.Column<decimal>(type: "numeric", nullable: false),
                    FieldValue = table.Column<decimal>(type: "numeric", nullable: false),
                    AbsoluteError = table.Column<decimal>(type: "numeric", nullable: false),
                    RelativeErrorPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DifferencePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DifferencePoints_ComparisonRuns_ComparisonRunId",
                        column: x => x.ComparisonRunId,
                        principalTable: "ComparisonRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComparisonRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    SuggestedAction = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recommendations_ComparisonRuns_ComparisonRunId",
                        column: x => x.ComparisonRunId,
                        principalTable: "ComparisonRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcousticSamples_DatasetId_Timestamp_FrequencyBand",
                table: "AcousticSamples",
                columns: new[] { "DatasetId", "Timestamp", "FrequencyBand" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparisonRuns_CreatedAt",
                table: "ComparisonRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ComparisonRuns_FieldDatasetId",
                table: "ComparisonRuns",
                column: "FieldDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_ComparisonRuns_SimulationDatasetId",
                table: "ComparisonRuns",
                column: "SimulationDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_Name",
                table: "Datasets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_Type_SourceSystem",
                table: "Datasets",
                columns: new[] { "Type", "SourceSystem" });

            migrationBuilder.CreateIndex(
                name: "IX_DifferencePoints_ComparisonRunId_Severity_Timestamp_Frequen~",
                table: "DifferencePoints",
                columns: new[] { "ComparisonRunId", "Severity", "Timestamp", "FrequencyBand" });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_ComparisonRunId_Confidence",
                table: "Recommendations",
                columns: new[] { "ComparisonRunId", "Confidence" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcousticSamples");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DifferencePoints");

            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ComparisonRuns");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Datasets");
        }
    }
}
