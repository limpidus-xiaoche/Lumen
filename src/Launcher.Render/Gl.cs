using System.Runtime.InteropServices;

namespace Launcher.Render;

public sealed class GlLoadException(string function) : Exception($"GL 函数不可用：{function}");

/// <summary>
/// 函数指针表。只声明 S1 需要的子集，全部通过 <see cref="IGlApi.GetProcAddress"/> 解析，
/// 因此不引入第二套 GL 加载器（也不依赖 Avalonia）。
/// 注：指针参数一律用 <c>nint*</c>；GL 侧按 4 字节写入，调用方必须传 <c>uint[]</c>/<c>int[]</c> 的地址。
/// </summary>
internal unsafe sealed class Gl
{
    public delegate* unmanaged<float, float, float, float, void> ClearColor;
    public delegate* unmanaged<uint, void> Clear;
    public delegate* unmanaged<uint, void> Enable;
    public delegate* unmanaged<uint, void> Disable;
    public delegate* unmanaged<uint, uint, void> BlendFunc;
    public delegate* unmanaged<uint, void> DepthFunc;
    public delegate* unmanaged<int, int, int, int, void> Viewport;
    public delegate* unmanaged<uint, uint> CreateShader;
    public delegate* unmanaged<uint, int, nint*, nint*, void> ShaderSource;
    public delegate* unmanaged<uint, void> CompileShader;
    public delegate* unmanaged<uint, uint, nint*, void> GetShaderiv;
    public delegate* unmanaged<uint, int, nint*, byte*, void> GetShaderInfoLog;
    public delegate* unmanaged<uint, void> DeleteShader;
    public delegate* unmanaged<uint> CreateProgram;
    public delegate* unmanaged<uint, uint, void> AttachShader;
    public delegate* unmanaged<uint, void> LinkProgram;
    public delegate* unmanaged<uint, uint, nint*, void> GetProgramiv;
    public delegate* unmanaged<uint, int, nint*, byte*, void> GetProgramInfoLog;
    public delegate* unmanaged<uint, void> UseProgram;
    public delegate* unmanaged<uint, void> DeleteProgram;
    public delegate* unmanaged<int, nint*, void> GenBuffers;
    public delegate* unmanaged<uint, uint, void> BindBuffer;
    public delegate* unmanaged<uint, nint, nint, uint, void> BufferData;
    public delegate* unmanaged<int, nint*, void> DeleteBuffers;
    public delegate* unmanaged<int, nint*, void> GenVertexArrays;
    public delegate* unmanaged<uint, void> BindVertexArray;
    public delegate* unmanaged<int, nint*, void> DeleteVertexArrays;
    public delegate* unmanaged<uint, int, uint, byte, int, nint, void> VertexAttribPointer;
    public delegate* unmanaged<uint, void> EnableVertexAttribArray;
    public delegate* unmanaged<uint, int, int, void> DrawArrays;
    public delegate* unmanaged<uint, nint, int> GetUniformLocation;
    public delegate* unmanaged<int, int, byte, float*, void> UniformMatrix4fv;
    public delegate* unmanaged<int, float, void> Uniform1f;
    public delegate* unmanaged<int, float, float, float, void> Uniform3f;
    public delegate* unmanaged<int, int, void> Uniform1i;
    public delegate* unmanaged<uint> GetError;
    public delegate* unmanaged<uint, nint> GetString;
    public delegate* unmanaged<int, nint*, void> GenTextures;
    public delegate* unmanaged<uint, uint, void> BindTexture;
    public delegate* unmanaged<uint, uint, int, int, int, int, uint, uint, void*, void> TexImage2D;
    public delegate* unmanaged<uint, uint, int, void> TexParameteri;
    public delegate* unmanaged<uint, void> ActiveTexture;
    public delegate* unmanaged<int, nint*, void> DeleteTextures;
    public delegate* unmanaged<uint, int, void> PixelStorei;

