using System;
using System.Collections.Generic;

/// <summary>
/// 배치 단계의 "공식 장부"입니다. (방장 컴퓨터에서만 사용)
/// 누가 어느 칸에 어떤 기물을 놓았는지 적어 두고, 규칙에 맞는지 검사해요.
/// 유니티 오브젝트와 상관없는 순수 C# 이라서 따로 테스트하기도 쉽습니다.
/// </summary>
public sealed class SetupBoardModel
{
    public const int Size = 8;
    public const int ZoneRows = 2;   // 각자 자기 쪽 앞 2줄에만 놓을 수 있어요 (백: y 0~1, 흑: y 6~7)

    public enum PlaceResult { Ok, OutOfBoard, OutsideZone, Occupied, LimitReached }

    private struct Cell
    {
        public bool Filled;
        public pieceType Type;
        public PieceColor Color;
    }

    private readonly Cell[,] _cells = new Cell[Size, Size];
    private readonly Dictionary<pieceType, int>[] _counts =
    {
        new Dictionary<pieceType, int>(),   // 백
        new Dictionary<pieceType, int>()    // 흑
    };

    private static int Side(PieceColor color) => color == PieceColor.white ? 0 : 1;

    public static bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    public static bool IsInZone(PieceColor color, int x, int y)
    {
        if (!InBounds(x, y)) return false;
        return color == PieceColor.white ? y < ZoneRows : y >= Size - ZoneRows;
    }

    public int GetCount(PieceColor color, pieceType type)
    {
        return _counts[Side(color)].TryGetValue(type, out int n) ? n : 0;
    }

    /// <summary>기물 놓기를 시도합니다. 규칙에 맞을 때만 장부에 적고 Ok를 돌려줘요.</summary>
    public PlaceResult TryPlace(PieceColor color, pieceType type, int x, int y, Func<pieceType, int> getMaxCount)
    {
        if (!InBounds(x, y)) return PlaceResult.OutOfBoard;
        if (!IsInZone(color, x, y)) return PlaceResult.OutsideZone;
        if (_cells[x, y].Filled) return PlaceResult.Occupied;
        if (GetCount(color, type) >= getMaxCount(type)) return PlaceResult.LimitReached;

        _cells[x, y] = new Cell { Filled = true, Type = type, Color = color };
        _counts[Side(color)][type] = GetCount(color, type) + 1;
        return PlaceResult.Ok;
    }

    /// <summary>내 기물만 치울 수 있어요.</summary>
    public bool TryRemove(PieceColor color, int x, int y)
    {
        if (!InBounds(x, y)) return false;

        Cell cell = _cells[x, y];
        if (!cell.Filled || cell.Color != color) return false;

        _cells[x, y] = default;
        _counts[Side(color)][cell.Type] = GetCount(color, cell.Type) - 1;
        return true;
    }

    /// <summary>해당 색 기물을 전부 치웁니다. 치운 것이 하나라도 있으면 true.</summary>
    public bool ClearSide(PieceColor color)
    {
        bool removed = false;
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (_cells[x, y].Filled && _cells[x, y].Color == color)
                {
                    _cells[x, y] = default;
                    removed = true;
                }
            }
        }
        _counts[Side(color)].Clear();
        return removed;
    }

    public void Clear()
    {
        Array.Clear(_cells, 0, _cells.Length);
        _counts[0].Clear();
        _counts[1].Clear();
    }

    /// <summary>"준비 완료"를 눌러도 되는 상태인지 검사합니다. (킹이 정확히 1개 있어야 함)</summary>
    public bool CanBeReady(PieceColor color, out string reason)
    {
        if (GetCount(color, pieceType.King) != 1)
        {
            reason = "킹을 1개 배치해야 준비할 수 있어요.";
            return false;
        }

        reason = null;
        return true;
    }

    public static string Describe(PlaceResult result)
    {
        switch (result)
        {
            case PlaceResult.OutOfBoard: return "판 밖이에요.";
            case PlaceResult.OutsideZone: return "내 진영 앞 2줄에만 놓을 수 있어요.";
            case PlaceResult.Occupied: return "이미 기물이 있는 칸이에요.";
            case PlaceResult.LimitReached: return "이 기물은 더 이상 놓을 수 없어요.";
            default: return null;
        }
    }
}