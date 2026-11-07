using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Concurrent uploads of the same author previously created duplicate rows:
            // both requests missed each other in the pre-insert lookup. Existing duplicates
            // are merged onto the earliest row before the index can be created.
            migrationBuilder.Sql("""
                WITH duplicates AS (
                    SELECT "Id",
                           "UserId",
                           "FullName",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "UserId", "FullName"
                               ORDER BY "Id"
                           ) AS "KeepId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "UserId", "FullName"
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

            migrationBuilder.CreateIndex(
                name: "IX_Authors_UserId_FullName",
                table: "Authors",
                columns: new[] { "UserId", "FullName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Authors_UserId_FullName",
                table: "Authors");
        }
    }
}
