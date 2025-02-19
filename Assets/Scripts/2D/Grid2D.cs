using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 그리드 시스템을 구현하는 제너릭 클래스
/// </summary>
public class Grid2D<T> {
    private T[] cells;               // 그리드의 셀들을 저장하는 배열
    private Vector2Int size;         // 그리드의 크기
    private Vector2Int offset;       // 그리드의 오프셋 (시작 위치)

    // 그리드의 크기와 오프셋에 대한 읽기 전용 속성
    public Vector2Int Size { get { return size; } }
    public Vector2Int Offset { get { return offset; } }

    /// <summary>
    /// 그리드 생성자
    /// </summary>
    /// <param name="size">그리드의 크기</param>
    /// <param name="offset">그리드의 시작 위치 오프셋</param>
    public Grid2D(Vector2Int size, Vector2Int offset) {
        this.size = size;
        this.offset = offset;
        cells = new T[size.x * size.y];  // 1차원 배열로 2D 그리드 구현
    }

    /// <summary>
    /// x, y 좌표로 그리드의 셀에 접근하는 인덱서
    /// </summary>
    public T this[int x, int y] {
        get {
            if (InBounds(x, y)) {
                return cells[x + y * size.x];  // 2D -> 1D 인덱스 변환
            }
            throw new IndexOutOfRangeException("그리드 범위를 벗어났습니다.");
        }
        set {
            if (InBounds(x, y)) {
                cells[x + y * size.x] = value;
                return;
            }
            throw new IndexOutOfRangeException("그리드 범위를 벗어났습니다.");
        }
    }

    /// <summary>
    /// Vector2Int를 사용한 그리드 접근 인덱서
    /// </summary>
    public T this[Vector2Int pos] {
        get { return this[pos.x, pos.y]; }
        set { this[pos.x, pos.y] = value; }
    }

    /// <summary>
    /// 주어진 좌표가 그리드 범위 내에 있는지 확인
    /// </summary>
    public bool InBounds(int x, int y) {
        return x >= 0 && x < size.x && y >= 0 && y < size.y;
    }

    /// <summary>
    /// Vector2Int 좌표가 그리드 범위 내에 있는지 확인
    /// </summary>
    public bool InBounds(Vector2Int pos) {
        return InBounds(pos.x, pos.y);
    }
}
