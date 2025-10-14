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
    /// 3D 그리드의 인덱서 구현
    /// 오프셋을 적용한 위치에 대한 데이터 접근을 제공
    /// </summary>
    /// <param name="pos">접근하려는 3D 위치</param>
    /// <returns>해당 위치의 데이터</returns>
    /// <remarks>
    /// 동작 방식:
    /// 1. 입력받은 위치에 오프셋 적용
    /// 2. 1차원 배열 인덱스로 변환
    /// 3. 데이터 접근 또는 수정
    /// 
    /// 주의사항:
    /// - 범위를 벗어난 접근 시 예외 발생 가능
    /// - 올바른 오프셋 설정 필요
    /// </remarks>
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

    /*
     한글 주석 요약:
     - Grid3D는 3차원 공간을 1차원 배열로 표현하는 유틸리티입니다.
     - 내부적으로는 (x + width*y + width*height*z) 식으로 인덱스를 계산합니다.
     - Offset은 외부 좌표계와 내부 인덱스의 오프셋을 맞추기 위해 사용됩니다.
     - InBounds는 BoundsInt를 사용해 위치가 그리드 범위 내에 있는지 판단합니다.
    */
}
