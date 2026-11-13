# DBHelper

基于 Dapper 的轻量级数据库访问层，SQL 全部外置到 XML 配置文件，通过 KEY + 参数字典的方式执行，支持配置热更新，业务代码零 SQL 文本。

## 特性

- **SQL 与代码分离**：SQL 集中写在 `SqlConfig/*.xml`，便于 DBA 审核与统一管理
- **四库支持**：MySQL / MariaDB / SQL Server / PostgreSQL，通过 XML 中 `type` 一处配置切换
- **动态 SQL**：`@@key@@` 参数化占位符、`<%= %>` 动态条件块（参数缺失整块删除）、`<R%= %R>` 标识符直接替换（带防注入白名单）
- **配置热更新**：基于 FileSystemWatcher 按文件增量刷新，改 SQL 无需重启应用
- **事务支持**：`ExecuteInTransaction` 委托式事务封装（自动提交/回滚），外部事务重载，多 SQL key 同事务执行
- **批量执行、多结果集（最多 5 个）、分页、瞬态错误自动重试**

## 数据库驱动（全部 MIT/Apache 协议，无 GPL 依赖）

| 数据库 | XML type 取值 | 驱动 |
|---|---|---|
| MySQL | `mysql` | MySqlConnector 2.4.0 |
| MariaDB | `mariadb` | MySqlConnector 2.4.0 |
| SQL Server | `sqlserver` | Microsoft.Data.SqlClient 5.2.2 |
| PostgreSQL | `postgresql` / `postgres` / `pgsql` | Npgsql 8.0.5 |

## 快速开始

### 1. XML 中定义 SQL

```xml
<?xml version="1.0" encoding="utf-8" ?>
<DBSql>
  <Data name="GetUserList" type="mysql" ConnStringName="test_mysql">
    <SqlDefinition>
      SELECT * FROM `user` WHERE (1=1)
      <%= AND status = @@status@@ %>
      LIMIT @@start@@, @@size@@
    </SqlDefinition>
  </Data>
</DBSql>
```

- `<%= AND ... %>`：参数字典中未传 `status` 时，该条件块整体删除（动态 WHERE）
- `@@key@@`：参数化占位符，值由 Dapper 绑定，天然防注入

### 2. 连接串（appsettings.json / App.config 的 ConnectionStrings 节）

```xml
<add name="test_mysql" connectionString="Server=127.0.0.1;Database=test;Uid=root;Pwd=***;" providerName="MySqlConnector.MySqlClient" />
```

### 3. 调用

```csharp
var dic = new Dictionary<string, object> { ["status"] = 1, ["start"] = 0, ["size"] = 10 };
List<dynamic> list = SQLHelperFactory.Instance.QueryForList("GetUserList", dic);
```

## 事务

```csharp
// 委托式封装：正常返回自动提交，抛异常自动回滚
SQLHelperFactory.Instance.ExecuteInTransaction(connStr, "mysql", (conn, trans) =>
{
    SQLHelperFactory.Instance.ExecuteNonQuery(conn, trans, "InsertOrder", orderDic);
    SQLHelperFactory.Instance.ExecuteNonQuery(conn, trans, "DeductStock", stockDic);
});
```

## 适用框架

| 包 | 目标框架 |
|---|---|
| DBHelper 2.0.0（本包） | .NET 8.0 |
| DBHelper（Framework 版，源码随仓库分发） | .NET Framework 4.8 |

## 相关文档

- 仓库：<https://github.com/panyoujin/DBHelper>
- `docs/例子.md`：IN / NOT IN / LIKE / 分页 / 批量 / 多结果集 / 事务 等常见场景写法
- `docs/Raw替换占位符白名单说明.md`：`<R%= %R>` 直接替换的安全策略

## License

MIT
