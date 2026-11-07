using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Auth.Api.Database;

public static class DbExceptionExtensions
{
    /// <summary>
    /// PostgreSQL unique violation (SQLSTATE 23505).
    /// </summary>
    private const string UniqueViolationSqlState = "23505";

    /// <summary>
    /// Detects a unique violation on a specific constraint, e.g. the email index on Users.
    /// Callers must name the constraint they expect: treating any unique violation as a
    /// domain conflict would hide unrelated integrity errors behind a misleading 409.
    /// </summary>
    public static bool IsUniqueViolationOn(this DbUpdateException exception, string constraintName)
    {
        return exception.InnerException is PostgresException { SqlState: UniqueViolationSqlState } postgres
               && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}
