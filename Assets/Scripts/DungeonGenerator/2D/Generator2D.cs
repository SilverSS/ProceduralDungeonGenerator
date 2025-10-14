using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System.Linq;

/// <summary>
/// 절차적 던전 생성기 클래스
/// </summary>
[ExecuteInEditMode]
public class Generator2D : MonoBehaviour, ISerializationCallbackReceiver {
    /*
     추가 설명 (한글 주석)
     이 클래스는 2D 그리드 기반의 절차적 던전 생성기를 구현합니다.
     주요 단계:
     1) 무작위로 방(Room)을 배치한다 (PlaceRooms)
     2) 각 방의 중심을 정점으로 하여 들로네 삼각분할을 수행한다 (Triangulate)
     3) 삼각분할에서 추출한 간선을 바탕으로 최소 신장 트리(MST)를 생성하고, 일부 간선을 추가하여 순환 경로를 만든다 (CreateHallways)
     4) 선택된 간선들 각각에 대해 A* 기반의 경로탐색을 수행해 실제 통로(복도)를 만든다 (PathfindHallways)
     5) 노드(분기점) 정보를 직렬화하고 방 속성을 설정해 시각화한다 (SetRoomProperties)

     주의/설계 노트:
     - 그리드는 Grid2D<CellType>로 관리되며, 각 셀은 Room/ Hallway/ None 중 하나로 표시된다.
     - 복도 경로는 A*의 비용함수를 통해 기존 복도 우대, 방 통과 비용 등으로 제어된다.
     - 노드(Node) 구조체는 경로 상의 분기점을 표현하며, 직렬화 가능한 형태로 저장된다.
    */
    // 셀의 타입을 정의하는 열거형
    enum CellType {
        None,       // 빈 공간
        Room,       // 방
        Hallway     // 복도
    }

    // 방을 나타내는 클래스
    class Room2D {
        public RectInt bounds;  // 방의 경계를 나타내는 직사각형
        public RoomProperties Properties { get; set; }  // 방의 속성

        // 방 생성자: 위치와 크기를 받아 방의 경계를 설정
        public Room2D(Vector2Int location, Vector2Int size, RoomType type = RoomType.Normal) {
            bounds = new RectInt(location, size);
            Properties = new RoomProperties(type);
        }

