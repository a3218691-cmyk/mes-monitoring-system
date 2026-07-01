namespace MES.Services;

// 純函式版的狀態轉換規則,不包 Random,方便 xUnit 傳固定 roll 值驗證邊界。
// 只做 運轉 ↔ 停機 兩態(種子資料沒有待機轉換的先例,不自行延伸)。
public static class StatusTransition
{
    public const string Running = "運轉";
    public const string Down = "停機";

    // 運轉:roll < 0.7 維持運轉(70%),否則轉停機(30%)
    // 停機:roll < 0.6 維持停機(60%),否則轉運轉(40%)
    public static (string NewStatus, int? ReasonId) Next(string currentStatus, double roll, IReadOnlyList<int> reasonIds)
    {
        string newStatus = currentStatus == Running
            ? (roll < 0.7 ? Running : Down)
            : (roll < 0.6 ? Down : Running);

        return newStatus == Down
            ? (newStatus, PickReason(roll, reasonIds))
            : (newStatus, null);
    }

    // 用 roll 取千分位再取模,讓原因分佈不會被上面的門檻切成只剩窄區間可選
    private static int PickReason(double roll, IReadOnlyList<int> reasonIds)
    {
        if (reasonIds.Count == 0) throw new ArgumentException("reasonIds 不可為空", nameof(reasonIds));
        int index = (int)(roll * 1000) % reasonIds.Count;
        return reasonIds[index];
    }
}
