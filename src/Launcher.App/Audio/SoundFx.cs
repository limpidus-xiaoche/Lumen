using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace Launcher.App.Audio;

/// <summary>
/// 启动器 UI 音效：直接用 winmm 播放内存里的 WAV，不引入额外音频依赖。
/// 同一时刻只发声一个音效，新的点击会打断上一个，正好符合导航交互的预期。
/// </summary>
public static class SoundFx
{
    public enum Kind
    {
        Enderman,
        ShulkerOpen,
        ShulkerShoot,
        Book,
        Eye,
        Portal,
        Chime,
    }

    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);

    private static readonly Dictionary<Kind, GCHandle> Buffers = [];
    private static readonly object Gate = new();

    public static void Play(Kind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var pointer = GetPointer(kind);
        if (pointer != IntPtr.Zero)
        {
            PlaySound(pointer, IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
        }
    }

    private static IntPtr GetPointer(Kind kind)
    {
        lock (Gate)
        {
            if (Buffers.TryGetValue(kind, out var cached))
            {
                return cached.AddrOfPinnedObject();
            }

            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://Lumen/Assets/sounds/{FileName(kind)}.wav"));
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var handle = GCHandle.Alloc(buffer.ToArray(), GCHandleType.Pinned);
                Buffers[kind] = handle;
                return handle.AddrOfPinnedObject();
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    private static string FileName(Kind kind) => kind switch
    {
        Kind.Enderman => "enderman",
        Kind.ShulkerOpen => "shulker-open",
        Kind.ShulkerShoot => "shulker-shoot",
        Kind.Book => "book",
        Kind.Eye => "eye",
        Kind.Portal => "portal",
        _ => "chime",
    };
}
