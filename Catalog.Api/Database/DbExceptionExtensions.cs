using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Catalog.Api.Database;

public static class DbExceptionExtensions
{
    /// <summary>
    /// PostgreSQL unique violation (SQLSTATE 23505).
    /// </summary>
    private const string UniqueViolationSqlState = "23505";

    public const string TagNameIndexName = "IX_Tags_UserId_Name";
    public const string AuthorNameIndexName = "IX_Authors_UserId_NormalizedName";

    /// <summary>
    /// Detects a unique violation on a specific constraint. The constraint is named by the
    /// caller on purpose: treating any unique violation as a domain conflict would hide
    /// unrelated integrity errors behind a misleading 409.
    /// </summary>
    public static bool IsUniqueViolationOn(this DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException { SqlState: UniqueViolationSqlState } postgres
               && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}
