using Xunit;

// Disable parallel test execution for E2E tests since they share server resources
[assembly: CollectionBehavior(DisableTestParallelization = true)]
