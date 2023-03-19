using BenchmarkDotNet.Running;
using PhotonBlue.PRS.Benchmarks;

// The final algorithm should take no longer than 24us.
BenchmarkRunner.Run<PrsDecoding>();