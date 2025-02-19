using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System;
using UnityEngine.Rendering;

/// <summary>
/// 3D 절차적 던전 생성기 클래스
/// </summary>
public class Generator3D : MonoBehaviour {
    // 셀의 타입을 정의하는 열거형
    enum CellType {
        None,       // 빈 공간
        Room,       // 방
        Hallway,    // 복도
        Stairs      // 계단
    }

    // 3D 방을 나타내는 클래스
    class Room {
        public BoundsInt bounds;    // 방의 3차원 경계를 나타내는 직육면체

        // 방 생성자: 위치와 크기를 받아 방의 경계를 설정
        public Room(Vector3Int location, Vector3Int size) {
            bounds = new BoundsInt(location, size);
        }

        // 두 방이 서로 겹치는지 확인하는 메서드
        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || 
                    ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x) ||
                    (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || 
                    ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y) ||
                    (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || 
                    ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }

    [SerializeField]
    Vector3Int size;            // 던전의 3차원 크기
    [SerializeField]
    int roomCount;              // 생성할 방의 개수
    [SerializeField]
    int tryMakeRoomCount;       // 방 생성 시도 횟수
    [SerializeField]
    Vector3Int roomMinSize;     // 방의 최소 3차원 크기
    [SerializeField]
    Vector3Int roomMaxSize;     // 방의 최대 3차원 크기
    [SerializeField]
    GameObject cubePrefab;      // 큐브 프리팹
    [SerializeField]
    Material roomMaterial;       // 방을 표시할 빨간색 재질
    [SerializeField]
    Material hallwayMaterial;      // 복도를 표시할 파란색 재질
    [SerializeField]
    Material stairMaterial;     // 계단을 표시할 초록색 재질

    [SerializeField] [HideInInspector]
    Grid3D<CellType> grid;      // 3D 그리드
    [SerializeField] [HideInInspector] 
    List<Room> rooms;           // 생성된 방들의 목록
    
    [SerializeField] [HideInInspector] 
    HashSet<Prim.Edge> selectedEdges;                                           // 선택된 간선들
    [SerializeField] [HideInInspector] 
    private List<List<Vector3Int>> pathLines = new List<List<Vector3Int>>();    // 그리드 단위 경로
    [SerializeField] [HideInInspector] 
    HashSet<Room> connectedRooms;                                               // 연결되지 않은 방을 추적하기 위한 연결된 방들의 집합

    
    Random random;            // 난수 생성기
    Delaunay3D delaunay;     // 3D 들로네 삼각분할

    [Header("Visualization Options")]
    [SerializeField] private bool showMSTEdges = true;    // MST 간선 표시 여부
    [SerializeField] private bool showPathLines = true;    // 실제 통로 경로 표시 여부
    [SerializeField] private Color mstColor = Color.blue;  // MST 간선 색상
    [SerializeField] private Color pathColor = Color.yellow;  // 통로 경로 색상

    // 게임 시작 시 호출되는 메서드
    void Start() {
        
        Generate();
    }

    [ExecuteAlways]
    public void Generate() {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        Clear();  // 기존 던전 데이터 초기화

        random = new Random(System.DateTime.Now.Millisecond);  // 현재 시간 기반 랜덤 시드
        grid = new Grid3D<CellType>(size, Vector3Int.zero);   // 3D 그리드 초기화
        rooms = new List<Room>();                             // 방 목록 초기화

        PlaceRooms();        // 방 배치
        Triangulate();       // 삼각분할
        CreateHallways();    // 복도 생성
        PathfindHallways();  // 경로 탐색 및 계단 배치

        stopwatch.Stop();

        if (Application.isPlaying == false)
            Debug.Log($"던전 생성 완료: {stopwatch.ElapsedMilliseconds / 1000.0f}ms");
    }

    [ExecuteAlways]
    public void Clear() {
        // 모든 자식 오브젝트 제거
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        // 기존 데이터 초기화
        if (grid != null) grid = null;
        if (rooms != null) rooms.Clear();
        if (delaunay != null) delaunay = null;
        if (selectedEdges != null) selectedEdges.Clear();
        if (connectedRooms != null) connectedRooms.Clear();
        if (pathLines != null) pathLines.Clear();
    }

