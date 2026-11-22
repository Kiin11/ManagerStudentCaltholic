using System.Collections.Concurrent;
using ManagerStudentCaltholic.Interface.Services;

namespace ManagerStudentCaltholic.Services
{
    /// <summary>
    /// TASK-INF-08: Tín hiệu gom nhóm (coalescing) trong bộ nhớ để đánh thức luồng Write-Behind Flush
    /// đồng bộ các phân vùng điểm danh đang có IsDirty = true trên Redis xuống PostgreSQL theo lô.
    /// </summary>
    public interface IAttendanceFlushSignal
    {
        void NotifyDirty(DateTime dateUtc, int classId);
        IReadOnlyList<(int ClassId, DateTime DateUtc)> DrainPendingPartitions();
        void RequeuePartitions(IEnumerable<(int ClassId, DateTime DateUtc)> partitions);
        bool HasPendingPartitions { get; }
        Task<bool> WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken);
        Task WaitForCoalesceOrThresholdAsync(TimeSpan debounceWindow, CancellationToken cancellationToken);
    }

    public sealed class AttendanceWriteBehindSignal : IAttendanceFlushSignal
    {
        public const int ImmediateBatchThreshold = 25;

        private readonly ConcurrentDictionary<(int ClassId, DateTime DateUtc), byte> _pendingPartitions = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private readonly SemaphoreSlim _thresholdSignal = new(0, 1);
        private int _pendingScanEvents;

        public bool HasPendingPartitions => !_pendingPartitions.IsEmpty;

        public void NotifyDirty(DateTime dateUtc, int classId)
        {
            if (classId <= 0) return;

            var normalizedDate = DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
            _pendingPartitions[(classId, normalizedDate)] = 0;

            var count = Interlocked.Increment(ref _pendingScanEvents);
            ReleaseIfEmpty(_wakeSignal);

            if (count >= ImmediateBatchThreshold)
            {
                ReleaseIfEmpty(_thresholdSignal);
            }
        }

        public IReadOnlyList<(int ClassId, DateTime DateUtc)> DrainPendingPartitions()
        {
            Interlocked.Exchange(ref _pendingScanEvents, 0);
            DrainSemaphore(_thresholdSignal);

            if (_pendingPartitions.IsEmpty)
            {
                return Array.Empty<(int ClassId, DateTime DateUtc)>();
            }

            var drained = new List<(int ClassId, DateTime DateUtc)>();
            foreach (var key in _pendingPartitions.Keys)
            {
                if (_pendingPartitions.TryRemove(key, out _))
                {
                    drained.Add(key);
                }
            }

            return drained;
        }

        public void RequeuePartitions(IEnumerable<(int ClassId, DateTime DateUtc)> partitions)
        {
            bool any = false;
            foreach (var (classId, dateUtc) in partitions)
            {
                if (classId <= 0) continue;
                var normalizedDate = DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
                _pendingPartitions[(classId, normalizedDate)] = 0;
                any = true;
            }

            if (any)
            {
                ReleaseIfEmpty(_wakeSignal);
            }
        }

        public async Task<bool> WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!_pendingPartitions.IsEmpty)
            {
                DrainSemaphore(_wakeSignal);
                return true;
            }

            return await _wakeSignal.WaitAsync(timeout, cancellationToken);
        }

        public async Task WaitForCoalesceOrThresholdAsync(TimeSpan debounceWindow, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _pendingScanEvents) >= ImmediateBatchThreshold)
            {
                DrainSemaphore(_thresholdSignal);
                return;
            }

            await _thresholdSignal.WaitAsync(debounceWindow, cancellationToken);
        }

        private static void ReleaseIfEmpty(SemaphoreSlim semaphore)
        {
            if (semaphore.CurrentCount == 0)
            {
                try
                {
                    semaphore.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Đã có tín hiệu chờ sẵn từ luồng khác
                }
            }
        }

        private static void DrainSemaphore(SemaphoreSlim semaphore)
        {
            while (semaphore.CurrentCount > 0)
            {
                if (!semaphore.Wait(0))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// TASK-INF-08: BackgroundService đồng bộ Write-Behind theo lô (mỗi 5 giây hoặc ngay khi gom đủ 25 lượt quét)
    /// từ Redis xuống PostgreSQL, đảm bảo quét QR < 150ms, giảm >= 60% truy vấn DB và không mất dữ liệu khi tắt ứng dụng.
    /// </summary>
    public sealed class AttendanceWriteBehindBackgroundService : BackgroundService
    {
        private static readonly TimeSpan StartupRecoveryDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan CoalesceDebounceWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SafetySweepInterval = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAttendanceFlushSignal _signal;
        private readonly ILogger<AttendanceWriteBehindBackgroundService> _logger;

        public AttendanceWriteBehindBackgroundService(
            IServiceScopeFactory scopeFactory,
            IAttendanceFlushSignal signal,
            ILogger<AttendanceWriteBehindBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _signal = signal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. Quét khôi phục các phân vùng còn IsDirty trên Redis AOF ngay khi ứng dụng khởi động lại
            try
            {
                await Task.Delay(StartupRecoveryDelay, stoppingToken);
                await RunFullDirtySweepAsync("StartupRecovery");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AttendanceWriteBehind] Lỗi trong lượt quét khôi phục khởi động.");
            }

            // 2. Vòng lặp chính: gom nhóm các lượt quét QR trong cửa sổ 5 giây (hoặc ngưỡng 25 lượt) rồi Batch Upsert 1 lần xuống PostgreSQL
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool signaled = await _signal.WaitForSignalAsync(SafetySweepInterval, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (signaled || _signal.HasPendingPartitions)
                    {
                        await _signal.WaitForCoalesceOrThresholdAsync(CoalesceDebounceWindow, stoppingToken);

                        var partitions = _signal.DrainPendingPartitions();
                        if (partitions.Count > 0)
                        {
                            try
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var attendanceService = scope.ServiceProvider.GetRequiredService<IRedisAttendanceService>();
                                await attendanceService.SyncDirtyClassesForPartitionsAsync(partitions);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "[AttendanceWriteBehind] Lỗi đồng bộ lô {PartitionCount} phân vùng xuống PostgreSQL; đưa lại vào hàng đợi thử lại.",
                                    partitions.Count);
                                _signal.RequeuePartitions(partitions);
                                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                            }
                        }
                    }
                    else
                    {
                        // Định kỳ 30s quét nhẹ Redis dirty index phòng trường hợp có dữ liệu chưa được báo tín hiệu trong bộ nhớ
                        await RunFullDirtySweepAsync("PeriodicSafetySweep");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AttendanceWriteBehind] Lỗi không mong muốn trong vòng lặp Write-Behind.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);

            // Đảm bảo Flush toàn bộ dữ liệu điểm danh đang chờ trên Redis xuống PostgreSQL trước khi container dừng (SIGTERM)
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var attendanceService = scope.ServiceProvider.GetRequiredService<IRedisAttendanceService>();

                var remainingPartitions = _signal.DrainPendingPartitions();
                if (remainingPartitions.Count > 0)
                {
                    var (clsCount, recCount) = await attendanceService.SyncDirtyClassesForPartitionsAsync(remainingPartitions);
                    _logger.LogInformation(
                        "[AttendanceWriteBehind] Graceful shutdown flushed {Records} records across {Classes} signaled partitions.",
                        recCount, clsCount);
                }

                var (sweepClasses, sweepRecords) = await attendanceService.SyncDirtyAttendancesToDatabaseAsync(null);
                if (sweepClasses > 0)
                {
                    _logger.LogInformation(
                        "[AttendanceWriteBehind] Graceful shutdown sweep flushed {Records} additional records across {Classes} classes.",
                        sweepRecords, sweepClasses);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AttendanceWriteBehind] Lỗi khi Flush cuối cùng lúc tắt ứng dụng (dữ liệu vẫn nằm trên Redis AOF để Hangfire/lần khởi động sau đồng bộ).");
            }
        }

        private async Task RunFullDirtySweepAsync(string source)
        {
            using var scope = _scopeFactory.CreateScope();
            var attendanceService = scope.ServiceProvider.GetRequiredService<IRedisAttendanceService>();
            var (classesSynced, recordsUpserted) = await attendanceService.SyncDirtyAttendancesToDatabaseAsync(null);

            if (classesSynced > 0)
            {
                _logger.LogInformation(
                    "[AttendanceWriteBehind:{Source}] Đã đồng bộ {Records} bản ghi của {Classes} lớp từ Redis xuống PostgreSQL.",
                    source, recordsUpserted, classesSynced);
            }
        }
    }
}
