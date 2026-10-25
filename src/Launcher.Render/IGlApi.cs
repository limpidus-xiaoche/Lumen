namespace Launcher.Render;

/// <summary>
/// GL 函数指针来源的抽象。App 层用 <c>Avalonia.OpenGL.Controls.OpenGlControlBase</c> 提供的
/// <c>GlInterface.GetProcAddress</c> 实现，本库因此不依赖 Avalonia，可在无 UI 环境下单测。
/// </summary>
public interface IGlApi
{
    nint GetProcAddress(string name);

    /// <summary>形如 "OpenGL ES 3.2 ANGLE ..."。</summary>
    string VersionString { get; }

    string RendererString { get; }
}