    // 3D 방 배치 메서드
    void PlaceRooms() {
        int createdRooms = 0;  // 실제 생성된 방의 개수

        for (int i = 0; i < tryMakeRoomCount && createdRooms < roomCount; i++) {
            // 랜덤한 위치 생성
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                random.Next(0, size.y),
                random.Next(0, size.z)
            );

            // 랜덤한 방 크기 생성
            Vector3Int roomSize = new Vector3Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1),
                random.Next(roomMinSize.z, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            // 방 주변에 버퍼 영역 생성 (xy평면에서만)
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), 
                                 roomSize + new Vector3Int(2, 0, 2));

            // 다른 방들과의 겹침 검사
            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            // 던전 경계 검사
            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z) {
                add = false;
            }

            // 방 추가가 가능한 경우
            if (add) {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                // 그리드에 방 영역 표시
                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }

                createdRooms++;
            }
        }

        // 원하는 수만큼 방을 생성하지 못한 경우 경고
        if (createdRooms < roomCount) {
            Debug.LogWarning($"요청한 방 개수({roomCount})보다 적은 수의 방({createdRooms})이 생성되었습니다.");
        }
    }

    /// <summary>
    /// 3D 들로네 삼각분할 수행 메서드
    /// </summary>
    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        // 각 방의 중심점을 정점으로 변환
        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }

        // 3D 들로네 삼각분할 수행
        delaunay = Delaunay3D.Triangulate(vertices);
    }

    /// <summary>
    /// 복도 생성을 위한 간선 선택 메서드
    /// </summary>
    void CreateHallways() {
        connectedRooms = new HashSet<Room>();
        selectedEdges = new HashSet<Prim.Edge>();
        List<Prim.Edge> edges = new List<Prim.Edge>();

        if (delaunay?.Edges == null || rooms == null || rooms.Count == 0) {
            Debug.LogError("들로네 삼각분할이 수행되지 않았거나 방이 없습니다.");
            return;
        }

        try {
            // 모든 방 쌍 사이의 간선 생성
            for (int i = 0; i < rooms.Count; i++) {
                for (int j = i + 1; j < rooms.Count; j++) {
                    if (rooms[i] != null && rooms[j] != null) {
                        var edge = new Prim.Edge(
                            new Vertex<Room>((Vector3)rooms[i].bounds.position + ((Vector3)rooms[i].bounds.size) / 2, rooms[i]),
                            new Vertex<Room>((Vector3)rooms[j].bounds.position + ((Vector3)rooms[j].bounds.size) / 2, rooms[j])
                        );
                        edges.Add(edge);
                    }
                }
            }

            // 첫 번째 방을 시작으로 최소 신장 트리 생성
            var mst = Prim.MinimumSpanningTree(edges, edges[0].U);
            
            if (mst != null) {
                foreach (var edge in mst) {
                    if (edge?.U is Vertex<Room> u && edge?.V is Vertex<Room> v) {
                        selectedEdges.Add(edge);
                        connectedRooms.Add(u.Item);
                        connectedRooms.Add(v.Item);
                    }
                }
            }

            // 연결되지 않은 방이 있는지 확인
            var unconnectedRooms = new HashSet<Room>(rooms);
            unconnectedRooms.ExceptWith(connectedRooms);

            if (unconnectedRooms.Count > 0) {
                Debug.LogWarning($"연결되지 않은 방 발견: {unconnectedRooms.Count}개");
                
                // 연결되지 않은 각 방에 대해
                foreach (var room in unconnectedRooms) {
                    // 이미 연결된 방들 중 가장 가까운 방 찾기
                    Room nearestRoom = null;
                    float minDistance = float.MaxValue;

                    foreach (var connectedRoom in connectedRooms) {
                        float distance = Vector3.Distance(
                            room.bounds.center,
                            connectedRoom.bounds.center
                        );
                        
                        if (distance < minDistance) {
                            minDistance = distance;
                            nearestRoom = connectedRoom;
                        }
                    }

                    if (nearestRoom != null) {
                        // 강제로 간선 추가
                        var edge = new Prim.Edge(
                            new Vertex<Room>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room),
                            new Vertex<Room>((Vector3)nearestRoom.bounds.position + ((Vector3)nearestRoom.bounds.size) / 2, nearestRoom)
                        );
                        selectedEdges.Add(edge);
                        connectedRooms.Add(room);
                    }
                }

                // 최종 확인
                unconnectedRooms = new HashSet<Room>(rooms);
                unconnectedRooms.ExceptWith(connectedRooms);
                
                if (unconnectedRooms.Count > 0) {
                    Debug.LogError($"여전히 연결되지 않은 방이 있습니다: {unconnectedRooms.Count}개");
                    foreach (var room in unconnectedRooms) {
                        Debug.LogError($"연결되지 않은 방 위치: {room.bounds.position}");
                    }
                }
            }

            // 모든 방이 연결된 후 순환 경로 추가
            if (edges.Count > 0) {
                // 12.5% 확률로 추가 간선 선택 (순환 경로 생성)
                var remainingEdges = new HashSet<Prim.Edge>(edges);
                remainingEdges.ExceptWith(selectedEdges);

                foreach (var edge in remainingEdges) {
                    if (random.NextDouble() < 0.125) {
                        selectedEdges.Add(edge);
                    }
                }
            }
        }
        catch (Exception e) {
            Debug.LogError($"복도 생성 중 치명적 오류 발생: {e.Message}");
        }
    }

    // A* 경로 탐색과 복도/계단 생성 메서드
    void PathfindHallways() {
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            // 각 방의 가장 낮은 높이 찾기
            var startLowestY = startRoom.bounds.yMin;
            var endLowestY = endRoom.bounds.yMin;

            // 시작점 설정 (x,z는 중심, y는 최저점)
            var startPos = new Vector3Int(
                (int)startRoom.bounds.center.x,
                startLowestY,
                (int)startRoom.bounds.center.z
            );

            // 도착점 설정 (x,z는 중심, y는 최저점)
            var endPos = new Vector3Int(
                (int)endRoom.bounds.center.x,
                endLowestY,
                (int)endRoom.bounds.center.z
            );

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost();
                var delta = b.Position - a.Position;

                if (delta.y == 0) {
                    // 평평한 복도의 경우
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    // 목표까지의 거리를 휴리스틱으로 사용

                    // 셀 타입에 따른 이동 비용 설정
                    if (grid[b.Position] == CellType.Stairs) {
                        return pathCost;  // 계단은 통과 불가
                    } else if (grid[b.Position] == CellType.Room) {
                        pathCost.cost += 5;  // 방을 통과하는 비용
                    } else if (grid[b.Position] == CellType.None) {
                        pathCost.cost += 1;  // 빈 공간을 통과하는 비용
                    }

                    pathCost.traversable = true;
                } else {
                    // 계단 설치의 경우
                    // 시작점과 도착점이 비어있거나 복도인 경우만 계단 설치 가능
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) 
                        return pathCost;

                    // 계단 설치 비용 계산
                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    // 기본 비용 + 휴리스틱

                    // 계단의 방향 벡터 계산
                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);    // 수직 이동량
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);   // 수평 이동량

                    // 계단 설치 공간이 그리드 범위 내인지 확인
                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset)) {
                        return pathCost;
                    }

                    // 계단 설치 공간이 비어있는지 확인
                    if (grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None) {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            // 경로가 존재하는 경우 복도와 계단 생성
            if (path != null) {
                // 경로 저장
                pathLines.Add(new List<Vector3Int>(path));

                // 각 경로 지점에 대해 처리
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    // 빈 공간을 복도로 변경
                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;
                    }

                    // 이전 위치와 현재 위치 사이에 계단 설치가 필요한지 확인
                    if (i > 0) {
                        var prev = path[i - 1];
                        var delta = current - prev;

                        // 높이 차이가 있는 경우 계단 설치
                        if (delta.y != 0) {
                            int xDir = Mathf.Clamp(delta.x, -1, 1);
                            int zDir = Mathf.Clamp(delta.z, -1, 1);
                            Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                            Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);
                            
                            // 계단 영역 설정 및 시각화
                            grid[prev + horizontalOffset] = CellType.Stairs;
                            grid[prev + horizontalOffset * 2] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset * 2] = CellType.Stairs;

                            PlaceStairs(prev + horizontalOffset);
                            PlaceStairs(prev + horizontalOffset * 2);
                            PlaceStairs(prev + verticalOffset + horizontalOffset);
                            PlaceStairs(prev + verticalOffset + horizontalOffset * 2);
                        }
                    }
                }

                // 복도 시각화
                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    // 큐브 오브젝트를 생성하여 배치하는 메서드
    void PlaceCube(Vector3Int location, Vector3Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<Transform>().localScale = size;
        go.GetComponent<MeshRenderer>().material = material;
    }

    // 방을 시각화하는 메서드 (빨간색)
    void PlaceRoom(Vector3Int location, Vector3Int size) {
        PlaceCube(location, size, roomMaterial);
    }

    // 복도를 시각화하는 메서드 (파란색)
    void PlaceHallway(Vector3Int location) {
        PlaceCube(location, Vector3Int.one, hallwayMaterial);
    }

    // 계단을 시각화하는 메서드 (초록색)
    void PlaceStairs(Vector3Int location) {
        PlaceCube(location, Vector3Int.one, stairMaterial);
    }

    private void OnDrawGizmosSelected() {
        if (selectedEdges == null) {
            return;
        }

        // MST 간선 시각화
        if (showMSTEdges) {
            Gizmos.color = mstColor;
            foreach (var edge in selectedEdges) {
                if (edge?.U is Vertex<Room> u && edge?.V is Vertex<Room> v) {
                    var startRoom = u.Item;
                    var endRoom = v.Item;

                    // 방의 중심점 계산 (0.5f 오프셋 추가)
                    Vector3 start = startRoom.bounds.center + Vector3.one * 0.5f;
                    Vector3 end = endRoom.bounds.center + Vector3.one * 0.5f;

                    // 연결선 그리기
                    Gizmos.DrawLine(start, end);
                    
                    // 방향 표시를 위한 구체 추가 (선택사항)
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                }
            }
        }

        // 실제 통로 경로 시각화
        if (showPathLines && pathLines != null) {
        Gizmos.color = pathColor;
            foreach (var path in pathLines) {
                for (int i = 1; i < path.Count; i++) {
                    Vector3 start = path[i-1] + Vector3.one * 0.5f;
                    Vector3 end = path[i] + Vector3.one * 0.5f;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawSphere(start, 0.1f);
                }
            }
        }
    }
}
