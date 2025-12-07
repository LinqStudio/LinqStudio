using Xunit;

// E2E tests must run sequentially because they share server resources (port 5020)
[assembly: CollectionBehavior(DisableTestParallelization = true)]
