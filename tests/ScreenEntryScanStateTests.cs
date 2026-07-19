using DwellTargeting;
using Xunit;

namespace DwellTargeting.Tests;

public sealed class ScreenEntryScanStateTests : IDisposable
{
    private const ulong EntityA = 1001;
    private const ulong EntityB = 2002;

    private long _nowMs;

    public ScreenEntryScanStateTests()
    {
        _nowMs = 0;
        ScanRuntime.NowMs = () => _nowMs;
        ScanRuntime.Info = _ => { };
        ScanRuntime.Warn = _ => { };
    }

    public void Dispose()
    {
        ScanRuntime.NowMs = () => System.Environment.TickCount64;
        ScanRuntime.Info = _ => { };
        ScanRuntime.Warn = _ => { };
    }

    private void Advance(long ms) => _nowMs += ms;

    [Fact]
    public void SettlesBeforeFirstScan()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");

        Advance(500);
        Assert.False(state.ShouldScanNow(EntityA, hasValidCache: false, out var waiting));
        Assert.True(waiting);

        Advance(500);
        Assert.True(state.ShouldScanNow(EntityA, hasValidCache: false, out waiting));
        Assert.False(waiting);
    }

    [Fact]
    public void EntityChangeReArmsSettleCycle()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);
        Assert.True(state.ShouldScanNow(EntityA, hasValidCache: false, out _));

        Advance(ScreenScanTiming.LayoutSettleMs);
        Assert.False(state.ShouldScanNow(EntityB, hasValidCache: false, out var waiting));
        Assert.True(waiting);
    }

    [Fact]
    public void SuccessCachesAndGoesQuiet()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        Assert.True(state.ShouldScanNow(EntityA, hasValidCache: false, out _));
        Assert.True(state.RegisterScanResult(3, "Test"));

        for (int frame = 0; frame < 100; frame++)
        {
            Advance(16);
            Assert.False(state.ShouldScanNow(EntityA, hasValidCache: true, out _));
        }
    }

    [Fact]
    public void EmptyRetriesAreBoundedThenStop()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        int scanAttempts = 0;
        while (true)
        {
            if (state.ShouldScanNow(EntityA, hasValidCache: false, out _))
            {
                scanAttempts++;
                if (state.RegisterScanResult(0, "Test"))
                    break;

                Advance(ScreenScanTiming.EmptyRetryMs);
            }
            else
            {
                Advance(16);
            }
        }

        Assert.Equal(ScreenScanTiming.MaxEmptyRetries + 1, scanAttempts);

        for (int frame = 0; frame < 100; frame++)
        {
            Advance(16);
            Assert.False(state.ShouldScanNow(EntityA, hasValidCache: false, out _));
        }
    }

    [Fact]
    public void ReArmAfterGiveUpStartsExactlyOneNewCycle()
    {
        var state = new ScreenEntryScanState();
        ExhaustEmptyRetries(ref state);

        state.ScheduleRescan("Test");
        Advance(500);
        Assert.False(state.ShouldScanNow(EntityA, hasValidCache: false, out var waiting));
        Assert.True(waiting);

        Advance(500);
        Assert.True(state.ShouldScanNow(EntityA, hasValidCache: false, out waiting));
        Assert.False(waiting);
    }

    [Fact]
    public void StaticFrameLoop_DoesNotRescanEveryFrameAfterGiveUp()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        int shouldScanTrueCount = 0;
        bool hasValidCache = false;

        for (int frame = 0; frame < 1000; frame++)
        {
            if (state.ShouldScanNow(EntityA, hasValidCache, out _))
            {
                shouldScanTrueCount++;
                if (state.RegisterScanResult(0, "Test"))
                    break;

                Advance(ScreenScanTiming.EmptyRetryMs);
            }

            Advance(16);
        }

        for (int frame = 0; frame < 1000; frame++)
        {
            Advance(16);
            if (state.ShouldScanNow(EntityA, hasValidCache, out _))
                shouldScanTrueCount++;
        }

        Assert.InRange(shouldScanTrueCount, 1, ScreenScanTiming.MaxEmptyRetries + 1);
        Assert.True(shouldScanTrueCount < 100, $"Expected bounded scans, got {shouldScanTrueCount}.");
    }

    private void ExhaustEmptyRetries(ref ScreenEntryScanState state)
    {
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        while (true)
        {
            if (!state.ShouldScanNow(EntityA, hasValidCache: false, out _))
            {
                Advance(16);
                continue;
            }

            if (state.RegisterScanResult(0, "Test"))
                return;

            Advance(ScreenScanTiming.EmptyRetryMs);
        }
    }
}
