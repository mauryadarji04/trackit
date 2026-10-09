using Microsoft.Data.SqlClient;
using TrackIt.Core;

namespace TrackIt.Infrastructure.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}

/// <summary>Gives Dapper a fresh connection for each call (ADO.NET pools them).</summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;

    public SqlConnection Create() => new(_connectionString);
}

internal static class SqlErrors
{
    /// <summary>Our stored procedures THROW numbers 50001-50006 for rule violations.</summary>
    public static bool IsBusinessError(SqlException ex) => ex.Number is >= 50001 and <= 50006;

    public static Exception ToDomainException(SqlException ex) =>
        ex.Number == 50003 ? new NotFoundException(ex.Message) : new BusinessRuleException(ex.Message);
}
