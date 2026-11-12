# `<R%= %R>` 原始替换占位符字符白名单说明

## 1. 适用范围

本说明只约束 **`<R%= @@key@@ %R>` 原始替换占位符**的传入值——这类值会被**直接拼进 SQL 文本**（通常用于表名、字段名等标识符），因此必须经过字符白名单校验，防止 SQL 注入。

以下场景**不受影响**，传任意值（含中文、特殊字符）都安全：

- `@@key@@` 裸参数标记
- `<%= @@key@@ %>` 可空参数块

这两类走参数化绑定（`?key` / `@key` + `DbParameter`），值不会拼进 SQL 文本。

## 2. 默认白名单规则

校验正则：`^[A-Za-z0-9_,\s\.]+$`（定义于 `SQLAnalytical/SqlSecurityOptions.cs`，4.5 与 NetCore 版一致）。

**允许的字符（仅此 6 类）：**

| 类别 | 字符 |
|---|---|
| 字母 | `A-Z` `a-z` |
| 数字 | `0-9` |
| 下划线 | `_` |
| 逗号 | `,` |
| 点号 | `.` |
| 空白字符 | 空格、制表符、换行等（`\s`） |

## 3. 禁止的字符（部分列举）

除上表外的一切字符都会被拒绝并抛出异常，常见如下：

| 类别 | 禁止字符 | 风险说明 |
|---|---|---|
| 语句分隔 | `;` | 可拼接第二条语句（如 `DROP TABLE`） |
| 注释符 | `--` `/*` `*/` | 截断后续 SQL，注入后抹痕迹 |
| 引号 | `'` `"` `` ` `` | 单双引号可逃逸字符串上下文；反引号可闭合标识符 |
| 括号 | `(` `)` | 可包裹子查询、函数调用 |
| 运算符 | `=` `<` `>` `+` `-` `*` `/` `%` | 可构造恒真条件或篡改表达式 |
| 逻辑符 | `&` `|` `!` `^` | 位/逻辑运算拼接 |
| 通配与元字符 | `*` `?` `[` `]` `{` `}` `\` | `*` 可替换全部列；`\` 有转义歧义 |
| 其他符号 | `@` `#` `$` `~` `:` `!` 等 | 变量引用、系统符号等 |
| 非 ASCII | 所有中文、全角字符 | 默认策略下不允许（见第 5 节放行方式） |

**典型被拒示例：**

`` `user` ``（反引号）、`user;select 1`（分号）、`user--备注`（注释符）、`order_华东`（中文）、`a*b`（星号）。

## 4. 校验失败的报错

```
InvalidOperationException: Raw SQL 参数 <key名> 含有非法字符，
请改用 SqlLiteral 包装或调整 RawSqlResolver。
```

## 5. 需要放行时的两种方式

### 方式一：SqlLiteral 显式声明可信片段（推荐，用于个别固定值）

```csharp
// 值完全由代码/配置写死、不含用户输入时
dic["tableName"] = SqlLiteral.From("`user_2026`");
```

### 方式二：全局替换 RawSqlResolver（用于统一放宽策略）

```csharp
// 程序启动时设置一次，例如放行中文标识符
SqlSecurityOptions.RawSqlResolver = (key, value) =>
{
    if (value == null) return string.Empty;
    var raw = value.ToString();
    if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
    // 在默认白名单基础上追加中文字符
    if (!Regex.IsMatch(raw, @"^[A-Za-z0-9_,\s\.\u4e00-\u9fa5]+$"))
        throw new InvalidOperationException("Raw SQL 参数 " + key + " 含有非法字符。");
    return raw;
};
```

> 注意：无论策略如何放宽，`<R%= %R>` 的值都**必须来自代码常量或服务端可信配置**，严禁直接透传用户输入。

## 6. 为什么默认策略这么严格

以被拦截的值 `user; DROP TABLE user; --` 为例，若不做校验，拼接后的 SQL 为：

```sql
SELECT * FROM user; DROP TABLE user; -- WHERE `status`=?status
```

数据库将额外执行 `DROP TABLE`，而 `--` 把合法查询条件注释掉，整个语句被攻击者劫持。白名单（只允许标识符字符）从机制上杜绝了这类拼接。

## 7. 版本一致性

| 版本 | 位置 | 默认策略 |
|---|---|---|
| .NET Framework 4.5 | `4.5/DBHelper/SQLAnalytical/SqlSecurityOptions.cs` | 相同白名单 + `SqlLiteral` + `RawSqlResolver` |
| .NET Core | `NetCore/CoreDBHelper/SQLAnalytical/SqlSecurityOptions.cs` | 相同白名单 + `SqlLiteral` + `RawSqlResolver` |

两版行为保持一致，放宽策略时请两版同步修改。
