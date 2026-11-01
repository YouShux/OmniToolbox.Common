using System.Threading;
using OmenTools.Interop.Game.Helpers;

namespace OmniToolbox.Game;

public sealed unsafe class DeepDungeonSaveLease : IDisposable
{
    private static int Held;
    private static int SuppressedAddonID;
    private bool disposed;

    public static bool IsHeld
    {
        get
        {
            if (Volatile.Read(ref Held) != 0) return true;
            var id = Volatile.Read(ref SuppressedAddonID);
            if (id == 0) return false;
            var addon = AddonHelper.GetByName("DeepDungeonSaveData");
            if (addon != null && addon->Id == id && addon->IsVisible) return true;
            Interlocked.CompareExchange(ref SuppressedAddonID, 0, id);
            return false;
        }
    }

    private DeepDungeonSaveLease() { }

    public static DeepDungeonSaveLease? TryAcquire() =>
        !IsHeld && Interlocked.CompareExchange(ref Held, 1, 0) == 0 ? new() : null;

    public static void SuppressUntilClosed(ushort addonID)
    {
        if (addonID != 0) Interlocked.Exchange(ref SuppressedAddonID, addonID);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Interlocked.Exchange(ref Held, 0);
    }
}
