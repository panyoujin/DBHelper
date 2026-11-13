using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace DBHelper.KeyNavigation
{
    /// <summary>
    /// 打开 XML 配置文件并定位到 key 所在行列
    /// </summary>
    internal static class SqlKeyNavigator
    {
        public static void OpenAt(SVsServiceProvider serviceProvider, SqlKeyLocation location)
        {
            try
            {
                VsShellUtilities.OpenDocument(
                    serviceProvider,
                    location.FilePath,
                    Guid.Empty,
                    out IVsUIHierarchy hierarchy,
                    out uint itemId,
                    out IVsWindowFrame frame,
                    out IVsTextView textView);

                frame?.Show();

                if (textView != null && textView.GetBuffer(out IVsTextLines lines) == 0 && lines != null)
                {
                    var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
                    var logicalView = Guid.Empty;
                    // NavigateToLineAndColumn 直接按行列定位（0 基）；NavigateToPosition 需要绝对偏移，不适用
                    IVsTextBuffer buffer = lines;
                    textManager?.NavigateToLineAndColumn(
                        buffer,
                        ref logicalView,
                        location.Line,
                        location.Column,
                        location.Line,
                        location.Column + 1);
                }
            }
            catch
            {
                // 打开失败（文件被删除等）静默返回
            }
        }
    }
}
