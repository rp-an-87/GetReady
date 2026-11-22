using Xunit;

// The settings are static state: one test at a time
[assembly: CollectionBehavior(DisableTestParallelization = true)]

[assembly: GetReady.Tests.IsolateStatics]
[assembly: TestCollectionOrderer("GetReady.Tests.SeededCollectionOrderer", "GetReady.Tests")]
[assembly: TestCaseOrderer("GetReady.Tests.SeededCaseOrderer", "GetReady.Tests")]
