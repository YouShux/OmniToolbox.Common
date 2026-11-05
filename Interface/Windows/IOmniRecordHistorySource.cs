namespace OmniToolbox.UI;

public interface IOmniRecordHistorySource<T>
{
    OmniRecordHistoryMenu.Labels Labels { get; }

    long Revision => 0;

    float HeaderWidth => 0f;

    bool IsCurrentSelected { get; }

    bool CanTransfer { get; }

    string GetID(T item);

    string GetLabel(T item);

    bool IsSelected(T item);

    void SelectCurrent();

    void Select(T item);

    void Import();

    void Export(T item);

    void DrawHeader() { }

    void DrawFooter() { }
}