        // 두 방이 서로 겹치는지 확인하는 메서드
        // a, b: 겹침을 검사할 두 방
        // 반환값: 두 방이 겹치면 true, 겹치지 않으면 false
        public static bool Intersect(Room2D a, Room2D b) {
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
    Material normalRoomMaterial;     // 일반 방을 표시할 빨간색 재질
    [SerializeField]
    Material startRoomMaterial;     // 시작 방을 표시할 빨간색 재질
    [SerializeField]
    Material endRoomMaterial;     // 종료 방을 표시할 빨간색 재질
    [SerializeField]
    Material merchantRoomMaterial;     // 상인 방을 표시할 빨간색 재질

    [SerializeField]
    Material hallwayMaterial;    // 복도를 표시할 파란색 재질

    private GameObject roomsParent;  // 방들을 그룹화할 부모 오브젝트
    private GameObject pathsParent;  // 경로들을 그룹화할 부모 오브젝트

    [Header("Random Generation Options")]
    [SerializeField] private bool useRandomSeed = true;  // 랜덤 시드 사용 여부
    [SerializeField] private int seed = 0;               // 고정 시드 값
    [SerializeField] [HideInInspector] private int savedSeed = 0;  // 저장된 시드 값

    [Header("Visualization Options")]
    [SerializeField] private bool showMSTEdges = true;    // MST 간선 표시 여부
    [SerializeField] private bool showPathLines = true;    // 실제 통로 경로 표시 여부
    [SerializeField] private Color mstColor = Color.blue;  // MST 간선 색상
    [SerializeField] private Color pathColor = Color.yellow;  // 통로 경로 색상    

    [Header("Room Properties")]
    [SerializeField] private DungeonGenerationOptions generationOptions = new DungeonGenerationOptions();

    private Room2D startRoom;
    private Room2D exitRoom;
    private Room2D merchantRoom;

    Grid2D<CellType> grid;   // 2D 그리드
    List<Room2D> rooms;        // 생성된 방들의 목록
    [SerializeField] [HideInInspector]
    private List<List<Vector2Int>> pathLines = new List<List<Vector2Int>>();
    [SerializeField] [HideInInspector]
    private HashSet<Room2D> connectedRooms = new HashSet<Room2D>();
    [SerializeField] [HideInInspector]
    private HashSet<Prim.Edge> selectedEdges = new HashSet<Prim.Edge>();

    Random random;            // 난수 생성기
    Delaunay2D delaunay;     // 들로네 삼각분할
    
    // 공유 통로 처리를 위한 노드 클래스
    [System.Serializable]
    public class Node {
        public Vector2Int Position { get; set; }
        [SerializeField]
        private List<PathDirection> openDirections = new List<PathDirection>();
        public List<PathDirection> OpenDirections {
            get => openDirections;
            set => openDirections = value;
        }
        public int PathIndex { get; set; }

        public Node(Vector2Int position) {
            Position = position;
            OpenDirections = new List<PathDirection>();
            PathIndex = -1;
        }

        // 직렬화를 위한 기본 생성자
        public Node() {
            Position = Vector2Int.zero;
            OpenDirections = new List<PathDirection>();
            PathIndex = -1;
        }
    }

    // 방향 정보를 나타내는 열거형
    [System.Serializable]
    public enum PathDirection {
        None,       // 방향 없음
        XPlus,      // X축 양의 방향
        XMinus,     // X축 음의 방향
        YPlus,      // Y축 양의 방향
        YMinus      // Y축 음의 방향
    }
    
    // 두 위치 간의 방향을 계산하는 메서드
    private PathDirection GetDirection(Vector2Int from, Vector2Int to) {
        Vector2Int delta = to - from;
        
        if (delta.x > 0) return PathDirection.XPlus;
        if (delta.x < 0) return PathDirection.XMinus;
        if (delta.y > 0) return PathDirection.YPlus;
        if (delta.y < 0) return PathDirection.YMinus;
        
        return PathDirection.None;
    }

    // 반대 방향을 구하는 헬퍼 메서드
    private PathDirection GetOppositeDirection(PathDirection direction) {
        switch (direction) {
            case PathDirection.XPlus: return PathDirection.XMinus;
            case PathDirection.XMinus: return PathDirection.XPlus;
            case PathDirection.YPlus: return PathDirection.YMinus;
            case PathDirection.YMinus: return PathDirection.YPlus;
            default: return PathDirection.None;
        }
    }

    [SerializeField] [HideInInspector]
    private List<Node> serializedNodes = new List<Node>();  // 직렬화된 노드 목록

    [SerializeField] [HideInInspector]
    private Dictionary<Vector2Int, Node> nodes = new Dictionary<Vector2Int, Node>();  // 노드 정보를 저장할 딕셔너리

    [SerializeField] [HideInInspector]
    private Dictionary<Vector2Int, int> positionToPathIndex = new Dictionary<Vector2Int, int>();  // 위치별 경로 인덱스

    public void OnBeforeSerialize() {
        // 직렬화 전에 실행되는 코드
    }

    public void OnAfterDeserialize() {
        // 역직렬화 후에 실행되는 코드
    }

    private void Awake() {
        if (savedSeed != 0) {
            seed = savedSeed;
            Generate();
        }
    }

    void Start() {
        if (savedSeed != 0) {
            seed = savedSeed;
        }
        Generate();
    }

    [ExecuteAlways]
    public void Generate() {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        Clear();
        
        // 카테고리 부모 오브젝트 생성
        roomsParent = new GameObject("Rooms");
        roomsParent.transform.SetParent(transform);
        roomsParent.transform.position = Vector3.zero;

        pathsParent = new GameObject("Paths");
        pathsParent.transform.SetParent(transform);
        pathsParent.transform.position = Vector3.zero;
        
        // 시드 설정
        if (useRandomSeed) {
            seed = System.DateTime.Now.Millisecond;
        }
        random = new Random(seed);
        Debug.Log($"던전 생성 시드: {seed}");
        
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room2D>();
        selectedEdges = new HashSet<Prim.Edge>();
        connectedRooms = new HashSet<Room2D>();
        pathLines = new List<List<Vector2Int>>();
        nodes = new Dictionary<Vector2Int, Node>();
        positionToPathIndex = new Dictionary<Vector2Int, int>();
        serializedNodes = new List<Node>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();

        // 노드 정보 직렬화
        serializedNodes = new List<Node>(nodes.Values);

        SetRoomProperties();

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
        if (nodes != null) nodes.Clear();
        if (positionToPathIndex != null) positionToPathIndex.Clear();
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
            Room2D newRoom = new Room2D(location, roomSize);
            // 방 주변에 1칸의 여유 공간을 둔 버퍼 영역 생성
            Room2D buffer = new Room2D(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            // 다른 방들과의 겹침 검사
            foreach (var room in rooms) {
                if (Room2D.Intersect(room, buffer)) {
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
            vertices.Add(new Vertex<Room2D>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    // 복도 생성을 위한 간선 선택 메서드
    void CreateHallways() {
        connectedRooms = new HashSet<Room2D>();
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
                    if (edge?.U is Vertex<Room2D> u && edge?.V is Vertex<Room2D> v) {
                        selectedEdges.Add(edge);
                        connectedRooms.Add(u.Item);
                        connectedRooms.Add(v.Item);
                    }
                }
            }

            // 연결되지 않은 방 처리
            var unconnectedRooms = new HashSet<Room2D>(rooms);
            unconnectedRooms.ExceptWith(connectedRooms);

            foreach (var room in unconnectedRooms) {
                Room2D nearestRoom = FindNearestConnectedRoom(room);
                if (nearestRoom != null) {
                    var edge = new Prim.Edge(
                        new Vertex<Room2D>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room),
                        new Vertex<Room2D>((Vector2)nearestRoom.bounds.position + ((Vector2)nearestRoom.bounds.size) / 2, nearestRoom)
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
    private Room2D FindNearestConnectedRoom(Room2D room) {
        Room2D nearest = null;
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
        int currentPathIndex = 0;

        foreach (var edge in selectedEdges) {
            // 시작점과 도착점 설정
            var startRoom = (edge.U as Vertex<Room2D>).Item;
            var endRoom = (edge.V as Vertex<Room2D>).Item;

            // 시작점과 도착점을 방의 중심점으로 설정
            var startPos = new Vector2Int(
                (int)startRoom.bounds.center.x,
                (int)startRoom.bounds.center.y
            );
            var endPos = new Vector2Int(
                (int)endRoom.bounds.center.x,
                (int)endRoom.bounds.center.y
            );

            Debug.Log($"경로 탐색 시작: 시작점 [{startPos.x},{startPos.y}] -> 끝점 [{endPos.x},{endPos.y}]");

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
                // 경로 저장
                pathLines.Add(path);
                
                // 먼저 모든 위치를 복도로 표시
                foreach (var pos in path) {
                    if (grid[pos] == CellType.None) {
                        grid[pos] = CellType.Hallway;
                    }
                    positionToPathIndex[pos] = currentPathIndex;
                }

                // 노드 정보 생성 및 방향 설정
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    // 노드 정보 가져오기 또는 생성
                    if (!nodes.TryGetValue(current, out Node node)) {
                        node = new Node(current);
                        nodes[current] = node;
                    }

                    node.PathIndex = currentPathIndex;

                    // 이전 노드와의 방향 설정
                    if (i > 0) {
                        var prev = path[i - 1];
                        var direction = GetDirection(prev, current);
                        var oppositeDirection = GetOppositeDirection(direction);
                        if (!node.OpenDirections.Contains(oppositeDirection)) {
                            node.OpenDirections.Add(oppositeDirection);
                        }
                    }

                    // 다음 노드와의 방향 설정
                    if (i < path.Count - 1) {
                        var next = path[i + 1];
                        var direction = GetDirection(current, next);
                        if (!node.OpenDirections.Contains(direction)) {
                            node.OpenDirections.Add(direction);
                        }
                    }
                }

                // 복도 시각화
                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }
                }
                
                currentPathIndex++;
            }
        }
    }
    
    // 위치가 방 안에 있는지 확인
    private bool IsPositionInRoom(Vector2Int position, Room2D room) {
        return position.x >= room.bounds.xMin && position.x < room.bounds.xMax && 
               position.y >= room.bounds.yMin && position.y < room.bounds.yMax;
    }

    // 방을 시각화하는 메서드
    void PlaceRoom(Vector2Int location, Vector2Int size) {
        // 스케일에 따른 오프셋 계산 (크기에서 1을 뺀 값으로 계산)
        Vector3 offset = new Vector3((size.x - 1) * 0.5f, 0, (size.y - 1) * 0.5f);
        Vector3 position = new Vector3(location.x, 0, location.y) + offset;
        
        GameObject roomObj = Instantiate(cubePrefab, position, Quaternion.identity);
        roomObj.transform.SetParent(roomsParent.transform);
        roomObj.transform.localScale = new Vector3(size.x, 1, size.y);
        roomObj.GetComponent<MeshRenderer>().material = normalRoomMaterial;
        
        // 기본 이름만 설정 (방 번호와 위치 정보)
        roomObj.name = $"Room-{rooms.Count}-[{location.x},{location.y}]";

        // RoomVisualizer 컴포넌트 추가
        var roomVisualizer = roomObj.AddComponent<RoomVisualizer>();
        roomVisualizer.RoomType = RoomType.Normal; // 기본값 설정
    }

    // 복도를 시각화하는 메서드
    void PlaceHallway(Vector2Int location) {
        int pathIndex = positionToPathIndex[location];
        GameObject pathObj = GameObject.Find($"Path-{pathIndex}");
        
        if (pathObj == null) {
            pathObj = new GameObject($"Path-{pathIndex}");
            pathObj.transform.SetParent(pathsParent.transform);
            pathObj.transform.position = Vector3.zero;
        }
        
        GameObject cube = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        cube.name = $"Hallway-[{location.x},{location.y}]";
        cube.transform.SetParent(pathObj.transform);
        cube.transform.localScale = Vector3.one;
        cube.GetComponent<MeshRenderer>().material = hallwayMaterial;
    }

    // 경로 시각화 - 방향 화살표와 시작/끝점 표시 추가
    private void OnDrawGizmos() {
        if (selectedEdges == null) return;

        if (showMSTEdges) {
            Gizmos.color = mstColor;
            foreach (var edge in selectedEdges) {
                if (edge?.U is Vertex<Room2D> u && edge?.V is Vertex<Room2D> v) {
                    Vector3 start = new Vector3(u.Item.bounds.center.x, 0, u.Item.bounds.center.y);
                    Vector3 end = new Vector3(v.Item.bounds.center.x, 0, v.Item.bounds.center.y);
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
                    Vector3 start = new Vector3(path[i-1].x, 0, path[i-1].y);
                    Vector3 end = new Vector3(path[i].x, 0, path[i].y);
                    Gizmos.DrawLine(start, end);

                    // 방향 표시를 위한 구체 추가
                    Gizmos.DrawSphere(start, 0.2f);
                    Gizmos.DrawSphere(end, 0.2f);
                    
                    // 경로의 시작점과 끝점을 다른 색상으로 표시
                    if (i == 1) {
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(start, 0.3f);
                        Gizmos.color = pathColor;
                    }
                    if (i == path.Count - 1) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(end, 0.3f);
                        Gizmos.color = pathColor;
                    }
                }
            }
        }
        
        // 노드의 열린 방향 시각화
        if (nodes != null) {
            foreach (var node in nodes.Values) {
                if (node.OpenDirections.Count == 0) continue;

                Vector3 center = new Vector3(node.Position.x, 0, node.Position.y);
                float arrowLength = 0.3f;
                float arrowHeadLength = 0.1f;
                float arrowHeadAngle = 20.0f;

                foreach (var direction in node.OpenDirections) {
                    Vector3 dir = Vector3.zero;
                    Color color = Color.white;

                    switch (direction) {
                        case PathDirection.XPlus:
                            dir = Vector3.right;
                            color = Color.red;
                            break;
                        case PathDirection.XMinus:
                            dir = Vector3.left;
                            color = new Color(1f, 0.5f, 0.5f);
                            break;
                        case PathDirection.YPlus:
                            dir = Vector3.forward;
                            color = Color.white;
                            break;
                        case PathDirection.YMinus:
                            dir = Vector3.back;
                            color = Color.black;
                            break;
                    }

                    // 인접 셀 확인
                    Vector2Int neighborPos = node.Position + new Vector2Int(
                        direction == PathDirection.XPlus ? 1 : direction == PathDirection.XMinus ? -1 : 0,
                        direction == PathDirection.YPlus ? 1 : direction == PathDirection.YMinus ? -1 : 0
                    );

                    // 인접 셀이 없는 경우 다른 색상으로 표시
                    if (grid != null) {
                        if (!grid.InBounds(neighborPos) || grid[neighborPos] == CellType.None) {
                            color = Color.yellow;
                        }
                    }

                    Gizmos.color = color;
                    Vector3 end = center + dir * arrowLength;
                    
                    // 화살표 몸통
                    Gizmos.DrawLine(center, end);
                    
                    // 화살표 머리
                    Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
                    Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
                    Gizmos.DrawLine(end, end + right * arrowHeadLength);
                    Gizmos.DrawLine(end, end + left * arrowHeadLength);
                }
            }
        }
    }

    private void SetRoomProperties() {
        // 시작방 찾기 (원점에서 가장 가까운 방)
        startRoom = FindClosestRoomToOrigin();
        startRoom.Properties.RoomType = RoomType.Start;
        startRoom.Properties.HasEnemies = random.Next(0, 100) < generationOptions.EnemySpawnChance;
        startRoom.Properties.HasItemChest = random.Next(0, 100) < generationOptions.ItemChestSpawnChance;

        // 출구방 찾기 (시작방에서 가장 먼 방)
        exitRoom = FindFarthestRoomFromStart();
        exitRoom.Properties.RoomType = RoomType.Exit;
        exitRoom.Properties.HasEnemies = random.Next(0, 100) < generationOptions.EnemySpawnChance;
        exitRoom.Properties.HasItemChest = random.Next(0, 100) < generationOptions.ItemChestSpawnChance;

        // 보스 설정
        if (generationOptions.SpawnBoss) {
            exitRoom.Properties.HasBoss = true;
            exitRoom.Properties.HasEnemies = true; // 보스 방은 무조건 적 출현
        }

        // 상점 방 설정
        if (generationOptions.SpawnMerchant) {
            int merchantRoomIndex = random.Next(0, rooms.Count);
            merchantRoom = rooms[merchantRoomIndex];
            merchantRoom.Properties.HasMerchant = true;
        }

        // 모든 방에 대해 속성 설정
        foreach (var room in rooms) {
            if (room == startRoom || room == exitRoom) continue;

            // 적 출현 확률
            if (random.Next(0, 100) < generationOptions.EnemySpawnChance) {
                room.Properties.HasEnemies = true;
            }

            // 아이템 상자 출현 확률
            if (random.Next(0, 100) < generationOptions.ItemChestSpawnChance) {
                room.Properties.HasItemChest = true;
            }
        }

        // 시각화 오브젝트의 RoomProperties 업데이트
        UpdateRoomVisualization();
    }

    private void UpdateRoomVisualization() {
        foreach (Transform child in roomsParent.transform) {
            // 기본 이름 구성
            string baseName = child.name;
        
            if (child.name.StartsWith("Room-")) {
                var roomVisualizer = child.GetComponent<RoomVisualizer>();
                if (roomVisualizer != null) {
                    // 방 번호 추출
                    string roomNumberStr = child.name.Split('-')[1].Split('[')[0];
                    if (int.TryParse(roomNumberStr, out int roomIndex) && roomIndex < rooms.Count) {
                        var room = rooms[roomIndex];
                        roomVisualizer.RoomType = room.Properties.RoomType;
                        roomVisualizer.HasEnemies = room.Properties.HasEnemies;
                        roomVisualizer.HasItemChest = room.Properties.HasItemChest;
                        roomVisualizer.HasMerchant = room.Properties.HasMerchant;
                        roomVisualizer.HasBoss = room.Properties.HasBoss;

                        // 방의 타입에 따라 접미사 추가
                        if (room.Properties.RoomType == RoomType.Start) {
                            baseName += "-Start";
                            child.GetComponent<MeshRenderer>().material = startRoomMaterial;
                        } else if (room.Properties.RoomType == RoomType.Exit) {
                            baseName += "-End";
                            child.GetComponent<MeshRenderer>().material = endRoomMaterial;
                        }
                        if (room.Properties.HasMerchant) {
                            baseName += "-Shop";
                            child.GetComponent<MeshRenderer>().material = merchantRoomMaterial;
                        }
                        
                        child.name = baseName;
                    }
                }
            }
        }
    }


    private Room2D FindClosestRoomToOrigin() {
        Room2D closestRoom = null;
        float minDistance = float.MaxValue;
        Vector2 origin = Vector2.zero;

        foreach (var room in rooms) {
            float distance = Vector2.Distance(room.bounds.center, origin);
            if (distance < minDistance) {
                minDistance = distance;
                closestRoom = room;
            }
        }

        return closestRoom;
    }

    private Room2D FindFarthestRoomFromStart() {
        Room2D farthestRoom = null;
        float maxDistance = 0;

        foreach (var room in rooms) {
            if (room == startRoom) continue;

            float distance = Vector2.Distance(room.bounds.center, startRoom.bounds.center);
            if (distance > maxDistance) {
                maxDistance = distance;
                farthestRoom = room;
            }
        }

        return farthestRoom;
    }

    // 3D 큐브를 생성하는 메서드
    void PlaceCube(Vector2Int location, Vector2Int size, Material material) {
        GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        go.GetComponent<MeshRenderer>().material = material;
    }
}
