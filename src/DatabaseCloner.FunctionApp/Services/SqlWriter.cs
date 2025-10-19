using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DatabaseCloner.FunctionApp.Configuration;

namespace DatabaseCloner.FunctionApp.Services;

public interface ISqlWriter
{
    Task BulkInsertAsync<T>(IEnumerable<T> items, Func<T, SqlParameter[]> parameterFactory, string tableName, CancellationToken cancellationToken = default);
}

public class SqlWriter : ISqlWriter
{
    private readonly SqlOptions _options;
    private readonly ILogger<SqlWriter> _logger;

    public SqlWriter(IOptions<SqlOptions> options, ILogger<SqlWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task BulkInsertAsync<T>(IEnumerable<T> items, Func<T, SqlParameter[]> parameterFactory, string tableName, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            _logger.LogInformation("No items to insert into {Table}", tableName);
            return;
        }

        await using var connection = new SqlConnection(_options.PrimaryConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var item in itemList)
        {
            var parameters = parameterFactory(item);
            using var command = new SqlCommand
            {
                Connection = connection,
                Transaction = transaction,
                CommandType = CommandType.Text,
                CommandText = $"INSERT INTO {tableName} VALUES (" + string.Join(",", Enumerable.Range(0, parameters.Length).Select(i => $"@p{i}")) + ")"
            };

            for (var i = 0; i < parameters.Length; i++)
            {
                parameters[i].ParameterName = $"@p{i}";
                command.Parameters.Add(parameters[i]);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Inserted {Count} records into {Table}", itemList.Count, tableName);
    }
}
