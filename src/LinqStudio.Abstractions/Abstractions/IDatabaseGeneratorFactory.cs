
namespace LinqStudio.Abstractions.Abstractions;

public interface IDatabaseGeneratorFactory
{
	IDatabaseQueryGenerator Create(string connectionString);
}
