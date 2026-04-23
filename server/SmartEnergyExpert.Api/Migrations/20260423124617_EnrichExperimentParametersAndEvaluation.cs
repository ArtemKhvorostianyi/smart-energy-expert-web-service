using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEnergyExpert.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnrichExperimentParametersAndEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ExperimentParameters",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ExperimentParameters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCritical",
                table: "ExperimentParameters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAcceptable",
                table: "ExperimentParameters",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAcceptable",
                table: "ExperimentParameters",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ExperimentParameters",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "ExperimentParameters",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "Evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Evaluations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TopFactors",
                table: "Evaluations",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExperimentParameters_Weight",
                table: "ExperimentParameters",
                sql: "\"Weight\" IS NULL OR \"Weight\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExperimentParameters_Weight",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "IsCritical",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "MaxAcceptable",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "MinAcceptable",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "ExperimentParameters");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "TopFactors",
                table: "Evaluations");
        }
    }
}
