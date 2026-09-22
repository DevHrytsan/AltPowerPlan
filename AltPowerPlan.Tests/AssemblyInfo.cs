using Xunit;

// WPF UI tests interact with static Application.Current and Dispatcher;
// disable cross-class parallelization to prevent race conditions in headless CI runners
[assembly: CollectionBehavior(DisableTestParallelization = true)]
