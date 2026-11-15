# Контракт Tool

Инструмент в коде — интерфейс `ITool`. Dispatcher зависит только от него и от `ToolRegistry`. Конкретные `RoutingTool` и `EscalationTool` Dispatcher не знает.

```csharp
public interface ITool
{
    string Name { get; }
    ToolResult Execute(ToolRequest request);
}

public sealed class ToolRequest
{
    public required Guid JobId { get; init; }
    public required string Operation { get; init; }
    public required IReadOnlyDictionary<string, object?> Params { get; init; }
}

public sealed class ToolResult
{
    public required bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Field { get; init; }
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
}
```

Кто проверяет параметры:

- HTTP API проверяет форму: есть ли поле, верный ли тип, не длиннее ли лимит, входит ли `operation` в список. Ошибка здесь — `400 invalid_params` или `400 unknown_operation`. До Dispatcher запрос не доходит, Job не меняется.
- Tool проверяет домен: существует ли исполнитель, активен ли он, допустим ли переход, заполнен ли смысл операции. Ошибка Tool — `422 invalid_params` или `409 invalid_transition` / `assignment_conflict`. Состояние меняется только при `Success = true`.
- Dispatcher инструмент не выбирает по содержимому параметров и свои проверки к ним не добавляет.

`transition` и `change_priority` выполняет сценарий инцидента по правилам Job, не через `ITool`. Через контракт Tool в первой версии проходят два инструмента ниже.

## RoutingTool

- Имя: `assign`.
- Получает: `JobId` и `assignee_id`.
- Возвращает при успехе: `assignee_id` и следующее состояние `IN_PROGRESS`, если Job был `OPEN`. Из `ESCALATED` успешное назначение возвращает `IN_PROGRESS`.
- Отказывает: `invalid_params` с полем `assignee_id`, если исполнителя нет или он неактивен; `assignment_conflict`, если назначение уже сохранено другим запросом; `invalid_transition`, если текущее состояние не допускает назначение.
- Соседний код не видит правила компетенции и нагрузки.

## EscalationTool

- Имя: `escalate`.
- Получает: `JobId` и `reason` (`reaction_sla_breached` или `resolution_sla_breached`).
- Возвращает при успехе: идентификатор эскалации и состояние `ESCALATED`. Если эта причина у этого Job уже записана, успех возвращает прежнюю эскалацию и признак повтора. Вторую запись и второе уведомление Tool не создаёт.
- Отказывает: `invalid_transition`, если из текущего состояния переход в `ESCALATED` запрещён; `invalid_params` с полем `reason`, если причина вне списка.
- После успешного сохранения вызывается адаптер уведомлений. Timeout, ограниченный retry и мусорный ответ становятся ошибкой доставки в истории. Состояние `ESCALATED` из-за этой ошибки не откатывается.

Общий вид отказа Tool для HTTP-слоя:

```json
{"error": {"code": "invalid_params", "message": "Assignee is inactive", "details": {"field": "assignee_id"}}}
```
