using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Room3D {

    public BoundsInt bounds;    // 방의 3차원 경계를 나타내는 직육면체
    public RoomProperties Properties { get; set; }  // 방의 속성
    public Dictionary<Vector3Int, CellProperties> CellProperties { get; set; }
    public GameObject VisualizationObject { get; set; }

    // 방 생성자: 위치와 크기를 받아 방의 경계를 설정
    public Room3D(Vector3Int location, Vector3Int size, RoomType type = RoomType.Normal) {
        bounds = new BoundsInt(location, size);
        Properties = new RoomProperties(type);
        CellProperties = new Dictionary<Vector3Int, CellProperties>();
        VisualizationObject = null;
    }

    // 두 방이 서로 겹치는지 확인하는 메서드
    public static bool Intersect(Room3D a, Room3D b) {
        return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || 
                ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x) ||
                (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || 
                ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y) ||
                (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || 
                ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
    }
}