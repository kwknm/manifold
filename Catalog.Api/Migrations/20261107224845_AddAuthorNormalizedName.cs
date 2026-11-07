using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <summary>
    /// Author identity moves from the display name to a case-folded key. Existing rows are
    /// backfilled and case-only duplicates are merged before the unique index is created,
    /// otherwise the index would not build on data that already contains them.
    /// </summary>
    public partial class AddAuthorNormalizedName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Authors_UserId_FullName",
                table: "Authors");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Authors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Rows written before this migration only have the display name. lower() is the
            // closest SQL equivalent of the application's ToLowerInvariant() and is only used
            // here; every row written afterwards carries the key the application computed.
            migrationBuilder.Sql("""
                UPDATE "Authors" SET "NormalizedName" = left(lower("FullName"), 64);
                """);

            // Same merge as IX_Authors_UserId_FullName applied, but on the folded key: books
            // keep pointing at the earliest row and the losing joins are dropped with it.
            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT "Id",
                           "UserId",
                           "NormalizedName",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "NormalizedName"
                               ORDER BY "Id"
                           ) AS "KeepId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "UserId", "NormalizedName"
                               ORDER BY "Id"
                           ) AS "RowNumber"
                    FROM "Authors"
                ),
                merged AS (
                    UPDATE "AuthorBook" AS ab
                    SET "AuthorsId" = d."KeepId"
                    FROM duplicates AS d
                    WHERE ab."AuthorsId" = d."Id"
                      AND d."RowNumber" > 1
                      AND NOT EXISTS (
                          SELECT 1 FROM "AuthorBook" AS existing
                          WHERE existing."AuthorsId" = d."KeepId"
                            AND existing."BooksId" = ab."BooksId"
                      )
                    RETURNING ab."AuthorsId"
                )
                DELETE FROM "Authors"
                WHERE "Id" IN (SELECT "Id" FROM duplicates WHERE "RowNumber" > 1);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "Authors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Authors_UserId_NormalizedName",
                table: "Authors",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Authors_UserId_NormalizedName",
                table: "Authors");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Authors");

            // The merge performed in Up is not reversed: the removed rows were duplicates.
            migrationBuilder.CreateIndex(
                name: "IX_Authors_UserId_FullName",
                table: "Authors",
                columns: new[] { "UserId", "FullName" },
                unique: true);
        }
    }
}
