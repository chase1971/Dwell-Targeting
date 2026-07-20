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
    public void SettlesBeforeFirstScanAfterForce()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");

        Advance(500);
        Assert.False(state.ShouldScan(EntityA));

        Advance(500);
        Assert.True(state.ShouldScan(EntityA));
    }

    [Fact]
    public void EntityChangeReArmsSettleCycle()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);
        Assert.True(state.ShouldScan(EntityA));
        state.MarkScanned(1, "Test");

        Advance(ScreenScanTiming.LayoutSettleMs);
        Assert.False(state.ShouldScan(EntityB));

        Advance(ScreenScanTiming.LayoutSettleMs);
        Assert.True(state.ShouldScan(EntityB));
    }

    [Fact]
    public void SuccessRespectsRescanInterval()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        Assert.True(state.ShouldScan(EntityA));
        state.MarkScanned(3, "Test");

        Advance(ScreenScanTiming.RescanIntervalMs - 1);
        Assert.False(state.ShouldScan(EntityA));

        Advance(1);
        Assert.True(state.ShouldScan(EntityA));
    }

    [Fact]
    public void EmptyResultRetriesOnEmptyIntervalNeverGivesUp()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        int scanAttempts = 0;
        for (int i = 0; i < 20; i++)
        {
            if (state.ShouldScan(EntityA))
            {
                scanAttempts++;
                state.MarkScanned(0, "Test");
            }

            Advance(ScreenScanTiming.EmptyRetryMs);
        }

        Assert.True(scanAttempts >= 10);
    }

    [Fact]
    public void ForceTriggersRescanAfterSettle()
    {
        var state = new ScreenEntryScanState();
        Advance(ScreenScanTiming.LayoutSettleMs);
        state.MarkScanned(5, "Test");

        Advance(ScreenScanTiming.RescanIntervalMs - 1);
        Assert.False(state.ShouldScan(EntityA));

        state.Force("Test");
        Advance(500);
        Assert.False(state.ShouldScan(EntityA));

        Advance(500);
        Assert.True(state.ShouldScan(EntityA));
    }

    [Fact]
    public void StaticFrameLoop_DoesNotRescanEveryFrameAfterSuccess()
    {
        var state = new ScreenEntryScanState();
        state.ScheduleRescan("Test");
        Advance(ScreenScanTiming.LayoutSettleMs);

        int shouldScanTrueCount = 0;
        Assert.True(state.ShouldScan(EntityA));
        state.MarkScanned(2, "Test");

        const int frames = 1000;
        const long frameMs = 16;
        for (int frame = 0; frame < frames; frame++)
        {
            Advance(frameMs);
            if (state.ShouldScan(EntityA))
            {
                shouldScanTrueCount++;
                state.MarkScanned(2, "Test");
            }
        }

        // Steady state rescans exactly once per RescanIntervalMs — never every frame.
        long expected = (frames * frameMs) / ScreenScanTiming.RescanIntervalMs;
        Assert.InRange(shouldScanTrueCount, expected - 3, expected + 3);
    }
}
