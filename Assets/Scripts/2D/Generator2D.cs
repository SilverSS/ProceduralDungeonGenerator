using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

/// <summary>
/// 절차적 던전 생성기 클래스
/// </summary>
public class Generator2D : MonoBehaviour {
    // 셀의 타입을 정의하는 열거형
    enum CellType {
        None,       // 빈 공간
        Room,       // 방
        Hallway     // 복도
    }

    // 방을 나타내는 클래스
    class Room {
        public RectInt bounds;  // 방의 경계를 나타내는 직사각형

        // 방 생성자: 위치와 크기를 받아 방의 경계를 설정
        public Room(Vector2Int location, Vector2Int size) {
            bounds = new RectInt(location, size);
        }

        // 두 방이 서로 겹치는지 확인하는 메서드
        // a, b: 겹침을 검사할 두 방
        // 반환값: 두 방이 겹치면 true, 겹치지 않으면 false
        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || 
                    ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x) ||
                    (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || 
                    ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    Vector2Int size;          // 던전의 전체 크기
    [SerializeField]
    int roomCount;            // 생성할 방의 개수
    [SerializeField]
    int tryMakeRoomCount;     // 방 생성 최대 시도 횟수
    [SerializeField]
    Vector2Int roomMinSize;   // 방의 최소 크기
    [SerializeField]
    Vector2Int roomMaxSize;   // 방의 최대 크기
    [SerializeField]
    GameObject cubePrefab;    // 큐브 프리팹
    [SerializeField]
    Material redMaterial;     // 방을 표시할 빨간색 재질
    [SerializeField]
    Material blueMaterial;    // 복도를 표시할 파란색 재질

    [Header("Visualization Options")]
    [SerializeField] private bool showMSTEdges = true;    // MST 간선 표시 여부
    [SerializeField] private bool showPathLines = true;    // 실제 통로 경로 표시 여부
    [SerializeField] private Color mstColor = Color.blue;  // MST 간선 색상
    [SerializeField] private Color pathColor = Color.yellow;  // 통로 경로 색상    

    Grid2D<CellType> grid;   // 2D 그리드
    List<Room> rooms;        // 생성된 방들의 목록
    private List<List<Vector2Int>> pathLines = new List<List<Vector2Int>>();
    private HashSet<Room> connectedRooms = new HashSet<Room>();
    HashSet<Prim.Edge> selectedEdges;  // 선택된 간선들

    Random random;            // 난수 생성기
    Delaunay2D delaunay;     // 들로네 삼각분할

    [ExecuteAlways]
    public void Generate() {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        Clear();
        
        random = new Random(System.DateTime.Now.Millisecond);
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();
        selectedEdges = new HashSet<Prim.Edge>();
        connectedRooms = new HashSet<Room>();
        pathLines = new List<List<Vector2Int>>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();

        stopwatch.Stop();

        if (Application.isPlaying == false)
        {
            string time = stopwatch.ElapsedMilliseconds / 1000.0f < 1 ? $"{stopwatch.ElapsedMilliseconds}ms" : $"{stopwatch.ElapsedMilliseconds / 1000.0f}s";
            Debug.Log($"던전 생성 완료: {time}");
        }
    }

    [ExecuteAlways]
    public void Clear() {
        while (transform.childCount > 0) {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        if (grid != null) grid = null;
        if (rooms != null) rooms.Clear();
        if (delaunay != null) delaunay = null;
        if (selectedEdges != null) selectedEdges.Clear();
        if (connectedRooms != null) connectedRooms.Clear();
        if (pathLines != null) pathLines.Clear();
    }

    // 던전을 생성하는 메인 메서드
    void Start() {
        Generate();
    }

    // 방 배치 메서드
    void PlaceRooms() {
        int createdRooms = 0;

        for (int i = 0; i < tryMakeRoomCount && createdRooms < roomCount; i++) {
            // 랜덤한 위치 생성
            Vector2Int location = new Vector2Int(
                random.Next(0, size.x),
                random.Next(0, size.y)
            );

            // 랜덤한 방 크기 생성
            Vector2Int roomSize = new Vector2Int(
                random.Next(roomMinSize.x, roomMaxSize.x + 1),
                random.Next(roomMinSize.y, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            // 방 주변에 1칸의 여유 공간을 둔 버퍼 영역 생성
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            // 다른 방들과의 겹침 검사
            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            // 던전 경계 검사
            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y) {
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

        if (createdRooms < roomCount) {
            Debug.LogWarning($"요청한 방 개수({roomCount})보다 적은 수의 방({createdRooms})이 생성되었습니다.");
        }
    }

    // 들로네 삼각분할 수행 메서드
    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        // 각 방의 중심점을 정점으로 변환
        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    // 복도 생성을 위한 간선 선택 메서드
    void CreateHallways() {
        connectedRooms = new HashSet<Room>();
        selectedEdges = new HashSet<Prim.Edge>();
        List<Prim.Edge> edges = new List<Prim.Edge>();

        // 들로네 삼각분할의 모든 간선을 프림 알고리즘용 간선으로 변환
        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        if (edges.Count > 0 && rooms.Count > 0) {
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

            // 연결되지 않은 방 처리
            var unconnectedRooms = new HashSet<Room>(rooms);
            unconnectedRooms.ExceptWith(connectedRooms);

            foreach (var room in unconnectedRooms) {
                Room nearestRoom = FindNearestConnectedRoom(room);
                if (nearestRoom != null) {
                    var edge = new Prim.Edge(
                        new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room),
                        new Vertex<Room>((Vector2)nearestRoom.bounds.position + ((Vector2)nearestRoom.bounds.size) / 2, nearestRoom)
                    );
                    selectedEdges.Add(edge);
                    connectedRooms.Add(room);
                }
            }
        }

        // 12.5% 확률로 추가 간선 선택 (순환 경로 생성)
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }
    }

    // 가장 가까운 연결된 방 찾기
    private Room FindNearestConnectedRoom(Room room) {
        Room nearest = null;
        float minDistance = float.MaxValue;
        
        foreach (var connectedRoom in connectedRooms) {
            float distance = Vector2.Distance(
                room.bounds.center,
                connectedRoom.bounds.center
            );
            
            if (distance < minDistance) {
                minDistance = distance;
                nearest = connectedRoom;
            }
        }
        
        return nearest;
    }

    // A* 알고리즘을 사용한 경로 탐색 메서드
    void PathfindHallways() {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges) {
            // 시작점과 도착점 설정
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            // 방의 중심점을 경로의 시작점과 끝점으로 사용
            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            // A* 경로 탐색
            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) => {
                var pathCost = new DungeonPathfinder2D.PathCost();
                
                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    // 휴리스틱 비용

                // 셀 타입에 따른 이동 비용 설정
                if (grid[b.Position] == CellType.Room) {
                    pathCost.cost += 10;    // 방을 통과하는 비용
                } else if (grid[b.Position] == CellType.None) {
                    pathCost.cost += 5;     // 빈 공간을 통과하는 비용
                } else if (grid[b.Position] == CellType.Hallway) {
                    pathCost.cost += 1;     // 기존 복도를 통과하는 비용
                }

                pathCost.traversable = true;  // 모든 셀은 통과 가능

                return pathCost;
            });

            // 경로가 존재하는 경우 복도 생성
            if (path != null) {
                pathLines.Add(path);
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    // 빈 공간을 복도로 변경
                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;
                    }

                    // 이전 위치와의 방향 계산
                    if (i > 0) {
                        var prev = path[i - 1];
                        var delta = current - prev;
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

    // 3D 큐브를 생성하는 메서드
    void PlaceCube(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }

    // 방을 시각화하는 메서드
    void PlaceRoom(Vector2Int location, Vector2Int size) {
        PlaceCube(location, size, redMaterial);
    }

    // 복도를 시각화하는 메서드
    void PlaceHallway(Vector2Int location) {
        PlaceCube(location, Vector2Int.one, blueMaterial);
    }

    // 경로 시각화
    private void OnDrawGizmosSelected() {
        if (selectedEdges == null) return;

        if (showMSTEdges) {
            Gizmos.color = mstColor;
            foreach (var edge in selectedEdges) {
                if (edge?.U is Vertex<Room> u && edge?.V is Vertex<Room> v) {
                    Vector3 start = new Vector3(u.Item.bounds.center.x, 0, u.Item.bounds.center.y) + Vector3.one * 0.5f;
                    Vector3 end = new Vector3(v.Item.bounds.center.x, 0, v.Item.bounds.center.y) + Vector3.one * 0.5f;
                    Gizmos.DrawLine(start, end);

                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                }
            }
        }

        if (showPathLines && pathLines != null) {
            Gizmos.color = pathColor;
            foreach (var path in pathLines) {
                for (int i = 1; i < path.Count; i++) {
                    Vector3 start = new Vector3(path[i-1].x, 0, path[i-1].y) + Vector3.one * 0.5f;
                    Vector3 end = new Vector3(path[i].x, 0, path[i].y) + Vector3.one * 0.5f;
                    Gizmos.DrawLine(start, end);

                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                }
            }
        }
    }
}
