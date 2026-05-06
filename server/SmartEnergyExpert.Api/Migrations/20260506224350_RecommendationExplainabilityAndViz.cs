using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEnergyExpert.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationExplainabilityAndViz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Recommendations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConfidenceRationale",
                table: "Recommendations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceSignalsJson",
                table: "Recommendations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InferenceMethod",
                table: "Recommendations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VisualizationPayloadJson",
                table: "ComparisonRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "ConfidenceRationale",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "EvidenceSignalsJson",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "InferenceMethod",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "VisualizationPayloadJson",
                table: "ComparisonRuns");
        }
    }
}
