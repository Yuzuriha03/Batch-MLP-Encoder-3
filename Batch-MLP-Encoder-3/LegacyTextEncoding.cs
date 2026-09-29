using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SadPencil.BatchMLPEncoder3 {

    /// <summary>
    /// 统一的“传统 ANSI 文本”编码入口。
    ///
    /// 背景：在 .NET Framework 中 Encoding.Default 是系统 ANSI 代码页（简体中文系统为 CP936），
    /// 而在 .NET Core / .NET 5+ 中 Encoding.Default 变成了 UTF-8。
    /// Surcode MLP Encoder 是 2003 年的程序，只认系统 ANSI 编码的 .ssf 文件，
    /// 所以这里必须显式取系统 ANSI 代码页，才能保持与迁移前完全一致的行为。
    ///
    /// 说明：这里刻意使用 Win32 的 GetACP()，而不是 CultureInfo.CurrentCulture.TextInfo.ANSICodePage。
    /// 因为程序允许用户在启动时切换界面语言（LanguageForm），CurrentCulture 并不等于
    /// Windows 实际使用的 ANSI 代码页，而 Surcode 读文件时用的是后者。
    /// </summary>
    internal static class LegacyTextEncoding {

        [DllImport("kernel32.dll")]
        private static extern uint GetACP();

        private static readonly Encoding AnsiEncoding = CreateAnsiEncoding();

        /// <summary>
        /// 系统 ANSI 代码页的编码器。无法表示的字符会被替换成 '?'，
        /// 与 .NET Framework 里 Encoding.Default 的行为一致。
        /// 用于写 .ssf 文件、读 eac3to 日志等“必须保持旧行为”的场合。
        /// </summary>
        internal static Encoding Ansi {
            get { return AnsiEncoding; }
        }

        /// <summary>
        /// 系统 ANSI 代码页的编号（简体中文系统通常是 936）。
        /// </summary>
        internal static int AnsiCodePage {
            get { return AnsiEncoding.CodePage; }
        }

        private static Encoding CreateAnsiEncoding() {
            //GB2312(CP936)、CP949 等传统代码页在 .NET Core 之后不再内置，需要显式注册。
            //重复注册是无害的，Program.Main 里也会注册一次。
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(checked((int)GetACP()));
        }

        /// <summary>
        /// 判断字符串能否用系统 ANSI 代码页无损表示。
        /// 只要有一个字符无法表示就返回 false（此时 Surcode 会读到 '?'，找不到文件，
        /// 调用方需要把文件名换成纯 ASCII 的临时名）。
        /// </summary>
        internal static bool CanBeAnsiEncoded(string Value) {
            if (string.IsNullOrEmpty(Value)) return true;
            try {
                Encoding.GetEncoding(AnsiEncoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetBytes(Value);
                return true;
            }
            catch (EncoderFallbackException) {
                return false;
            }
        }
    }
}
