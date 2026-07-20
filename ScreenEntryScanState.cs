namespace DwellTargeting;

/// <summary>
/// Time-based rescan scheduling — entity change or Force() rescans after settle; empty results retry
/// on EmptyRetryMs; non-empty results refresh on RescanIntervalMs. No permanent "resolved" give-up.
/// </summary>
internal struct ScreenEntryScanState
{
    private ulong _entityId;
    private long _entityBoundAtMs;
    private long _lastScanMs;
    private bool _forcePending;
    private bool _lastResultEmpty;
    private long _settleMs;

    internal void Reset()
    {
        _entityId = 0;
        _entityBoundAtMs = 0;
        _lastScanMs = 0;
        _forcePending = false;
        _lastResultEmpty = true;
        _settleMs = 0;
    }

    /// <summary>Alias for Reset — used when overlay hides.</summary>
    internal void OnHide() => Reset();

    /// <summary>Immediate rescan after layout settle (mode enter, user action, invalidation).</summary>
    internal void Force(string logTag)
    {
        _forcePending = true;
        _entityBoundAtMs = ScanRuntime.NowMs();
        ScanRuntime.Info($"[{logTag}] rescan forced.");
    }

    /// <summary>Call when mode enters or user action requires a fresh snapshot.</summary>
    internal void ScheduleRescan(string logTag, long settleMs = 0)
    {
        _settleMs = settleMs;
        Force(logTag);
    }

    /// <summary>Returns true when a discovery scan should run now.</summary>
    internal bool ShouldScan(ulong entityId)
    {
        long now = ScanRuntime.NowMs();

        if (_entityId != entityId)
        {
            bool firstBind = _entityId == 0;
            _entityId = entityId;
            if (!firstBind || !_forcePending)
            {
                _forcePending = true;
                _entityBoundAtMs = now;
                _lastResultEmpty = true;
            }
        }

        if (_forcePending)
        {
            long settleMs = _settleMs > 0 ? _settleMs : ScreenScanTiming.LayoutSettleMs;
            if (now - _entityBoundAtMs < settleMs)
                return false;

            return true;
        }

        long interval = _lastResultEmpty ? ScreenScanTiming.EmptyRetryMs : ScreenScanTiming.RescanIntervalMs;
        return _lastScanMs == 0 || now - _lastScanMs >= interval;
    }

    /// <summary>Call after a scan attempt completes.</summary>
    internal void MarkScanned(int resultCount, string logTag)
    {
        _lastScanMs = ScanRuntime.NowMs();
        _forcePending = false;
        _settleMs = 0;
        _lastResultEmpty = resultCount <= 0;

        if (_lastResultEmpty)
            ScanRuntime.Info($"[{logTag}] scan empty — next retry in {ScreenScanTiming.EmptyRetryMs}ms.");
    }
}
