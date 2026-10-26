namespace OmniToolbox.UI;

public static class OmniWindowGeometry
{
    public static Vector2 ClampPosition(
        Vector2 position, Vector2 size, Vector2 workPosition, Vector2 workSize, bool allowHalfOutside)
    {
        var minimum = allowHalfOutside ? workPosition - size * 0.5f : workPosition;
        var maximum = workPosition + workSize - (allowHalfOutside ? size * 0.5f : size);
        return Vector2.Clamp(position, minimum, Vector2.Max(minimum, maximum));
    }

    public static Vector2 GetBesidePosition(
        Vector2 sourcePosition, Vector2 sourceSize, Vector2 targetSize,
        Vector2 workPosition, Vector2 workSize, Vector2 offset, bool preferRight = false)
    {
        var left = sourcePosition.X - targetSize.X - offset.X;
        var right = sourcePosition.X + sourceSize.X + offset.X;
        var preferred = preferRight ? right : left;
        var alternative = preferRight ? left : right;
        var preferredVisibleWidth = GetVisibleWidth(preferred);
        if (preferredVisibleWidth * 2f < targetSize.X && GetVisibleWidth(alternative) > preferredVisibleWidth)
            preferred = alternative;
        return ClampPosition(new(preferred, sourcePosition.Y + offset.Y), targetSize, workPosition, workSize, true);

        float GetVisibleWidth(float x) => MathF.Max(0f,
            MathF.Min(x + targetSize.X, workPosition.X + workSize.X) - MathF.Max(x, workPosition.X));
    }
}
