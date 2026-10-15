using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumivale.Core.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lumivale.Start.GameTests
{
    /// <summary>Native host lifetime coverage; helpers are test-only and need no scene assets.</summary>
    public sealed class GameBootstrapLifetimeTests
    {
        private CleanupBarrierBootstrap first;
        private CleanupBarrierBootstrap replacement;
        private CleanupBarrierService firstService;
        private BootstrapCoordinator firstCoordinator;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (LumiBootstrap.Instance != null)
            {
                var previous = LumiBootstrap.Instance;
                yield return Wait(previous.ShutdownAsync());
                UnityEngine.Object.Destroy(previous.gameObject);
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            firstService?.ReleaseShutdown.TrySetResult(true);
            if (firstCoordinator != null) yield return Wait(firstCoordinator.ShutdownAsync());
            if (replacement != null)
            {
                yield return Wait(replacement.ShutdownAsync());
                UnityEngine.Object.Destroy(replacement.gameObject);
            }
            if (first != null)
            {
                yield return Wait(first.ShutdownAsync());
                UnityEngine.Object.Destroy(first.gameObject);
            }
            CleanupBarrierBootstrap.ServiceFactory = null;
            yield return null;
        }

        [UnityTest]
        [Category("StarterCleanup")]
        public IEnumerator ReplacementBootstrapWaitsForTheDestroyedHostsPendingShutdown()
        {
            firstService = new CleanupBarrierService(blockShutdown: true);
            var replacementService = new CleanupBarrierService(blockShutdown: false);
            CleanupBarrierBootstrap.ServiceFactory = () => firstService;
            first = new GameObject("Lifetime Test Previous Host").AddComponent<CleanupBarrierBootstrap>();
            yield return Wait(first.Ready);
            firstCoordinator = first.Coordinator;
            UnityEngine.Object.Destroy(first.gameObject);
            yield return null;
            Assert.That(firstService.ShutdownStarted, Is.True);

            CleanupBarrierBootstrap.ServiceFactory = () => replacementService;
            replacement = new GameObject("Lifetime Test Replacement Host").AddComponent<CleanupBarrierBootstrap>();
            yield return null;
            Assert.That(replacement.IsReady, Is.False);
            Assert.That(replacement.Ready.IsCompleted, Is.False);
            Assert.That(replacementService.InitializeCount, Is.Zero,
                "Unity destruction must not allow a new service to start before the previous service releases its resources.");

            firstService.ReleaseShutdown.TrySetResult(true);
            yield return Wait(replacement.Ready);
            Assert.That(firstCoordinator.State, Is.EqualTo(BootstrapState.Stopped));
            Assert.That(replacementService.InitializeCount, Is.EqualTo(1));
            Assert.That(LumiBootstrap.Instance, Is.SameAs(replacement));
            Assert.That(UnityEngine.Object.FindObjectsByType<CleanupBarrierBootstrap>(FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            yield return Wait(replacement.ShutdownAsync());
            Assert.That(replacement.Coordinator.State, Is.EqualTo(BootstrapState.Stopped));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Native Bootstrap shutdown barrier timed out.");
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }
    }

    public sealed class CleanupBarrierBootstrap : LumiBootstrap
    {
        public static Func<ILumiService> ServiceFactory { get; set; }

        protected override IEnumerable<ILumiService> CreateServices() => new[] { ServiceFactory() };
    }

    public sealed class CleanupBarrierService : ILumiService
    {
        private readonly bool blockShutdown;

        public CleanupBarrierService(bool blockShutdown) => this.blockShutdown = blockShutdown;

        public string Id => "starter-lifetime-test";
        public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();
        public int InitializeCount { get; private set; }
        public bool ShutdownStarted { get; private set; }
        public TaskCompletionSource<bool> ReleaseShutdown { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync(ServiceContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCount++;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken cancellationToken)
        {
            ShutdownStarted = true;
            return blockShutdown ? ReleaseShutdown.Task : Task.CompletedTask;
        }
    }
}
