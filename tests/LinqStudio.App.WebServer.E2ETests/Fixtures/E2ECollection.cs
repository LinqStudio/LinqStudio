using Xunit;

namespace LinqStudio.App.WebServer.E2ETests.Fixtures;

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<AppServerFixture>, ICollectionFixture<PlaywrightFixture>
{
	// collection shared between tests
}
