using MES.Services;
using Xunit;

namespace MES.Tests;

// 純函式測試,不連資料庫。reasonIds 用種子資料的 4 筆 Id(1~4)。
public class StatusTransitionTests
{
    private static readonly int[] ReasonIds = { 1, 2, 3, 4 };

    [Fact]
    public void 運轉狀態_roll落在轉停機區間_應轉為停機且帶原因()
    {
        var (status, reasonId) = StatusTransition.Next(StatusTransition.Running, 0.99, ReasonIds);

        Assert.Equal(StatusTransition.Down, status);
        Assert.NotNull(reasonId);
        Assert.Contains(reasonId!.Value, ReasonIds);
    }

    [Fact]
    public void 運轉狀態_roll落在維持區間_應維持運轉且原因為null()
    {
        var (status, reasonId) = StatusTransition.Next(StatusTransition.Running, 0.1, ReasonIds);

        Assert.Equal(StatusTransition.Running, status);
        Assert.Null(reasonId);
    }

    [Fact]
    public void 停機狀態_roll落在轉運轉區間_應轉為運轉且原因為null()
    {
        var (status, reasonId) = StatusTransition.Next(StatusTransition.Down, 0.99, ReasonIds);

        Assert.Equal(StatusTransition.Running, status);
        Assert.Null(reasonId);
    }

    [Fact]
    public void 停機狀態_roll落在維持區間_應維持停機且帶原因()
    {
        var (status, reasonId) = StatusTransition.Next(StatusTransition.Down, 0.1, ReasonIds);

        Assert.Equal(StatusTransition.Down, status);
        Assert.NotNull(reasonId);
        Assert.Contains(reasonId!.Value, ReasonIds);
    }

    [Fact]
    public void 運轉狀態_roll剛好等於0點7門檻_應轉為停機()
    {
        // 70% 維持運轉是 roll < 0.7,roll == 0.7 應落在轉停機那 30%
        var (status, _) = StatusTransition.Next(StatusTransition.Running, 0.7, ReasonIds);
        Assert.Equal(StatusTransition.Down, status);
    }

    [Fact]
    public void 運轉狀態_roll略小於0點7門檻_應維持運轉()
    {
        var (status, _) = StatusTransition.Next(StatusTransition.Running, 0.699999, ReasonIds);
        Assert.Equal(StatusTransition.Running, status);
    }

    [Fact]
    public void 停機狀態_roll剛好等於0點6門檻_應轉為運轉()
    {
        // 60% 維持停機是 roll < 0.6,roll == 0.6 應落在轉運轉那 40%
        var (status, _) = StatusTransition.Next(StatusTransition.Down, 0.6, ReasonIds);
        Assert.Equal(StatusTransition.Running, status);
    }

    [Fact]
    public void 停機狀態_roll略小於0點6門檻_應維持停機()
    {
        var (status, _) = StatusTransition.Next(StatusTransition.Down, 0.599999, ReasonIds);
        Assert.Equal(StatusTransition.Down, status);
    }
}
