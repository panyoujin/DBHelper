using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace DBHelper.KeyNavigation
{
    /// <summary>
    /// SQL key 的位置信息
    /// </summary>
    public sealed class SqlKeyLocation
    {
        public string FilePath { get; internal set; }
        public int Line { get; internal set; }      // 0 基行号
        public int Column { get; internal set; }    // 0 基列号（指向 name=" 之后）
    }

    /// <summary>
    /// SQL key 索引服务：递归扫描解决方案目录下的 *.xml，
    /// 提取 &lt;Data name="key"&gt; 定义并建立缓存；FileSystemWatcher 增量失效。
    /// 与 DBHelper 运行时 CacheSqlConfig 的大小写不敏感语义保持一致。
    /// </summary>
    [Export(typeof(SqlKeyIndexService))]
    internal sealed class SqlKeyIndexService : IDisposable
    {
        private static readonly Regex DataNameRegex = new Regex(
            @"<\s*Data\b[^>]*?name\s*=\s*""(?<key>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly object _gate = new object();
        private Dictionary<string, SqlKeyLocation> _keys =
            new Dictionary<string, SqlKeyLocation>(StringComparer.OrdinalIgnoreCase);
        private string _solutionDir;

        private FileSystemWatcher _watcher;
        private Timer _reloadTimer;
        private const int ReloadDebounceMs = 800;

        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 尝试获取 key 对应的 XML 配置位置。未找到返回 false。
        /// </summary>
        public bool TryGetLocation(string key, out SqlKeyLocation location)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                location = null;
                return false;
            }
            EnsureIndex();
            lock (_gate)
            {
                return _keys.TryGetValue(key.Trim(), out location);
            }
        }

        /// <summary>
        /// 首次查询或解决方案切换时重建索引
        /// </summary>
        private void EnsureIndex()
        {
            string solutionDir = GetSolutionDirectory();
            lock (_gate)
            {
                if (_solutionDir != null &&
                    string.Equals(_solutionDir, solutionDir, StringComparison.OrdinalIgnoreCase))
                {
                    return; // 已缓存且解决方案未变（文件变更由 watcher 触发重建）
                }
                _solutionDir = solutionDir;
                if (string.IsNullOrEmpty(solutionDir))
                {
                    _keys = new Dictionary<string, SqlKeyLocation>(StringComparer.OrdinalIgnoreCase);
                    DisposeWatcher();
                    return;
                }
                RebuildCore(solutionDir);
                StartWatcher(solutionDir);
            }
        }

        private void RebuildCore(string solutionDir)
        {
            var keys = new Dictionary<string, SqlKeyLocation>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var xml in Directory.EnumerateFiles(solutionDir, "*.xml", SearchOption.AllDirectories))
                {
                    // 跳过常见无关目录
                    var dirName = Path.GetDirectoryName(xml) ?? string.Empty;
                    if (dirName.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dirName.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dirName.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dirName.IndexOf("\\.vs\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    ScanFile(xml, keys);
                }
            }
            catch
            {
                // 扫描失败时保留部分结果
            }
            _keys = keys;
        }

        private void ScanFile(string path, Dictionary<string, SqlKeyLocation> keys)
        {
            try
            {
                string text;
                using (var reader = new StreamReader(path, true))
                {
                    text = reader.ReadToEnd();
                }
                foreach (Match match in DataNameRegex.Matches(text))
                {
                    var key = match.Groups["key"].Value;
                    // name= 之后引号内内容的位置即为跳转目标
                    int line = CountLinesBefore(text, match.Groups["key"].Index, out int lineStart);
                    var loc = new SqlKeyLocation
                    {
                        FilePath = Path.GetFullPath(path),
                        Line = line,
                        Column = match.Groups["key"].Index - lineStart
                    };
                    // 后出现的同名 key 覆盖先出现的（与 DBHelper 运行时按文件顺序覆盖的语义近似）
                    keys[key] = loc;
                }
            }
            catch
            {
                // 单个文件解析失败不中断整体扫描
            }
        }

        private static int CountLinesBefore(string text, int index, out int lineStart)
        {
            int line = 0;
            lineStart = 0;
            for (int i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    lineStart = i + 1;
                    if (i > 0 && text[i - 1] == '\r')
                    {
                        lineStart = i + 1; // \r\n 时行首统一为 \n 之后
                    }
                }
            }
            return line;
        }

        private string GetSolutionDirectory()
        {
            try
            {
                var solution = ServiceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
                if (solution != null &&
                    solution.GetSolutionInfo(out string dir, out string file, out string opts) == 0 &&
                    !string.IsNullOrEmpty(dir))
                {
                    return dir;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 监视解决方案目录下的 XML 变更，防抖后重建索引（与运行时热更新一致的体验）
        /// </summary>
        private void StartWatcher(string solutionDir)
        {
            DisposeWatcher();
            try
            {
                _watcher = new FileSystemWatcher(solutionDir, "*.xml")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnXmlChanged;
                _watcher.Created += OnXmlChanged;
                _watcher.Deleted += OnXmlChanged;
                _watcher.Renamed += OnXmlChanged;
            }
            catch
            {
                // 路径不支持监视时放弃热更新，仅保留静态索引
                _watcher = null;
            }
        }

        private void OnXmlChanged(object sender, FileSystemEventArgs e)
        {
            var timer = new Timer(_ =>
            {
                lock (_gate)
                {
                    if (_solutionDir == null) return;
                    RebuildCore(_solutionDir);
                }
            }, null, ReloadDebounceMs, Timeout.Infinite);
            var old = Interlocked.Exchange(ref _reloadTimer, timer);
            old?.Dispose();
        }

        private void DisposeWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            _reloadTimer?.Dispose();
            _reloadTimer = null;
        }

        public void Dispose()
        {
            DisposeWatcher();
        }
    }
}
