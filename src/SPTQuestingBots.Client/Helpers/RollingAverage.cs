using System.Runtime.CompilerServices;

namespace SPTQuestingBots.Helpers;

public class RollingAverage(int windowSize, int recalcInterval = 1000)
{
    private readonly float[] _buffer = new float[windowSize];
    private int _writeIndex;
    private float _sum;
    private int _count;
    private int _updatesSinceRecalc;

    public float Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count > 0 ? _sum / _count : 0f;
    }

    public void Update(float value)
    {
        // Only subtract evicted value if buffer is full
        if (_count >= _buffer.Length)
            _sum -= _buffer[_writeIndex];

        // Add new value
        _buffer[_writeIndex] = value;
        _sum += value;

        // Advance circular index
        _writeIndex = (_writeIndex + 1) % _buffer.Length;

        // Track actual count until buffer fills
        if (_count < _buffer.Length)
            _count++;

        // Periodically recalculate to prevent drift
        if (_updatesSinceRecalc < recalcInterval)
        {
            _updatesSinceRecalc++;
            return;
        }

        Recalculate();
        _updatesSinceRecalc = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Recalculate()
    {
        _sum = 0f;
        for (var i = 0; i < _count; i++)
        {
            _sum += _buffer[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _writeIndex = 0;
        _sum = 0f;
        _count = 0;
        _updatesSinceRecalc = 0;
    }
}
