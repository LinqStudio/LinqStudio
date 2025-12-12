
using LinqStudio.Abstractions.Abstractions;
using LinqStudio.Databases;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace LinqStudio.Core.Services;

internal class DatabaseGeneratorFactory_Mssql : IDatabaseGeneratorFactory
{
	public IDatabaseQueryGenerator Create(string connectionString) => new MssqlGenerator(new SqlConnection(connectionString));
}

internal class DatabaseGeneratorFactory_Mysql : IDatabaseGeneratorFactory
{
	public IDatabaseQueryGenerator Create(string connectionString) => new MySqlGenerator(new MySqlConnection(connectionString));
}
