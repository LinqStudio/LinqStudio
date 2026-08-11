using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace LinqStudio.Core.Services;

/// <summary>
/// Describes a generated <see cref="Microsoft.EntityFrameworkCore.DbContext"/> exposed to queries.
/// </summary>
public sealed record CompiledProjectContext(
	string DatabaseName,
	string ContextTypeName,
	string Namespace);

/// <summary>
/// Immutable compiled model data shared by IntelliSense and query executions.
/// </summary>
/// <remarks>
/// The snapshot owns the collectible load context containing the generated model assembly.
/// Consumers must hold a lease while using the assembly or its metadata reference.
/// </remarks>
public sealed class CompiledProjectSnapshot : IDisposable
{
	private int _referenceCount = 1;
	private int _ownerReleased;

	internal CompiledProjectSnapshot(
		IReadOnlyList<CompiledProjectContext> contexts,
		IReadOnlyDictionary<string, string> modelFiles,
		IReadOnlyDictionary<string, string> dbContextFiles,
		byte[] assemblyBytes,
		AssemblyLoadContext loadContext,
		Assembly assembly,
		MetadataReference metadataReference)
	{
		Contexts = contexts;
		ModelFiles = modelFiles;
		DbContextFiles = dbContextFiles;
		AssemblyBytes = assemblyBytes;
		LoadContext = loadContext;
		Assembly = assembly;
		MetadataReference = metadataReference;
	}

	/// <summary>Gets the generated database contexts available to queries.</summary>
	public IReadOnlyList<CompiledProjectContext> Contexts { get; }

	/// <summary>Gets the generated entity source files.</summary>
	public IReadOnlyDictionary<string, string> ModelFiles { get; }

	/// <summary>Gets the generated <c>DbContext</c> source files.</summary>
	public IReadOnlyDictionary<string, string> DbContextFiles { get; }

	/// <summary>Gets the emitted model assembly image.</summary>
	public byte[] AssemblyBytes { get; }
	internal AssemblyLoadContext LoadContext { get; }
	internal Assembly Assembly { get; }
	internal MetadataReference MetadataReference { get; }

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _ownerReleased, 1) == 0)
			Release();
	}

	/// <summary>
	/// Acquires a reference-counted lease that keeps this snapshot alive.
	/// </summary>
	internal CompiledProjectSnapshotLease AcquireLease()
	{
		while (true)
		{
			var referenceCount = Volatile.Read(ref _referenceCount);
			if (referenceCount == 0)
				throw new ObjectDisposedException(nameof(CompiledProjectSnapshot));

			if (Interlocked.CompareExchange(ref _referenceCount, referenceCount + 1, referenceCount) == referenceCount)
				return new CompiledProjectSnapshotLease(this);
		}
	}

	internal void Release()
	{
		if (Interlocked.Decrement(ref _referenceCount) == 0)
			LoadContext.Unload();
	}
}

/// <summary>
/// Keeps a <see cref="CompiledProjectSnapshot"/> alive until the lease is disposed.
/// </summary>
public sealed class CompiledProjectSnapshotLease : IDisposable
{
	private CompiledProjectSnapshot? _snapshot;

	internal CompiledProjectSnapshotLease(CompiledProjectSnapshot snapshot)
	{
		_snapshot = snapshot;
	}

	/// <summary>Gets the snapshot protected by this lease.</summary>
	public CompiledProjectSnapshot Snapshot
		=> _snapshot ?? throw new ObjectDisposedException(nameof(CompiledProjectSnapshotLease));

	public void Dispose()
	{
		Interlocked.Exchange(ref _snapshot, null)?.Release();
	}
}
