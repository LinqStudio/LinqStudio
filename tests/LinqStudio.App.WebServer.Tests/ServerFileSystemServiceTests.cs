using LinqStudio.App.WebServer.Services;

namespace LinqStudio.App.WebServer.Tests;

public class ServerFileSystemServiceTests
{
	[Fact]
	public void GetDefaultDocumentsPath_ReturnsNonEmptyPath()
	{
		// Arrange
		var service = new ServerFileSystemService();

		// Act
		var path = service.GetDefaultDocumentsPath();

		// Assert
		Assert.NotNull(path);
		Assert.NotEmpty(path);
		Assert.True(Path.IsPathRooted(path), "Path should be absolute");
	}

	[Fact]
	public async Task PromptSaveFileAsync_CombinesPathWithFileName()
	{
		// Arrange
		var service = new ServerFileSystemService();
		var fileName = "test.linq";

		// Act
		var result = await service.PromptSaveFileAsync(fileName);

		// Assert
		Assert.NotNull(result);
		Assert.EndsWith(fileName, result);
		Assert.Contains(Path.DirectorySeparatorChar.ToString(), result);
	}

	[Fact]
	public async Task PromptOpenFileAsync_ReturnsDefaultPath()
	{
		// Arrange
		var service = new ServerFileSystemService();

		// Act
		var result = await service.PromptOpenFileAsync();

		// Assert
		Assert.NotNull(result);
		Assert.True(Path.IsPathRooted(result), "Result should be absolute path");
	}
}