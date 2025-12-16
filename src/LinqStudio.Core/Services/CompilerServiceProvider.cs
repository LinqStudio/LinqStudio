namespace LinqStudio.Core.Services;

/// <summary>
/// Provides and manages the lifecycle of CompilerService instances.
/// Ensures CompilerService is only disposed when the provider itself is disposed,
/// preventing race conditions with Monaco editor callbacks.
/// </summary>
public class CompilerServiceProvider : IDisposable
{
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CompilerService? _compilerService;
	private bool _disposed;
	private string? _currentContextTypeName;
	private string? _currentProjectNamespace;

	/// <summary>
	/// Gets or creates a CompilerService instance.
	/// If context type or namespace changes, a new instance is created.
	/// </summary>
	public async Task<CompilerService> GetOrCreateAsync(string contextTypeName, string projectNamespace)
	{
		ObjectDisposedException.ThrowIf(_disposed, nameof(CompilerServiceProvider));

		await _initLock.WaitAsync();
		try
		{
			// If we already have a service with the same configuration, reuse it
			if (_compilerService is not null &&
				_currentContextTypeName == contextTypeName &&
				_currentProjectNamespace == projectNamespace)
			{
				return _compilerService;
			}

			// Configuration changed or first initialization - dispose old instance if exists
			if (_compilerService is not null)
			{
				try
				{
					_compilerService.Dispose();
				}
				catch { }
			}

			// Create new instance
			_compilerService = new CompilerService(contextTypeName, projectNamespace);
			_currentContextTypeName = contextTypeName;
			_currentProjectNamespace = projectNamespace;

			return _compilerService;
		}
		finally
		{
			_initLock.Release();
		}
	}

	/// <summary>
	/// Gets the current CompilerService instance if one exists.
	/// Returns null if no instance has been created yet.
	/// </summary>
	public CompilerService? GetCurrent()
	{
		return _compilerService;
	}

	/// <summary>
	/// Clears the current CompilerService instance.
	/// The next call to GetOrCreateAsync will create a new instance.
	/// </summary>
	public async Task ClearAsync()
	{
		await _initLock.WaitAsync();
		try
		{
			if (_compilerService is not null)
			{
				try
				{
					_compilerService.Dispose();
				}
				catch { }

				_compilerService = null;
				_currentContextTypeName = null;
				_currentProjectNamespace = null;
			}
		}
		finally
		{
			_initLock.Release();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Dispose the compiler service
		if (_compilerService is not null)
		{
			try
			{
				_compilerService.Dispose();
			}
			catch { }
			_compilerService = null;
		}

		// Dispose the lock
		try
		{
			_initLock?.Dispose();
		}
		catch { }

		GC.SuppressFinalize(this);
	}
}