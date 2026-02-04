using System.Buffers;
using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Storage;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;

namespace CryptoCore.Analytics;

/// <summary>
/// Replays archived market data (Tardis/cache), reconstructs the L2 order book
/// via <see cref="OrderBookL2"/> and forwards events to <see cref="IMarketDataListener"/>.
/// Analytics-oriented variant: no matcher, only feature/regressor generation.
/// </summary>
public sealed class MarketDataCacheReplayer : IDisposable
{
    private readonly string _rootDir;
    private readonly MarketDataHash _hash;
    private readonly int _rateMs;
    private readonly IMarketDataListener _sink;
    private readonly OrderBookL2 _book;
    private readonly int _initialBufferSize;

    private LevelUpdate[] _buffer;
    private int _bufferSize;
    private int _buffPtr;

    private bool _isSnapshot;
    private long _currentTsMs;
    private long _nextReportTimestamp;

    // Binance-like ids
    private ulong _nextUpdateId;
    private ulong _prevUpdateId;
    private ulong _firstUpdateId;
    private ulong _lastUpdateId;

    private L2UpdatePooled? _currentPool;
    private IDisposable? _bookSub;
    private IDisposable? _topSub;

    private long _currentEventTimeMs;

    public MarketDataCacheReplayer(
        string rootDir,
        MarketDataHash hash,
        IMarketDataListener sink,
        int rateMs = 100,
        int initialBufferSize = 128)
    {
        ArgumentNullException.ThrowIfNull(rootDir);
        ArgumentNullException.ThrowIfNull(sink);
        _rootDir = rootDir;
        _hash = hash;
        _rateMs = rateMs;
        _sink = sink;
        _initialBufferSize = initialBufferSize > 0 ? initialBufferSize : 128;

        _book = new OrderBookL2(hash.Symbol);

        _bufferSize = _initialBufferSize;
        _buffer = ArrayPool<LevelUpdate>.Shared.Rent(_bufferSize);

        InitIdsAndTimestamps();


        _bookSub = _book.OnBookUpdated(book =>
        {
            _sink.OrderBookUpdated(_currentEventTimeMs, book);
        });

        _topSub = _book.OnTopUpdated(book =>
        {
            var (bbPx, bbQty) = book.BestBid();
            var (baPx, baQty) = book.BestAsk();
            _sink.TopUpdated(_currentEventTimeMs, bbPx, bbQty, baPx, baQty);
        });
    }

    private void InitIdsAndTimestamps()
    {
        _buffPtr = 0;
        _isSnapshot = true;

        _nextUpdateId = 1;
        _prevUpdateId = 0;
        _firstUpdateId = 0;
        _lastUpdateId = 0;

        var startOfDay = _hash.Date
            .ToDateTime(new TimeOnly(0, 0, 0))
            .ToUnixMillisecondsTimestamp();

        _currentTsMs = startOfDay;
        _nextReportTimestamp = startOfDay + _rateMs;
        _currentEventTimeMs = startOfDay;
    }

    /// <summary>
    /// Main replay loop. Sequentially reads cached market data and sends
    /// reconstructed order book and trade events to the listener sink.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the replay.</param>
    public void Run(CancellationToken cancellationToken = default)
    {
        using var accessor = new MarketDataCacheAccessor<PackedMarketData24>(_rootDir, _hash);

        foreach (var packed in accessor.ReadAll())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (packed.IsTrade())
            {
                var trade = packed.ToTrade(_hash.Date);
                _sink.TradeReceived(in trade);
                continue;
            }

            var level = packed.ToLevelUpdate(_hash.Date);
            _currentTsMs = level.Timestamp.ToUnixMillisecondsTimestamp();
            _currentEventTimeMs = _currentTsMs;

            if (level.IsSnapshot != _isSnapshot)
            {
                CompleteBufferAndApply();
                _isSnapshot = level.IsSnapshot;
            }

            if (!_isSnapshot && _currentTsMs >= _nextReportTimestamp)
            {
                CompleteBufferAndApply();
            }

            AddToBuffer(level);
        }

        CompleteBufferAndApply();
    }

    private void AddToBuffer(in LevelUpdate level)
    {
        if (_buffPtr == 0)
            _firstUpdateId = _nextUpdateId;

        _lastUpdateId = _nextUpdateId;
        _nextUpdateId++;

        if (_buffPtr == _bufferSize)
            GrowBuffer();

        _buffer[_buffPtr++] = level;
    }

    private void GrowBuffer()
    {
        var newSize = _bufferSize * 2;
        var newArr = ArrayPool<LevelUpdate>.Shared.Rent(newSize);
        Array.Copy(_buffer, 0, newArr, 0, _buffPtr);
        ArrayPool<LevelUpdate>.Shared.Return(_buffer, clearArray: false);
        _buffer = newArr;
        _bufferSize = newSize;
    }

    private void CompleteBufferAndApply()
    {
        if (_buffPtr == 0)
        {
            _nextReportTimestamp += _rateMs;
            return;
        }

        _currentPool ??= new L2UpdatePooled(_bufferSize);
        _currentPool.Clear();
        _currentPool.SetHeader(_hash.Symbol, _currentTsMs, _isSnapshot, _firstUpdateId, _lastUpdateId, _prevUpdateId);

        for (var i = 0; i < _buffPtr; i++)
        {
            var lvl = _buffer[i];
            // LevelUpdate -> L2Delta
            var d = new L2Delta(
                lvl.Side,
                decimal.ToDouble(lvl.Price),
                decimal.ToDouble(lvl.Quantity));

            _currentPool.AddDelta(in d);
        }


        _book.Apply(in _currentPool, force: false);
        _sink.QuoteBatchReceived(_currentEventTimeMs, in _currentPool, _book);

        _prevUpdateId = _lastUpdateId;

        _buffPtr = 0;
        _firstUpdateId = 0;

        _nextReportTimestamp += _rateMs;
    }

    public void Dispose()
    {
        _bookSub?.Dispose();
        _topSub?.Dispose();

        if (_currentPool is not null)
        {
            _currentPool.Dispose();
            _currentPool = null;
        }

        ArrayPool<LevelUpdate>.Shared.Return(_buffer, clearArray: false);
    }
}
