using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3차원 그리드 시스템을 구현하는 제네릭 클래스
/// </summary>
public class Grid3D<T> {
    // 그리드 데이터를 저장하는 1차원 배열
    T[] data;

    // 그리드의 크기와 오프셋에 대한 프로퍼티
    public Vector3Int Size { get; private set; }     // 그리드의 3차원 크기
    public Vector3Int Offset { get; set; }          // 그리드의 위치 오프셋

    /// <summary>
    /// 3D 그리드 생성자
    /// </summary>
    /// <param name="size">그리드의 3차원 크기</param>
    /// <param name="offset">그리드의 시작 위치 오프셋</param>
    public Grid3D(Vector3Int size, Vector3Int offset) {
        Size = size;
        Offset = offset;
        // 3차원 크기를 1차원 배열로 변환하여 초기화
        data = new T[size.x * size.y * size.z];
    }

    /// <summary>
    /// 3차원 좌표를 1차원 인덱스로 변환
    /// </summary>
    public int GetIndex(Vector3Int pos) {
        return pos.x + (Size.x * pos.y) + (Size.x * Size.y * pos.z);
    }

    /// <summary>
    /// 주어진 위치가 그리드 범위 내에 있는지 확인
    /// </summary>
    public bool InBounds(Vector3Int pos) {
        return new BoundsInt(Vector3Int.zero, Size).Contains(pos + Offset);
    }

    /// <summary>
    /// x, y, z 좌표로 그리드의 셀에 접근하는 인덱서
    /// </summary>
    public T this[int x, int y, int z] {
        get {
            return this[new Vector3Int(x, y, z)];
        }
        set {
            this[new Vector3Int(x, y, z)] = value;
        }
    }

    /// <summary>
    /// Vector3Int를 사용한 그리드 접근 인덱서
    /// </summary>
    public T this[Vector3Int pos] {
        get {
            pos += Offset;
            return data[GetIndex(pos)];
        }
        set {
            pos += Offset;
            data[GetIndex(pos)] = value;
        }
    }
}
