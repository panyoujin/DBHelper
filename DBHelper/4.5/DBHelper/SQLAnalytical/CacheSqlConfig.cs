using DBHelper.Helper;
using DBHelper.Models;
using DBHelper.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;

namespace DBHelper.SQLAnalytical
{
    public class CacheSqlConfig
    {
        private static readonly Lazy<CacheSqlConfig> _instance = new Lazy<CacheSqlConfig>(() => new CacheSqlConfig());
        //唯一的查询缓存：并发安全，请求读取与热更新写入可并行
        private static readonly ConcurrentDictionary<string, SqlAnalyModel> _sqlAnalyModelDic = new ConcurrentDictionary<string, SqlAnalyModel>();
        //文件 -> 该文件包含的 key 列表，用于按文件增量更新/删除；仅初始化与热更新时在锁内修改
        private static readonly Dictionary<string, List<string>> _fileKeysDic = new Dictionary<string, List<string>>();
        static object _SqlLock = new object();
        //热更新防抖：编辑器保存通常会连续触发多次变更事件，攒起来延迟统一处理
        static object _PendingLock = new object();
        static HashSet<string> _pendingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static Timer _reloadTimer;
        static FileSystemWatcher _watcher;
        //重载延迟（毫秒）：最后一次变更事件后静默该时长才执行重载
        const int ReloadDelayMilliseconds = 500;
        private string _sqlConfigPath;
        /// <summary>
        /// SQL配置的路径 将会检索该目录和子目录下的所有xml缓存起来
        /// </summary>
        public string SqlConfigPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_sqlConfigPath))
                {
                    _sqlConfigPath = AppDomain.CurrentDomain.BaseDirectory + "/bin/SqlConfig";
                }
                if (!Directory.Exists(_sqlConfigPath))
                {
                    _sqlConfigPath = AppDomain.CurrentDomain.BaseDirectory + "/SqlConfig";
                }
                if (!Directory.Exists(_sqlConfigPath))
                {
                    _sqlConfigPath = ConfigHelper.GetConfigValue("SqlConfigPath", "");
                    if (string.IsNullOrWhiteSpace(_sqlConfigPath) || !Directory.Exists(_sqlConfigPath))
                    {
                        _sqlConfigPath = AppDomain.CurrentDomain.BaseDirectory;
                    }
                }
                return _sqlConfigPath;
            }
            set
            {
                _sqlConfigPath = value;
            }
        }
        public static CacheSqlConfig Instance
        {
            get
            {
                return _instance.Value;
            }
        }
        private CacheSqlConfig()
        {
            SqlConfigInit();
            try
            {
                StartWatcher();
            }
            catch
            {
                //路径不支持文件监视（如部分网络共享）时降级为无热更新，不影响缓存加载
            }
        }
        /// <summary>
        /// 通过指定的KEY获取SQL，keyValue 会被用于解析 SQL 中的占位符
        /// </summary>
        public SqlAnalyModel GetSqlAnalyByKey(string key, Dictionary<string, object> keyValue)
        {
            SqlAnalyModel model = TryGetModel(key);
            if (model == null)
            {
                //未命中直接抛异常，禁止把传入字符串当作 SQL 执行
                throw new Exception(string.Format("配置中找不到KEY：{0}", key));
            }
            SqlDefinition sqlDefinition = new SqlDefinition(model);
            return sqlDefinition.SqlAnaly(keyValue);
        }
        /// <summary>
        /// 通过指定的KEY获取SQL，keyValue 列表取第一项作为参数，用于批量执行场景
        /// </summary>
        public SqlAnalyModel GetSqlAnalyByKey(string key, List<Dictionary<string, object>> keyValue)
        {
            SqlAnalyModel model = TryGetModel(key);
            if (model == null)
            {
                throw new Exception(string.Format("配置中找不到KEY：{0}", key));
            }
            if (model.SqlText.IndexOf("<R%=") >= 0)
            {
                throw new Exception("批量执行不能用在直接替换的场景");
            }
            SqlDefinition sqlDefinition = new SqlDefinition(model);
            return sqlDefinition.SqlAnaly(keyValue.FirstOrDefault());
        }
        /// <summary>
        /// 按KEY取模型；初始化由构造函数完成，key 缺失时返回 null（调用方抛异常），不做中途重载
        /// </summary>
        private static SqlAnalyModel TryGetModel(string key)
        {
            SqlAnalyModel model;
            //ConcurrentDictionary 支持与热更新写入并行的无锁读取
            _sqlAnalyModelDic.TryGetValue(key.ToLower(), out model);
            return model;
        }

        #region InitSql
        /// <summary>
        /// SQL配置加载：递归扫描 SqlConfigPath 下所有 xml，整体加载一次
        /// </summary>
        private void SqlConfigInit()
        {
            lock (_SqlLock)
            {
                GetFileToXml(SqlConfigPath);
            }
        }

        private void GetFileToXml(string path)
        {
            var dir = new DirectoryInfo(path);
            var files = dir.GetFiles();
            foreach (var file in files)
            {
                if (file.Extension.ToLower() == ".xml")
                {
                    GetXml(file);
                }
            }
            var childDir = dir.GetDirectories();
            foreach (var d in childDir)
            {
                GetFileToXml(d.FullName);
            }
            //全部文件加载完成后统一接父链
            RebuildParentLinks();
        }

        private static void GetXml(FileInfo file)
        {
            try
            {
                RegisterFile(file.FullName, ParseFile(file));
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("加载SQL配置文件[{0}]失败", file.FullName), ex);
            }
        }

        /// <summary>
        /// 解析单个配置文件，返回该文件包含的 name -> SqlAnalyModel
        /// </summary>
        private static Dictionary<string, SqlAnalyModel> ParseFile(FileInfo file)
        {
            var models = new Dictionary<string, SqlAnalyModel>();
            XmlDocument doc = new XmlDocument();
            doc.Load(file.FullName);
            foreach (XmlNode nodeChild in doc.DocumentElement.ChildNodes)
            {
                if (nodeChild.NodeType != XmlNodeType.Element || nodeChild.Name.ToLower() != "data") continue;
                if (nodeChild.Attributes["name"] == null) continue;
                var key = XmlUtility.getNodeAttributeStringValue(nodeChild, "name").ToLower();
                models[key] = SqlAnalyModel.XmlToSqlAnalyModel(nodeChild["SqlDefinition"]);
            }
            return models;
        }

        /// <summary>
        /// 登记文件与其 key 的对应关系，并按文件增量写入查询缓存：先移除该文件原有的 key，再写入新 key
        /// </summary>
        private static void RegisterFile(string filePath, Dictionary<string, SqlAnalyModel> models)
        {
            lock (_SqlLock)
            {
                List<string> oldKeys;
                if (_fileKeysDic.TryGetValue(filePath, out oldKeys))
                {
                    SqlAnalyModel removed;
                    foreach (var k in oldKeys)
                    {
                        _sqlAnalyModelDic.TryRemove(k, out removed);
                    }
                }
                foreach (var kv in models)
                {
                    _sqlAnalyModelDic[kv.Key] = kv.Value;
                }
                _fileKeysDic[filePath] = new List<string>(models.Keys);
                //文件更新后，父链关系可能变化，统一重接
                RebuildParentLinks();
            }
        }

        /// <summary>
        /// 重建 ParentAnaly 父链引用：遍历所有 model，ParentKey 存在于缓存中则指向当前父模型。
        /// 沿链做环检测，成环的配置不建立引用（避免请求期递归栈溢出）。
        /// 配置中 SQL 总量级很小，全量重建成本可忽略
        /// </summary>
        private static void RebuildParentLinks()
        {
            foreach (var kv in _sqlAnalyModelDic)
            {
                if (string.IsNullOrWhiteSpace(kv.Value.ParentKey)) continue;
                SqlAnalyModel parent;
                if (!_sqlAnalyModelDic.TryGetValue(kv.Value.ParentKey, out parent)) continue;
                //环检测：沿链收集 key，任何一步重复或回到自身即视为成环
                var visited = new HashSet<string> { kv.Key, kv.Value.ParentKey };
                var hasCycle = false;
                var cursor = parent;
                while (!string.IsNullOrWhiteSpace(cursor.ParentKey))
                {
                    if (!visited.Add(cursor.ParentKey))
                    {
                        hasCycle = true;
                        break;
                    }
                    SqlAnalyModel next;
                    if (!_sqlAnalyModelDic.TryGetValue(cursor.ParentKey, out next)) break;
                    cursor = next;
                }
                if (!hasCycle)
                {
                    kv.Value.ParentAnaly = parent;
                }
            }
        }

        /// <summary>
        /// 按文件增量移除缓存：只删除该文件包含的 key，不影响其他文件的缓存
        /// </summary>
        private static void RemoveFile(string filePath)
        {
            lock (_SqlLock)
            {
                List<string> oldKeys;
                if (_fileKeysDic.TryGetValue(filePath, out oldKeys))
                {
                    SqlAnalyModel removed;
                    foreach (var k in oldKeys)
                    {
                        _sqlAnalyModelDic.TryRemove(k, out removed);
                    }
                    _fileKeysDic.Remove(filePath);
                    RebuildParentLinks();
                }
            }
        }
        #endregion InitSql

        #region HotReload
        /// <summary>
        /// 启动文件监视：SqlConfig 目录（含子目录）下的 xml 发生变更时，按文件增量热更新缓存
        /// </summary>
        private void StartWatcher()
        {
            _watcher = new FileSystemWatcher(SqlConfigPath, "*.xml")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Created += OnWatcherCreated;
            _watcher.Changed += OnWatcherChanged;
            _watcher.Deleted += OnWatcherDeleted;
            _watcher.Renamed += OnWatcherRenamed;
            _watcher.EnableRaisingEvents = true;
        }

        private static void OnWatcherCreated(object sender, FileSystemEventArgs e)
        {
            ScheduleReload(e.FullPath);
        }

        private static void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            ScheduleReload(e.FullPath);
        }

        private static void OnWatcherDeleted(object sender, FileSystemEventArgs e)
        {
            RemoveFile(e.FullPath);
        }

        private static void OnWatcherRenamed(object sender, RenamedEventArgs e)
        {
            RemoveFile(e.OldFullPath);
            ScheduleReload(e.FullPath);
        }

        private static void ScheduleReload(string filePath)
        {
            lock (_PendingLock)
            {
                _pendingFiles.Add(filePath);
                if (_reloadTimer == null)
                {
                    _reloadTimer = new Timer(FlushPendingReloads, null, ReloadDelayMilliseconds, Timeout.Infinite);
                }
                else
                {
                    _reloadTimer.Change(ReloadDelayMilliseconds, Timeout.Infinite);
                }
            }
        }

        private static void FlushPendingReloads(object state)
        {
            string[] files;
            lock (_PendingLock)
            {
                files = new string[_pendingFiles.Count];
                _pendingFiles.CopyTo(files);
                _pendingFiles.Clear();
            }
            foreach (var path in files)
            {
                ReloadFile(path);
            }
        }

        /// <summary>
        /// 按文件增量重载：只更新该文件包含的 key，不影响其他文件的缓存。
        /// 文件被占用或写入不完整时静默跳过，等待下一次变更事件重试。
        /// </summary>
        private static void ReloadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                RegisterFile(filePath, ParseFile(new FileInfo(filePath)));
            }
            catch
            {
                //解析失败不做任何缓存改动，等待下一次变更事件重试
            }
        }
        #endregion HotReload
    }
}
