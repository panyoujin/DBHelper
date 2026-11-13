using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace DBHelper.KeyNavigation
{
    /// <summary>
    /// 监听 C# 编辑器视图创建，挂接 Ctrl+点击：点击 SQL KEY 字符串字面量时跳转到 XML 配置。
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class KeyJumpTextViewListener : IWpfTextViewCreationListener
    {
        [Import]
        internal SqlKeyIndexService IndexService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(
                typeof(KeyJumpController), () => new KeyJumpController(textView, IndexService));
        }
    }

    /// <summary>
    /// 单个视图的 Ctrl+Click 控制器（随视图销毁自动解除事件挂接）
    /// </summary>
    internal sealed class KeyJumpController : IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly SqlKeyIndexService _indexService;

        internal KeyJumpController(IWpfTextView textView, SqlKeyIndexService indexService)
        {
            _textView = textView;
            _indexService = indexService;
            // 用 Preview 事件抢在编辑器默认的 caret 定位之前拿到点击位置
            // IWpfTextView 本身不是 UIElement，WPF 事件挂在 VisualElement 上
            _textView.VisualElement.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
            _textView.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                return;
            }

            try
            {
                var bufferPosition = GetBufferPositionUnderMouse(e);
                if (bufferPosition == null)
                {
                    return;
                }

                var literal = GetEnclosedStringLiteral(bufferPosition.Value);
                if (literal == null)
                {
                    return;
                }

                if (_indexService.TryGetLocation(literal, out var location))
                {
                    e.Handled = true;
                    ThreadHelper.Generic.BeginInvoke(() =>
                        SqlKeyNavigator.OpenAt(_indexService.ServiceProvider, location));
                }
            }
            catch
            {
                // 扩展内部异常不影响编辑器正常交互
            }
        }

        /// <summary>
        /// 将鼠标坐标换算为缓冲区位置
        /// </summary>
        private int? GetBufferPositionUnderMouse(MouseButtonEventArgs e)
        {
            var point = e.GetPosition(_textView.VisualElement);
            var line = _textView.TextViewLines.GetTextViewLineContainingYCoordinate(point.Y + _textView.ViewportTop);
            if (line == null)
            {
                return null;
            }
            var bufferPosition = line.GetBufferPositionFromXCoordinate(point.X + _textView.ViewportLeft);
            return bufferPosition.HasValue ? (int?)bufferPosition.Value.Position : null;
        }

        /// <summary>
        /// 若点击位置处于字符串字面量内部（两侧被 " 包围），返回字面量内容
        /// </summary>
        private string GetEnclosedStringLiteral(int position)
        {
            var textLine = _textView.TextSnapshot.GetLineFromPosition(position);
            var text = textLine.GetText();
            var idx = position - textLine.Start.Position;
            if (idx < 0 || idx >= text.Length)
            {
                return null;
            }

            int open = -1;
            for (var i = Math.Min(idx, text.Length - 1); i >= 0; i--)
            {
                if (text[i] == '"')
                {
                    open = i;
                    break;
                }
            }
            if (open < 0)
            {
                return null;
            }

            int close = -1;
            for (var i = open + 1; i < text.Length; i++)
            {
                if (text[i] == '"')
                {
                    close = i;
                    break;
                }
            }
            if (close < 0 || idx > close)
            {
                // 点击位置不在该对引号之间（如字符串右侧的逗号），放行
                return null;
            }

            var literal = text.Substring(open + 1, close - open - 1).Trim();
            if (literal.Length == 0 || literal.Contains(" ") && literal.StartsWith("using"))
            {
                return null;
            }
            return literal;
        }

        public void Dispose()
        {
            _textView.VisualElement.PreviewMouseLeftButtonDown -= OnPreviewMouseDown;
            _textView.Closed -= OnViewClosed;
        }
    }
}
