/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

#if NBENCH_TESTS

using System;
using System.Collections;
using System.Linq;
using NBench.Reporting.Targets;
using NBench.Sdk;
using NBench.Sdk.Compiler;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.NUnitPerformanceTesting
{
    public abstract class PerformanceTestSuite<T>
    {
        [TestCaseSource(nameof(Benchmarks))]
        public void PerformanceTests(Benchmark benchmark)
        {
            Benchmark.PrepareForRun();
            benchmark.Run();
            benchmark.Finish();
        }

        public static IEnumerable Benchmarks()
        {
            var discovery = new ReflectionDiscovery(new ActionBenchmarkOutput(report => { }, results =>
            {
                foreach (var assertion in results.AssertionResults)
                {
                    Assert.True(assertion.Passed, results.BenchmarkName + " " + assertion.Message);
                    Console.WriteLine(assertion.Message);
                }

                foreach (var nameToMetric in results.Data.StatsByMetric)
                {
                    var stats = nameToMetric.Value.Stats;

                    Console.WriteLine($"Metric: {nameToMetric.Key} [Unit: {nameToMetric.Value.Unit}]: ");
                    Console.WriteLine($"Min: {stats.Min}");
                    Console.WriteLine($"Max: {stats.Max}");
                    Console.WriteLine($"Average: {stats.Average}");
                    Console.WriteLine($"Std Err: {stats.StandardError}");
                    Console.WriteLine($"Std Dev: {stats.StandardDeviation}");
                }
            }));

            var benchmarks = discovery.FindBenchmarks(typeof(T)).ToList();

            foreach (var benchmark in benchmarks)
            {
                var name = benchmark.BenchmarkName.Split('+')[1];
                yield return new TestCaseData(benchmark).SetName(name);
            }
        }
    }
}

#endif