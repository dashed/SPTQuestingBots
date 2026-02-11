using System.Runtime.CompilerServices;
using UnityEngine;

namespace SPTQuestingBots.Helpers;

public class PositionHistory
{
    private readonly int _bufferSize;
    private readonly Vector3[] _positions;
    private int _writeIndex;
    private int _validCount;

    public PositionHistory(int segments)
    {
        // Add one extra item as we want to cover bufferSize + 1 segments.
        // E.g. to measure 10 segments, we need 11 samples.
        _bufferSize = segments + 1;
        _positions = new Vector3[_bufferSize];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(Vector3 currentPosition)
    {
        _positions[_writeIndex] = currentPosition;
        _writeIndex = (_writeIndex + 1) % _bufferSize;

        if (_validCount < _bufferSize)
        {
            _validCount++;
        }
    }

    public float GetDistanceSqr()
    {
        if (_validCount < 2)
        {
            return 0f;
        }

        // Most recent sample is one position behind write index
        var mostRecentIndex = (_writeIndex - 1 + _bufferSize) % _bufferSize;
        var mostRecentPosition = _positions[mostRecentIndex];

        // Oldest sample calculation
        var oldestIndex = _validCount < _bufferSize ? 0 : _writeIndex;
        var oldestPosition = _positions[oldestIndex];

        // Observed distance over actual time window
        var observedDistSqr = (mostRecentPosition - oldestPosition).sqrMagnitude;

        if (_validCount >= _bufferSize)
        {
            return observedDistSqr;
        }

        // During warmup: project velocity to full buffer duration
        var scaleFactor = (_bufferSize - 1f) / (_validCount - 1);
        return observedDistSqr * scaleFactor * scaleFactor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _writeIndex = 0;
        _validCount = 0;
    }
}
