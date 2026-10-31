using System.Text.Json;
using System.Text.Json.Serialization;
using ManagerStudentCaltholic.Interface.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ManagerStudentCaltholic.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryFallback;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly TimeSpan _defaultExpiration;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public RedisCacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryFallback,
            IConfiguration configuration,
            ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache;
            _memoryFallback = memoryFallback;
            _logger = logger;

            int defaultMinutes = int.TryParse(configuration["CacheSettings:DefaultExpirationMinutes"], out int m) ? m : 60;
            _defaultExpiration = TimeSpan.FromMinutes(defaultMinutes);
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var cachedJson = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    return JsonSerializer.Deserialize<T>(cachedJson, SerializerOptions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisCache] Không thể đọc khóa {Key} từ Redis, chuyển sang đọc từ MemoryCache dự phòng.", key);
                if (_memoryFallback.TryGetValue(key, out T? fallbackVal))
                {
                    return fallbackVal;
                }
            }

            return default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var ttl = expiration ?? _defaultExpiration;

            // Luôn đồng bộ một bản lưu ngắn hạn trên MemoryCache dự phòng
            _memoryFallback.Set(key, value, ttl);

            try
            {
                var serialized = JsonSerializer.Serialize(value, SerializerOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetStringAsync(key, serialized, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisCache] Không thể ghi khóa {Key} lên Redis, đã lưu tạm trên MemoryCache.", key);
            }
        }

        public async Task<T> GetOrSetAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
            {
                return cached;
            }

            var freshData = await factory();
            if (freshData != null)
            {
                await SetAsync(key, freshData, expiration, cancellationToken);
            }

            return freshData;
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _memoryFallback.Remove(key);
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisCache] Lỗi khi xóa khóa {Key} trên Redis.", key);
            }
        }

        public async Task InvalidateAcademicYearsCacheAsync(CancellationToken cancellationToken = default)
        {
            await RemoveAsync(CacheKeys.AllAcademicYears, cancellationToken);
            await RemoveAsync(CacheKeys.CurrentAcademicYear, cancellationToken);
        }

        public async Task InvalidateClassesCacheAsync(int academicYearId, CancellationToken cancellationToken = default)
        {
            await RemoveAsync(CacheKeys.ClassesByYear(academicYearId), cancellationToken);
        }

        public async Task InvalidateGradeConfigsCacheAsync(int academicYearId, string gradeLevel, int classId, CancellationToken cancellationToken = default)
        {
            await RemoveAsync(CacheKeys.GradeConfigsByClass(academicYearId, gradeLevel, classId), cancellationToken);
        }
    }
}