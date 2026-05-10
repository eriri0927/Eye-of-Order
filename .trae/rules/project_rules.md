# 项目规范

## Unity 序列化铁律

**所有 MonoBehaviour 中的 `public` 字段，必须在 `Awake()` 中强制赋值。**

原因：Unity 会将 `public` 字段序列化到场景文件中。当修改代码中的默认值后，场景中缓存的旧值会在运行时覆盖代码中的新值，导致修改无效。`Awake()` 在序列化反序列化之后执行，可以确保代码中的值始终生效。

```csharp
// ✅ 正确
public float dodgeSpeed = 6f;

void Awake()
{
    dodgeSpeed = 6f;  // 强制覆盖场景缓存
}

// ❌ 错误 - 不加 Awake，场景缓存值会覆盖代码默认值
public float dodgeSpeed = 6f;
```
