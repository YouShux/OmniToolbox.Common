namespace OmniToolbox.UI;

public interface IOmniRecordWindowSettings
{
    bool Locked { get; }

    bool Collapsed { get; set; }

    Vector2 Position { get; set; }

    Vector2 CollapsedPosition { get; set; }

    Vector2 Size { get; set; }

    float GlobalScale { get; }

    float Opacity { get; }
}
