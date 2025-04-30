using UnityEngine;
using System.Collections.Generic;

// 방향 정보를 나타내는 열거형
public enum PathDirection {
    None,       // 방향 없음
    XPlus,      // X축 양의 방향
    XMinus,     // X축 음의 방향
    YPlus,      // Y축 양의 방향
    YMinus,     // Y축 음의 방향
    ZPlus,      // Z축 양의 방향
    ZMinus      // Z축 음의 방향
}


public enum OuterCellType {
    None,           // 외곽셀이 아님
    Ceiling,        // 천정
    Floor,          // 바닥
    Wall,           // 벽
    FloorWall,      // 바닥-벽
    CeilingWall,    // 천정-벽
    FloorCorner,    // 바닥-벽 모서리
    CeilingCorner,  // 천정-벽 모서리
    WallEntrance,   // 벽 출입구
    CornerEntrance  // 모서리 출입구
}

public class CellProperties {
    public Vector3Int Position { get; set; }
    public List<PathDirection> OuterDirections { get; set; } = new List<PathDirection>();
    public List<PathDirection> EntranceDirections { get; set; } = new List<PathDirection>();
    public bool IsEntrance { get; set; }
    public bool IsInnerCell { get; set; }
    public OuterCellType OuterType { get; set; }

    public CellProperties(Vector3Int position) {
        Position = position;
        IsInnerCell = true;
        IsEntrance = false;
        OuterType = OuterCellType.None;
    }
} 