    public static Gl Load(IGlApi api)
    {
        var gl = new Gl();

        nint Req(string name)
        {
            var ptr = api.GetProcAddress(name);
            return ptr == 0 ? throw new GlLoadException(name) : ptr;
        }

        gl.ClearColor = (delegate* unmanaged<float, float, float, float, void>)Req("glClearColor");
        gl.Clear = (delegate* unmanaged<uint, void>)Req("glClear");
        gl.Enable = (delegate* unmanaged<uint, void>)Req("glEnable");
        gl.Disable = (delegate* unmanaged<uint, void>)Req("glDisable");
        gl.BlendFunc = (delegate* unmanaged<uint, uint, void>)Req("glBlendFunc");
        gl.DepthFunc = (delegate* unmanaged<uint, void>)Req("glDepthFunc");
        gl.Viewport = (delegate* unmanaged<int, int, int, int, void>)Req("glViewport");
        gl.CreateShader = (delegate* unmanaged<uint, uint>)Req("glCreateShader");
        gl.ShaderSource = (delegate* unmanaged<uint, int, nint*, nint*, void>)Req("glShaderSource");
        gl.CompileShader = (delegate* unmanaged<uint, void>)Req("glCompileShader");
        gl.GetShaderiv = (delegate* unmanaged<uint, uint, nint*, void>)Req("glGetShaderiv");
        gl.GetShaderInfoLog = (delegate* unmanaged<uint, int, nint*, byte*, void>)Req("glGetShaderInfoLog");
        gl.DeleteShader = (delegate* unmanaged<uint, void>)Req("glDeleteShader");
        gl.CreateProgram = (delegate* unmanaged<uint>)Req("glCreateProgram");
        gl.AttachShader = (delegate* unmanaged<uint, uint, void>)Req("glAttachShader");
        gl.LinkProgram = (delegate* unmanaged<uint, void>)Req("glLinkProgram");
        gl.GetProgramiv = (delegate* unmanaged<uint, uint, nint*, void>)Req("glGetProgramiv");
        gl.GetProgramInfoLog = (delegate* unmanaged<uint, int, nint*, byte*, void>)Req("glGetProgramInfoLog");
        gl.UseProgram = (delegate* unmanaged<uint, void>)Req("glUseProgram");
        gl.DeleteProgram = (delegate* unmanaged<uint, void>)Req("glDeleteProgram");
        gl.GenBuffers = (delegate* unmanaged<int, nint*, void>)Req("glGenBuffers");
        gl.BindBuffer = (delegate* unmanaged<uint, uint, void>)Req("glBindBuffer");
        gl.BufferData = (delegate* unmanaged<uint, nint, nint, uint, void>)Req("glBufferData");
        gl.DeleteBuffers = (delegate* unmanaged<int, nint*, void>)Req("glDeleteBuffers");
        gl.GenVertexArrays = (delegate* unmanaged<int, nint*, void>)Req("glGenVertexArrays");
        gl.BindVertexArray = (delegate* unmanaged<uint, void>)Req("glBindVertexArray");
        gl.DeleteVertexArrays = (delegate* unmanaged<int, nint*, void>)Req("glDeleteVertexArrays");
        gl.VertexAttribPointer = (delegate* unmanaged<uint, int, uint, byte, int, nint, void>)Req("glVertexAttribPointer");
        gl.EnableVertexAttribArray = (delegate* unmanaged<uint, void>)Req("glEnableVertexAttribArray");
        gl.DrawArrays = (delegate* unmanaged<uint, int, int, void>)Req("glDrawArrays");
        gl.GetUniformLocation = (delegate* unmanaged<uint, nint, int>)Req("glGetUniformLocation");
        gl.UniformMatrix4fv = (delegate* unmanaged<int, int, byte, float*, void>)Req("glUniformMatrix4fv");
        gl.Uniform1f = (delegate* unmanaged<int, float, void>)Req("glUniform1f");
        gl.Uniform3f = (delegate* unmanaged<int, float, float, float, void>)Req("glUniform3f");
        gl.Uniform1i = (delegate* unmanaged<int, int, void>)Req("glUniform1i");
        gl.GetError = (delegate* unmanaged<uint>)Req("glGetError");
        gl.GetString = (delegate* unmanaged<uint, nint>)Req("glGetString");
        gl.GenTextures = (delegate* unmanaged<int, nint*, void>)Req("glGenTextures");
        gl.BindTexture = (delegate* unmanaged<uint, uint, void>)Req("glBindTexture");
        gl.TexImage2D = (delegate* unmanaged<uint, uint, int, int, int, int, uint, uint, void*, void>)Req("glTexImage2D");
        gl.TexParameteri = (delegate* unmanaged<uint, uint, int, void>)Req("glTexParameteri");
        gl.ActiveTexture = (delegate* unmanaged<uint, void>)Req("glActiveTexture");
        gl.DeleteTextures = (delegate* unmanaged<int, nint*, void>)Req("glDeleteTextures");
        gl.PixelStorei = (delegate* unmanaged<uint, int, void>)Req("glPixelStorei");
        return gl;
    }

    public int Uniform(uint program, string name)
    {
        nint ptr = 0;
        try
        {
            ptr = Marshal.StringToHGlobalAnsi(name);
            return GetUniformLocation(program, ptr);
        }
        finally
        {
            if (ptr != 0)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
