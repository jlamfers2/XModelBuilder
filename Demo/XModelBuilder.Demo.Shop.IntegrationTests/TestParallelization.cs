// The whole scenario suite shares ONE SqlConnection (so the per-scenario transaction can wrap both
// the test code and the in-process API requests). That connection is not thread-safe, so scenarios
// must run sequentially - disable xUnit's parallelization for the assembly.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
